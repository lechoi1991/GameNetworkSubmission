using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using System.Diagnostics;

namespace UdpPeerPosition;

internal class Program
{
    private static string localPeerId = "";
    private static string remotePeerId = "";

    private static int localPort;
    private static int remotePort;

    // 내 좌표와 상대 좌표를 별도로 보관합니다.
    private static int myX;
    private static int myY;

    private static long nextSequence;
    private static long lastReceivedSequence = -1;

    private static int remoteX;
    private static int remoteY;
    private static bool hasReceivedPosition;

    // 실제로 전송한 메시지를 순서 번호별로 보관합니다.
    private static readonly Dictionary<long, string> sentHistory = new();

    private static UdpClient? udpClient;
    private static IPEndPoint? remoteEndPoint;

    private static readonly JsonSerializerOptions jsonOptions = new();

    // 수신·송신 결과를 누적 집계합니다.
    private static long totalReceivedPackets;
    private static long appliedPackets;
    private static long invalidPackets;
    private static long stalePackets;
    private static long sendCompletedPackets;
    private static long sendErrors;

    // 시험 수신 집계 상태입니다.
    private static int benchmarkCaptureActive;
    private static long benchmarkCaptureReceived;
    private static int benchmarkExpectedCount;
    private static Stopwatch? benchmarkCaptureStopwatch;

    // 대량 전송 중에는 메시지마다 콘솔을 출력하지 않습니다.
    private static bool isBenchmarkSending;

    private static async Task Main()
    {
        Console.WriteLine("=== UDP Peer Position ===");

        Console.Write("Peer ID (A or B): ");
        string? input = Console.ReadLine()?.Trim().ToUpperInvariant();

        if (input != "A" && input != "B")
        {
            Console.WriteLine("Peer ID must be A or B.");
            return;
        }

        localPeerId = input;
        remotePeerId = localPeerId == "A" ? "B" : "A";

        localPort = localPeerId == "A" ? 5000 : 5001;
        remotePort = localPeerId == "A" ? 5001 : 5000;

        try
        {
            udpClient = new UdpClient(localPort);
        }
        catch (SocketException ex)
        {
            Console.WriteLine($"Could not open local port: {ex.Message}");
            return;
        }

        remoteEndPoint = new IPEndPoint(
            IPAddress.Loopback,
            remotePort
        );

        using CancellationTokenSource cancellation = new();

        Console.WriteLine();
        Console.WriteLine($"My peer ID: {localPeerId}");
        Console.WriteLine($"Local port: {localPort}");
        Console.WriteLine($"Remote peer: {remotePeerId}");
        Console.WriteLine($"Remote port: {remotePort}");
        Console.WriteLine();
        Console.WriteLine("Commands:");
        Console.WriteLine("  move X Y  - Change my position and send it");
        Console.WriteLine("  show      - Show both positions");
        Console.WriteLine("  send      - Send my current position with a new sequence");
        Console.WriteLine("  replay N  - Resend the original message with sequence N");
        Console.WriteLine("  bench N       - Send N position messages");
        Console.WriteLine("  benchstart N  - Start receiving test measurement");
        Console.WriteLine("  benchstop     - Stop measurement and show results");
        Console.WriteLine("  stats         - Show cumulative counters");
        Console.WriteLine("  quit      - Exit");
        Console.WriteLine();

        Task receiveTask = ReceivePositionsAsync(
            udpClient,
            cancellation.Token
        );

        try
        {
            while (true)
            {
                Console.Write("> ");
                string? commandLine = Console.ReadLine();

                if (commandLine == null ||
                    commandLine.Trim().Equals(
                        "quit",
                        StringComparison.OrdinalIgnoreCase))
                {
                    break;
                }

                string[] parts = commandLine.Split(
                    ' ',
                    StringSplitOptions.RemoveEmptyEntries
                );

                if (parts.Length == 0)
                    continue;

                switch (parts[0].ToLowerInvariant())
                {
                    case "move":
                        await HandleMoveAsync(parts);
                        break;

                    case "show":
                        ShowPositions();
                        break;

                    case "send":
                        await SendNewPositionAsync();
                        break;

                    case "replay":
                        await ReplayMessageAsync(parts);
                        break;

                        case "bench":
                        await HandleBenchmarkAsync(parts);
                        break;

                    case "benchstart":
                        StartBenchmarkCapture(parts);
                        break;

                    case "benchstop":
                        StopBenchmarkCapture();
                        break;

                    case "stats":
                        ShowStatistics();
                        break;

                    default:
                        Console.WriteLine("Unknown command.");
                        break;
                }
            }
        }
        finally
        {
            cancellation.Cancel();
            udpClient.Close();

            try
            {
                await receiveTask;
            }
            catch (OperationCanceledException)
            {
                // 프로그램 종료로 수신 대기를 취소합니다.
            }
            catch (ObjectDisposedException)
            {
                // 종료 과정에서 소켓이 이미 닫힌 경우입니다.
            }

            udpClient.Dispose();
            Console.WriteLine("Peer stopped.");
        }
    }

