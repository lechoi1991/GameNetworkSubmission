# 게임 네트워크 프로그래밍 제출물 실행 안내

이 폴더는 두 프로그램으로 구성된다.

| 폴더 | 내용 |
|---|---|
| `CoinGame/` | 과제 A. Unity·NGO 코인 게임 (Host·Client) |
| `UdpPeer/` | 과제 B. 두 피어가 2차원 위치를 주고받는 UDP 콘솔 프로그램 |
| `Evidence/` | 시험 로그와 화면 캡처 |
| `DESIGN.md` | 설계 설명 |
| `TEST_REPORT.md` | 시험 기록 |

## 1. 개발 환경 및 버전

| 항목 | 버전 / 설정 |
|---|---|
| Unity Editor | 6000.6.6f1 |
| Visual Studio Code | 1.98.1 |
| .NET SDK | 10.0.402 |
| 네트워크 라이브러리 | Netcode for GameObjects 2.13.3 |
| 전송 계층 | Unity Transport 6.6.0 |
| 플레이어 이동 | CharacterController |
| UI | uGUI 2.6.0 / TextMeshPro |
| 입력 | Input System 1.20.0 |
| 기본 접속 주소 | `127.0.0.1` |
| 기본 포트 | `7777` |
| 전송 프로토콜 | UDP (Unity Transport 설정 기준) |

패키지 버전은 `CoinGame/Packages/manifest.json`에 고정되어 있다. 프로젝트를 처음 열면 Unity가 패키지를 자동으로 내려받고 `Library` 폴더를 새로 만든다 (몇 분 걸린다). UDP 콘솔 프로그램은 외부 패키지 없이 .NET 기본 라이브러리만 사용하며, `dotnet build` 시 자동으로 복원된다.

## 2. 시작 씬 및 프로젝트 위치

### Unity 프로젝트

- Unity 프로젝트 경로: `최종화_GameNetwork/CoinGame`
- 시작 씬: `Assets/Scenes/CoinGame`
- 주요 스크립트: `Assets/Scripts/`
- 플레이어 프리팹: `Assets/Prefabs/Player.prefab`
- 코인 프리팹: `Assets/Prefabs/Coin.prefab`

### 콘솔 프로젝트

- 콘솔 프로젝트 경로: `최종화_GameNetwork/UdpPeer`
- 프로젝트 파일: `UdpPeer/UdpPeer.csproj` (net10.0)
- 소스: `UdpPeer/Program.cs`
- 실행 방법: 3.4절 참고 (빌드 산출물 `bin`·`obj`는 제출에서 제외)

## 3. 빌드 및 실행 방법

### 3.1 Unity Editor에서 Host 실행

1. Unity Hub에서 CoinGame 프로젝트를 연다.
2. Assets/Scenes/CoinGame 씬을 연다.
3. Play 버튼을 누른다.
4. 화면에서 Host 시작 버튼을 누른다.
5. 연결 상태가 Host 또는 Connected로 표시되는지 확인한다.

Host는 서버와 로컬 플레이어 역할을 함께 수행한다.

### 3.2 Client 실행

1. Unity에서 Host를 먼저 실행한다.
2. Unity Build Settings 또는 Build Profiles에서 현재 테스트 씬을 포함해 빌드한다.
3. 빌드한 실행 파일을 별도 창으로 실행한다.
4. 접속 주소 `127.0.0.1`, 포트 `7777`을 사용해 Client 시작 버튼을 누른다.
5. Host와 Client 양쪽에서 플레이어가 생성되는지 확인한다.

같은 컴퓨터에서 테스트할 때는 `127.0.0.1`을 사용한다. 다른 컴퓨터에서 접속할 경우 Host 컴퓨터의 LAN IP 주소를 사용해야 하며, 방화벽과 네트워크 설정도 확인해야 한다.

### 3.3 실행 순서

일반적인 실행 순서는 다음과 같다.

