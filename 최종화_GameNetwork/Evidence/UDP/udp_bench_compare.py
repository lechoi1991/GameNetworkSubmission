"""Run the same UDP throughput condition several times and summarize the results.

Used for the before/after comparison in TEST_REPORT.md (message size adjustment).

usage: python udp_bench_compare.py <UdpPeer.dll> <out_dir> <label>

Condition: peer A sends `bench 10000` to peer B on 127.0.0.1 (A:5000 -> B:5001).
One warm-up run of 1,000 messages is excluded, then 5 measured runs follow.
"""
import datetime
import os
import re
import subprocess
import sys
import threading
import time

DLL, OUT, LABEL = sys.argv[1], sys.argv[2], sys.argv[3]
os.makedirs(OUT, exist_ok=True)

MESSAGES = 10000
RUNS = 5


def ts():
    return datetime.datetime.now().strftime("%H:%M:%S.%f")[:-3]


class Peer:
    def __init__(self, peer_id):
        self.lines = []
        self.log = open(os.path.join(OUT, f"{LABEL}_peer_{peer_id}.txt"), "w", encoding="utf-8")
        self.proc = subprocess.Popen(
            ["dotnet", DLL], stdin=subprocess.PIPE, stdout=subprocess.PIPE,
            stderr=subprocess.STDOUT, text=True, encoding="utf-8", bufsize=1)
        self.lock = threading.Lock()
        threading.Thread(target=self._read, daemon=True).start()
        self.cmd(peer_id)

    def _read(self):
        for line in self.proc.stdout:
            with self.lock:
                self.lines.append(line.rstrip())
                self.log.write(f"[{ts()}] {line.rstrip()}\n")
                self.log.flush()

    def note(self, text):
        with self.lock:
            self.log.write(f"\n[{ts()}] ##### {text}\n")
            self.log.flush()

    def cmd(self, text, wait=0.6):
        with self.lock:
            self.log.write(f"[{ts()}] <입력> {text}\n")
            self.log.flush()
        self.proc.stdin.write(text + "\n")
        self.proc.stdin.flush()
        time.sleep(wait)

    def mark(self):
        with self.lock:
            return len(self.lines)

    def since(self, start):
        with self.lock:
            return list(self.lines[start:])

    def quit(self):
        self.cmd("quit", wait=1.0)
        self.proc.wait(timeout=10)
        self.log.close()


def value(lines, key):
    for line in lines:
        m = re.match(rf"\s*{re.escape(key)}:\s*([-\d.]+)", line)
        if m:
            return float(m.group(1))
    return None


a = Peer("A")
b = Peer("B")
time.sleep(1.5)

for p in (a, b):
    p.note(f"{LABEL}: warm-up bench 1000 (excluded)")
b.cmd("benchstart 1000", wait=0.3)
a.cmd("bench 1000", wait=1.5)
b.cmd("benchstop", wait=0.5)

results = []
for run in range(1, RUNS + 1):
    for p in (a, b):
        p.note(f"{LABEL}: run {run} bench {MESSAGES}")
    b.cmd(f"benchstart {MESSAGES}", wait=0.3)
    a_start = a.mark()
    a.cmd(f"bench {MESSAGES}", wait=2.0)
    b_start = b.mark()
    b.cmd("benchstop", wait=0.5)
    b.cmd("show", wait=0.5)
    a_lines, b_lines = a.since(a_start), b.since(b_start)
    results.append({
        "run": run,
        "sent": value(a_lines, "Send calls completed"),
        "errors": value(a_lines, "Send errors"),
        "send_ms": value(a_lines, "Elapsed milliseconds"),
        "bytes": value(a_lines, "Payload bytes sent"),
        "avg_bytes": value(a_lines, "Average payload bytes per message"),
        "received": value(b_lines, "Datagrams observed during window"),
        "missing": value(b_lines, "Observed missing at stop"),
        "remote": next((l.strip() for l in b_lines if l.strip().startswith("Remote position")), ""),
    })

a.cmd("stats")
b.cmd("stats")
a.quit()
b.quit()

summary = os.path.join(OUT, f"{LABEL}_summary.txt")
with open(summary, "w", encoding="utf-8") as f:
    f.write(f"{LABEL}: A -> B, bench {MESSAGES}, {RUNS} runs after a 1,000-message warm-up\n")
    for r in results:
        f.write(
            f"run {r['run']}: sent={r['sent']:.0f} errors={r['errors']:.0f} "
            f"received={r['received']:.0f} missing={r['missing']:.0f} "
            f"payload_bytes={r['bytes']:.0f} avg_bytes={r['avg_bytes']:.2f} "
            f"send_ms={r['send_ms']:.2f} | B {r['remote']}\n")
    avg_ms = sum(r["send_ms"] for r in results) / len(results)
    f.write(f"average send_ms={avg_ms:.2f}\n")

print(open(summary, encoding="utf-8").read())