    private static async Task HandleMoveAsync(string[] parts)
    {
        if (parts.Length != 3 ||
            !int.TryParse(parts[1], out int newX) ||
            !int.TryParse(parts[2], out int newY))
        {
            Console.WriteLine("Usage: move X Y (integers only)");
            return;
        }

        if (newX == myX && newY == myY)
        {
            Console.WriteLine(
                "Position unchanged. Use 'send' to send it again."
            );
            return;
        }

        myX = newX;
        myY = newY;

        await SendNewPositionAsync();
    }

    private static async Task SendNewPositionAsync()
    {
        nextSequence++;

        PositionMessage message = new()
        {
            PeerId = localPeerId,
            Sequence = nextSequence,
            X = myX,
            Y = myY
        };

        string json = JsonSerializer.Serialize(message, jsonOptions);

        // 원본 메시지를 보관해 나중에 동일한 내용으로 재전송합니다.
        sentHistory[message.Sequence] = json;

        await SendJsonAsync(json, message.Sequence, false);
    }

    private static async Task ReplayMessageAsync(string[] parts)
    {
        if (parts.Length != 2 ||
            !long.TryParse(parts[1], out long sequence) ||
            sequence <= 0)
        {
            Console.WriteLine("Usage: replay N (positive sequence number)");
            return;
        }

        if (!sentHistory.TryGetValue(sequence, out string? json))
        {
            Console.WriteLine(
                $"No original message found for sequence {sequence}."
            );
            return;
        }

        // 저장된 JSON을 그대로 보내므로 좌표와 순서 번호가 바뀌지 않습니다.
        await SendJsonAsync(json, sequence, true);
    }

    
    private static async Task SendJsonAsync(
        string json,
        long sequence,
        bool isReplay)
    {
        if (udpClient == null || remoteEndPoint == null)
            return;

        byte[] data = Encoding.UTF8.GetBytes(json);

        try
        {
            await udpClient.SendAsync(data, remoteEndPoint);
            Interlocked.Increment(ref sendCompletedPackets);

            if (!isBenchmarkSending)
            {
                Console.WriteLine(
                    $"[Sent{(isReplay ? " replay" : "")}] " +
                    $"Seq={sequence}, Data={json}"
                );
            }
        }
        catch (SocketException ex)
        {
            Interlocked.Increment(ref sendErrors);
            Console.WriteLine($"[Send error] {ex.Message}");
        }
        catch (ObjectDisposedException)
        {
            Interlocked.Increment(ref sendErrors);
            Console.WriteLine("Socket is already closed.");
        }
    }