1. Host 실행
2. Client 실행
3. 플레이어 생성 및 이동 확인
4. 코인 획득 및 점수 동기화 확인
5. Client 종료 후 Host 유지 확인
6. Client 재접속 후 코인 상태 동기화 확인

### 3.4 UDP 피어 실행

코인 게임과 별개의 프로그램이다. 같은 컴퓨터에서 콘솔 창 두 개를 열고 각각 실행한다.

1. 콘솔 1: `UdpPeer` 폴더에서 `dotnet run` → `Peer ID (A or B):`에 `A` 입력
2. 콘솔 2: `UdpPeer` 폴더에서 `dotnet run` → `B` 입력
3. 시작하면 각 피어가 자신의 ID, 로컬 포트, 상대 ID, 상대 포트를 출력하고 명령을 기다린다.

| 명령 | 동작 |
|---|---|
| `move X Y` | 내 위치를 정수 좌표 (X, Y)로 바꾸고 새 순서 번호로 전송 |
| `send` | 현재 위치를 새 순서 번호로 다시 전송 |
| `show` | 내 위치와 상대의 마지막 반영 위치·순서 번호 표시 (미수신이면 `Not received yet`) |
| `replay N` | 예전에 보낸 순서 번호 N의 메시지를 당시 내용 그대로 재전송 (오래된·중복 번호 시험용) |
| `bench N` | 위치 메시지 N개를 연속 전송 (전송량 시험) |
| `benchstart N` / `benchstop` | 수신 측 전송량 측정 시작·종료 |
| `stats` | 누적 송신·수신·반영·무시 수 |
| `quit` | 종료 |

정상 동작 예: A에서 `move 3 5`, B에서 `move 8 2` 후 `show`를 입력하면 A에는 `Remote position (B): (8, 2), Sequence=1`, B에는 `Remote position (A): (3, 5), Sequence=1`이 표시된다.

시험 스크립트로 자동 실행할 수도 있다 (Python 3 필요). `UdpPeer` 폴더에서 `dotnet build` 후 `최종화_GameNetwork` 폴더에서:

```
python Evidence/UDP/udp_test_driver.py UdpPeer/bin/Debug/net10.0/UdpPeer.dll <출력 폴더>
```

피어 A·B를 띄워 위치 교환, 잘못된 메시지, 오래된·중복 번호, 전송량 시험을 차례로 실행하고 각 콘솔 출력을 `<출력 폴더>`에 저장한다. 결과 예시는 `Evidence/UDP/`와 `TEST_REPORT.md` 2장에 있다.

## 4. 접속 주소·포트 및 설정 변경

### 기본 설정

| 설정 | 기본값 | 설명 |
|---|---|---|
| Address | `127.0.0.1` | 같은 컴퓨터에서 접속할 때 사용 |
| Port | `7777` | Host와 Client가 사용할 포트 |
| Protocol | UDP | Unity Transport 설정에 따름 |

### 코인 게임: 변경할 위치

1. Hierarchy에서 `Canvas` 오브젝트를 선택한다.
2. Inspector의 `Network Connection UI` 컴포넌트에서 `Address`와 `Port`를 바꾼다.

Host·Client 버튼을 누르면 `NetworkConnectionUI`가 이 값으로 `UnityTransport.SetConnectionData()`를 호출하므로, `NetworkManager` 오브젝트의 `Unity Transport` 컴포넌트에 적힌 값은 실행 시 이 값으로 덮어쓰인다. Client 빌드에도 같은 값이 들어가므로, 값을 바꾼 뒤에는 Client를 다시 빌드한다.

### UDP 피어: 주소·포트

| 피어 | 로컬 주소·포트 | 상대 주소·포트 |
|---|---|---|
| A | 127.0.0.1:5000 | 127.0.0.1:5001 |
| B | 127.0.0.1:5001 | 127.0.0.1:5000 |

