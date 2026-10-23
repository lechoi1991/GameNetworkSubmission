"""Drive two UdpPeer processes with scripted console input and record their output.

usage: python udp_driver.py <UdpPeer.dll> <out_dir>
"""
import datetime
import os
import socket
import subprocess
import sys
import threading
import time

DLL, OUT = sys.argv[1], sys.argv[2]
os.makedirs(OUT, exist_ok=True)


def ts():
    return datetime.datetime.now().strftime("%H:%M:%S.%f")[:-3]


class Peer:
    def __init__(self, peer_id):
        self.id = peer_id
        self.log = open(os.path.join(OUT, f"peer_{peer_id}_console.txt"), "w", encoding="utf-8")
        self.proc = subprocess.Popen(
            ["dotnet", DLL], stdin=subprocess.PIPE, stdout=subprocess.PIPE,
            stderr=subprocess.STDOUT, text=True, encoding="utf-8", bufsize=1)
        self.lock = threading.Lock()
        threading.Thread(target=self._read, daemon=True).start()
        self.cmd(peer_id, note="Peer ID 입력")

    def _read(self):
        for line in self.proc.stdout:
            with self.lock:
                self.log.write(f"[{ts()}] {line.rstrip()}\n")
                self.log.flush()

    def note(self, text):
        with self.lock:
            self.log.write(f"\n[{ts()}] ##### {text}\n")
            self.log.flush()

    def cmd(self, text, wait=0.6, note=None):
        with self.lock:
            self.log.write(f"[{ts()}] <입력> {text}" + (f"   ({note})" if note else "") + "\n")
            self.log.flush()
        self.proc.stdin.write(text + "\n")
        self.proc.stdin.flush()
        time.sleep(wait)

    def quit(self):
        self.cmd("quit", wait=1.0)
        self.proc.wait(timeout=10)
        time.sleep(0.3)
        self.log.close()


# 1. 잘못된 메시지: A만 실행하고 B 자리(127.0.0.1:5001)에서 시험 송신기가 보낸다.
a = Peer("A")
time.sleep(1.5)
a.note("시험 1: 잘못된 메시지 수신 (B 포트 5001에 바인딩한 시험 송신기 사용, B는 아직 실행 전)")
a.cmd("show", note="상대 미수신 상태 확인")

bad = [
    (b"", "빈 데이터그램"),
    (b"   ", "공백만 있는 메시지"),
    (b"{bad json", "JSON 형식 오류"),
    (b"null", "null JSON"),
    (b'{"p":"B","s":1,"x":"abc","y":2}', "X에 문자열 (해석 불가 값)"),
    (b'{"p":"B","s":1,"x":99999999999,"y":2}', "X가 int 범위 초과"),
    (b'{"p":"A","s":1,"x":1,"y":1}', "자기 자신(A)의 ID"),
    (b'{"p":"C","s":1,"x":1,"y":1}', "알 수 없는 피어 ID"),
    (b'{"p":"B","s":0,"x":1,"y":1}', "순서 번호 0"),
    (b'{"p":"B","s":-5,"x":1,"y":1}', "음수 순서 번호"),
    (b'{"PeerId":"B","Sequence":1,"X":1,"Y":1}', "조정 전 형식(긴 필드 이름)"),
]
tester_log = open(os.path.join(OUT, "invalid_sender_log.txt"), "w", encoding="utf-8")
tester_log.write("시험 송신기: 127.0.0.1:5001 -> 127.0.0.1:5000 (피어 A)\n\n")
s = socket.socket(socket.AF_INET, socket.SOCK_DGRAM)
s.bind(("127.0.0.1", 5001))
for payload, desc in bad:
    a.note(f"시험 송신기 -> A: {desc}: {payload!r}")
    tester_log.write(f"[{ts()}] {desc}: {payload!r}\n")
    s.sendto(payload, ("127.0.0.1", 5000))
    time.sleep(0.4)
s.close()

other = socket.socket(socket.AF_INET, socket.SOCK_DGRAM)
other.bind(("127.0.0.1", 6000))
payload = b'{"p":"B","s":1,"x":1,"y":1}'
a.note(f"시험 송신기(포트 6000) -> A: 설정하지 않은 송신 주소: {payload!r}")
tester_log.write(f"[{ts()}] 설정하지 않은 송신 포트 6000: {payload!r}\n")
other.sendto(payload, ("127.0.0.1", 5000))
other.close()
tester_log.close()
time.sleep(0.5)
a.cmd("show", note="잘못된 메시지 후에도 미수신 상태 유지 확인")
a.cmd("stats")

# 2. 정상 위치 교환
b = Peer("B")
time.sleep(1.5)
for p in (a, b):
    p.note("시험 2: 양쪽 위치 변경·전송과 상대 위치 표시")
b.cmd("show", note="상대 미수신 상태 확인")
a.cmd("move 3 5")
b.cmd("move 8 2")
a.cmd("show")
b.cmd("show")

# 3. 오래된 번호·중복 번호
for p in (a, b):
    p.note("시험 3: 오래된 순서 번호·중복 순서 번호 무시 (A가 replay로 당시 메시지를 그대로 재전송)")
a.cmd("move 4 6")
a.cmd("move 7 7")
b.cmd("show", note="B가 반영한 A 위치 (7, 7) Seq=3 확인")
a.cmd("replay 2", note="오래된 번호: Seq=2 (4, 6)")
a.cmd("replay 1", note="오래된 번호: Seq=1 (3, 5)")
a.cmd("replay 3", note="중복 번호: Seq=3 (7, 7)")
b.cmd("show", note="여전히 (7, 7) Seq=3 이어야 함")
a.cmd("move 3 5", wait=0.6, note="이전과 같은 좌표라도 새 메시지는 Seq=4")
b.cmd("show")

# 4. 전송량 시험: 100개, 10,000개 (A -> B), 10,000개 (B -> A)
for p in (a, b):
    p.note("시험 4: 전송량 조건 비교")
for n in (100, 10000):
    b.cmd(f"benchstart {n}", wait=0.3)
    a.cmd(f"bench {n}", wait=2.0)
    b.cmd("benchstop", wait=0.5)
    b.cmd("show")
a.cmd("benchstart 10000", wait=0.3)
b.cmd("bench 10000", wait=2.0)
a.cmd("benchstop", wait=0.5)
a.cmd("show")
b.cmd("show")
a.cmd("stats")
b.cmd("stats")

a.quit()
b.quit()
print("done")