    private static async Task ReceivePositionsAsync(
        UdpClient client,
        CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                UdpReceiveResult result =
                    await client.ReceiveAsync(cancellationToken);

                // 수신한 데이터그램을 집계하고, 시험 측정 중이면 별도로 셉니다.
                Interlocked.Increment(ref totalReceivedPackets);

                if (Volatile.Read(ref benchmarkCaptureActive) == 1)
                {
                    Interlocked.Increment(ref benchmarkCaptureReceived);
                }

                // 설정한 상대의 주소와 포트에서 온 데이터인지 확인합니다.
                if (!IPAddress.IsLoopback(result.RemoteEndPoint.Address) ||
                    result.RemoteEndPoint.Port != remotePort)
                {
                    Console.WriteLine(
                        "[Ignored] Unexpected sender endpoint."
                    );
                    continue;
                }                

                string json = Encoding.UTF8.GetString(result.Buffer);

                if (string.IsNullOrWhiteSpace(json))
                {
                    Interlocked.Increment(ref invalidPackets);
                    Console.WriteLine("[Ignored] Empty message.");
                    continue;
                }

                PositionMessage? message;

                try
                {
                    message = JsonSerializer.Deserialize<PositionMessage>(
                        json,
                        jsonOptions
                    );
                }
                catch (JsonException)
                {
                    Interlocked.Increment(ref invalidPackets);
                    Console.WriteLine("[Ignored] Invalid JSON.");
                    continue;
                }

                if (message == null ||
                    message.PeerId != remotePeerId ||
                    message.Sequence <= 0)
                {
                    Interlocked.Increment(ref invalidPackets);
                    Console.WriteLine("[Ignored] Invalid message fields.");
                    continue;
                }

                // 마지막으로 반영한 순서 번호보다 작거나 같으면 무시합니다.
                if (message.Sequence <= lastReceivedSequence)
                {
                    Interlocked.Increment(ref stalePackets);

                    Console.WriteLine(
                        $"[Ignored stale/duplicate] " +
                        $"Peer={message.PeerId}, " +
                        $"Seq={message.Sequence}, " +
                        $"Last applied={lastReceivedSequence}"
                    );

                    continue;
                }

                // 수신한 상대 좌표만 갱신합니다.
                remoteX = message.X;
                remoteY = message.Y;
                lastReceivedSequence = message.Sequence;
                hasReceivedPosition = true;

                // 정상적으로 반영한 메시지 수를 증가시킵니다.
                Interlocked.Increment(ref appliedPackets);

                // 측정 중에는 메시지마다 출력하지 않아 콘솔 부하를 줄입니다.
                if (Volatile.Read(ref benchmarkCaptureActive) == 0)
                {
                    Console.WriteLine();
                    Console.WriteLine(
                        $"[Updated] Peer={remotePeerId}, " +
                        $"Seq={lastReceivedSequence}, " +
                        $"Position=({remoteX}, {remoteY})"
                    );
                    Console.Write("> ");
                }
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (ObjectDisposedException)
            {
                break;
            }
            catch (SocketException ex)
            {
                if (!cancellationToken.IsCancellationRequested)
                    Console.WriteLine($"[Receive error] {ex.Message}");

                break;
            }
        }
    }

    private static void ShowPositions()
    {
        Console.WriteLine();
        Console.WriteLine(
            $"My position ({localPeerId}): ({myX}, {myY})"
        );

        if (hasReceivedPosition)
        {
            Console.WriteLine(
                $"Remote position ({remotePeerId}): " +
                $"({remoteX}, {remoteY}), " +
                $"Sequence={lastReceivedSequence}"
            );
        }
        else
        {
            Console.WriteLine(
                $"Remote position ({remotePeerId}): Not received yet"
            );
        }

        Console.WriteLine();
    }

    private static async Task HandleBenchmarkAsync(string[] parts)
{
    if (parts.Length != 2 ||
        !int.TryParse(parts[1], out int count) ||
        count < 1 ||
        count > 100000)
    {
        Console.WriteLine("Usage: bench N (1 to 100000)");
        return;
    }

    if (udpClient == null || remoteEndPoint == null)
        return;

    var timer = Stopwatch.StartNew();
    long completedBefore = Interlocked.Read(ref sendCompletedPackets);

    // UDP payload bytes only (IPv4 20 + UDP 8 header bytes are not included).
    long payloadBytes = 0;

    isBenchmarkSending = true;

    try
    {
        for (int i = 0; i < count; i++)
        {
            nextSequence++;

            // 각 시험 메시지는 증가하는 순서 번호와 정수 좌표를 사용합니다.。
            PositionMessage message = new()
            {
                PeerId = localPeerId,
                Sequence = nextSequence,
                X = i,
                Y = -i
            };

            string json = JsonSerializer.Serialize(message, jsonOptions);
            sentHistory[message.Sequence] = json;
            payloadBytes += Encoding.UTF8.GetByteCount(json);

            await SendJsonAsync(json, message.Sequence, false);
        }
    }
    finally
    {
        isBenchmarkSending = false;
        timer.Stop();
    }

    long completed = Interlocked.Read(ref sendCompletedPackets)
        - completedBefore;

    Console.WriteLine();
    Console.WriteLine("=== Sender Benchmark Result ===");
    Console.WriteLine($"Requested messages: {count}");
    Console.WriteLine($"Send calls completed: {completed}");
    Console.WriteLine($"Send errors: {count - completed}");
    Console.WriteLine($"Elapsed milliseconds: {timer.Elapsed.TotalMilliseconds:F2}");
    Console.WriteLine($"Payload bytes sent: {payloadBytes}");
    Console.WriteLine($"Average payload bytes per message: {(double)payloadBytes / count:F2}");
    Console.WriteLine("Wait for the receiver to finish measuring.");
}