실행 시 입력한 Peer ID로 결정되며, `UdpPeer/Program.cs`의 `Main()` 안 `localPort`·`remotePort` 대입문과 `IPAddress.Loopback`에서 바꿀 수 있다. 실행 인자나 입력으로 바꾸는 기능은 없다 (7장 참고).

## 5. 이동·코인 획득·시험 기능 조작 방법

### 플레이어 이동

- Host 또는 Client로 접속한다.
- 본인에게 배정된 플레이어만 직접 조작할 수 있다.
- 기본 입력: `W`, `A`, `S`, `D` 또는 방향키
- 게임패드: 왼쪽 스틱 입력을 지원하도록 설정되어 있다.
- 상대 플레이어의 이동 결과가 다른 실행 화면에도 반영되는지 확인한다.

### 코인 획득

- 플레이어가 코인에 닿으면(수평 거리 1.0 이내) 클라이언트가 그 코인의 획득을 서버에 자동으로 요청한다. 코인마다 한 번만 요청한다. 별도의 획득 키는 없다.
- 서버가 요청을 검사해 승인하면 점수가 1 오르고, 해당 코인이 Host와 Client 양쪽에서 제거된다. 3초 뒤 새 코인이 생성된다.
- 획득 가능 거리는 코인과 플레이어의 수평 거리 1.5 이내다. 씬의 `NetworkCoinSpawner` 오브젝트에서 `Pickup Range` 값으로 바꿀 수 있다.
- 개인별 점수 방식을 사용하며, 최종 점수 변경은 서버에서 확정한다.
- 화면 오른쪽 점수판에 플레이어별 점수, 남은 코인 수(`Coins left`), 내 요청 결과 최근 5건(`My requests`)이 표시된다.

### 시험 기능

서버가 잘못된 요청을 거절하는지 확인하기 위한 시험용 키다. Player 프리팹의 `NetworkCoinCollector`에서 `Enable Test Input`을 끄면 비활성화된다.

| 키 | 보내는 요청 | 기대 결과 (화면 `My requests`) |
|---|---|---|
| `F1` | 존재하지 않는 코인 ID 999999 | `InvalidCoinId` |
| `F2` | 가장 먼 코인 | `TooFar` |
| `F3` | 가장 가까운 코인을 한 프레임에 두 번 | 첫 번째 `Accepted`, 두 번째 `AlreadyCollected`, 점수는 1만 증가. 닿기 직전 거리(1.0~1.5)에서 누른다. 1.0 안으로 들어가면 접촉 요청이 먼저 처리된다 |
| `F4` | 마지막으로 요청한 코인 ID를 다시 | 이미 획득한 코인이면 `AlreadyCollected` |

Host와 Client가 같은 코인에 거의 동시에 닿아도 서버는 요청을 하나씩 처리하므로 먼저 처리된 요청만 `Accepted`가 된다.

서버(Host) Console에는 요청마다 `[CoinRequest] client=<id> coin=<id> result=<결과> score=<점수>` 로그가 남는다. 요청한 쪽 Console에는 `[CoinResult] coin=<id> result=<결과>`가 남는다.


## 6. 정상 실행 판단 기준

### 화면에서 확인할 내용

- Host 시작 후 접속 상태가 표시된다.
- Client 접속 후 양쪽 실행 화면에 플레이어가 생성된다.
- Host와 Client의 플레이어를 각각 조작할 수 있다.
- 한쪽 플레이어의 이동이 다른 쪽 화면에도 반영된다.
- 코인 획득 후 점수가 올바르게 표시된다.
- 획득한 코인이 양쪽 화면에서 제거된다.
- Client 연결이 종료되어도 Host는 계속 실행된다.
- Client 재접속 후 현재 남은 코인 상태가 동기화된다.

### Console에서 확인할 내용

- Host 및 Client 연결 관련 로그
- 서버의 코인 획득 판정 로그
- 점수 변경 로그
- 잘못된 요청의 거절 로그
- 연결 종료 및 재접속 로그

정상 실행 여부는 화면 표시만으로 판단하지 않는다. 서버 로그와 Host·Client의 상태가 일치하는지도 함께 확인한다.

## 7. 알려진 오류·미완성 기능·환경 제한

### 미완성 또는 추가 검증이 필요한 기능

- 반복적인 연결 및 해제 과정에서 네트워크 오브젝트가 중복 생성되거나 남지 않는지 확인하지 않았다.
- UDP 피어의 주소·포트는 Peer ID에 따라 코드에 고정되어 있다. 안내서의 "로컬·상대 주소와 포트를 설정할 수 있어야 한다"는 조건은 코드 수정으로만 충족한다.

### 재접속 정책

- Client가 종료되면 해당 플레이어 오브젝트는 제거되어야 한다.
- Host는 Client 종료 후에도 계속 실행되어야 한다.
- 재접속한 Client는 새 플레이어로 생성되며 개인 점수는 0점부터 시작한다.
- 서버가 실행 중인 동안 유지되는 코인 상태는 재접속한 Client에도 동기화되어야 한다.
- Host까지 종료되면 메모리에 저장된 점수와 코인 상태는 유지되지 않는다. 영구 저장은 구현 범위에 포함하지 않는다.

### 환경 제한

- `127.0.0.1`은 같은 컴퓨터 내부에서만 사용하는 주소다.
- 다른 컴퓨터에서 접속할 때는 Host 컴퓨터의 LAN IP 주소를 사용해야 한다.
- Unity Editor와 빌드 실행 파일은 성능 및 입력 처리 환경이 다를 수 있으므로 양쪽에서 테스트한다.
- 같은 컴퓨터에서 Host(에디터)와 Client(빌드) 창을 함께 띄우면 한 창은 항상 포커스를 잃는다. 포커스를 잃은 창도 계속 실행되도록 Player Settings → Resolution and Presentation의 Run In Background를 켰다.
- Input System 설정(`Assets/Settings/InputSystemSettings.asset`, Edit → Project Settings → Input System Package)은 Play Mode Input Behavior = `All Device Input Always Goes To Game View`, Background Behavior = `Ignore Focus`다. 에디터의 Game 창 포커스와 관계없이 게임이 키 입력을 받는다. 대신 플레이 중 Inspector 등 에디터의 다른 창에 입력해도 게임에 키 입력이 들어간다.

### 알려진 오류

- **에디터 첫 플레이에서 키보드 입력 무반응 (원인 미확정):** 프로젝트를 연 직후 첫 플레이에서 WASD를 포함한 모든 키가 반응하지 않는 현상이 여러 번 있었다. 마우스 클릭은 동작했고 빌드 실행 파일에서는 정상이었다. 입력 설정 변경으로는 해결되지 않았고, 진단 결과 Input System이 처리한 입력 이벤트 수가 0이었다. 프로젝트 폴더를 지우고 저장소에서 다시 받아 `Library`·`UserSettings`를 새로 만든 뒤에는 정상 동작했다. 같은 현상이 생기면 Unity를 닫고 `CoinGame/Library`와 `CoinGame/UserSettings` 폴더를 지운 뒤 다시 연다 (두 폴더는 제출물에 포함되지 않으며 Unity가 다시 만든다). 경과는 `TEST_REPORT.md` 1장에 기록했다.

## 8. 구현 범위

이 프로젝트는 다음 항목을 학습하고 검증하는 것을 목표로 한다.

- Host 및 Client 접속
- 플레이어 생성과 네트워크 이동 동기화
- 서버 권한 기반 코인 획득 판정
- 개인별 점수 및 코인 상태 동기화
- 잘못된 요청과 중복 보상 방지
- 연결 종료 및 재접속 처리

승리·패배 판정, 목표 점수 기반 라운드 종료, 서버 종료 후 데이터 영구 저장 기능은 현재 과제 범위에서 제외한다.