private static void StartBenchmarkCapture(string[] parts)
{
    if (parts.Length != 2 ||
        !int.TryParse(parts[1], out int expected) ||
        expected < 1 ||
        expected > 100000)
    {
        Console.WriteLine("Usage: benchstart N (1 to 100000)");
        return;
    }

    if (Interlocked.CompareExchange(
            ref benchmarkCaptureActive, 1, 0) != 0)
    {
        Console.WriteLine("A measurement is already active.");
        return;
    }

    benchmarkExpectedCount = expected;
    Interlocked.Exchange(ref benchmarkCaptureReceived, 0);

    benchmarkCaptureStopwatch = Stopwatch.StartNew();

    Console.WriteLine();
    Console.WriteLine("=== Receiver Measurement Started ===");
    Console.WriteLine($"Expected messages: {expected}");
    Console.WriteLine("Run the sender's bench command now.");
    Console.WriteLine("Use benchstop after the sender finishes.");
}

private static void StopBenchmarkCapture()
{
    if (Interlocked.Exchange(ref benchmarkCaptureActive, 0) == 0)
    {
        Console.WriteLine("No active measurement.");
        return;
    }

    benchmarkCaptureStopwatch?.Stop();

    long received = Interlocked.Read(ref benchmarkCaptureReceived);
    long observedMissing = Math.Max(0, benchmarkExpectedCount - received);

    Console.WriteLine();
    Console.WriteLine("=== Receiver Benchmark Result ===");
    Console.WriteLine($"Expected messages: {benchmarkExpectedCount}");
    Console.WriteLine($"Datagrams observed during window: {received}");
    Console.WriteLine($"Observed missing at stop: {observedMissing}");

    if (received > benchmarkExpectedCount)
    {
        Console.WriteLine(
            $"Extra datagrams observed: {received - benchmarkExpectedCount}"
        );
    }

    Console.WriteLine(
        $"Measurement window milliseconds: " +
        $"{benchmarkCaptureStopwatch?.Elapsed.TotalMilliseconds:F2}"
    );

    Console.WriteLine(
        "The missing count is an observation at stop time, not proof " +
        "that every unobserved datagram was permanently lost."
    );
}

    private static void ShowStatistics()
    {
        Console.WriteLine();
        Console.WriteLine("=== Cumulative Statistics ===");
        Console.WriteLine(
            $"Send calls completed: {Interlocked.Read(ref sendCompletedPackets)}"
        );
        Console.WriteLine(
            $"Send errors: {Interlocked.Read(ref sendErrors)}"
        );
        Console.WriteLine(
            $"Received datagrams: {Interlocked.Read(ref totalReceivedPackets)}"
        );
        Console.WriteLine(
            $"Applied position messages: {Interlocked.Read(ref appliedPackets)}"
        );
        Console.WriteLine(
            $"Invalid messages: {Interlocked.Read(ref invalidPackets)}"
        );
        Console.WriteLine(
            $"Stale/duplicate messages: {Interlocked.Read(ref stalePackets)}"
        );
    }

    // 위치 메시지에 포함되는 데이터 구조입니다.
    // 메시지 크기를 줄이기 위해 JSON 필드 이름을 한 글자로 씁니다.
    // 예: {"p":"A","s":1,"x":3,"y":5}
    private sealed class PositionMessage
    {
        [JsonPropertyName("p")]
        public string PeerId { get; set; } = "";

        [JsonPropertyName("s")]
        public long Sequence { get; set; }

        [JsonPropertyName("x")]
        public int X { get; set; }

        [JsonPropertyName("y")]
        public int Y { get; set; }
    }
}