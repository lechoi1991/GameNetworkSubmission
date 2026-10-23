# TEST_REPORT.md

실제로 실행·관찰한 내용만 기록합니다. 시험 항목은 `DESIGN.md` 2.8·3.9, 조작 방법은 `README.md` 5장을 기준으로 합니다. 증거 파일은 `Evidence/` 폴더에 두고 파일명을 함께 적습니다.

## 시험 환경

| 항목 | 값 |
| --- | --- |
| Unity Editor | 6000.6.6f1 |
| Netcode for GameObjects | 2.13.3 |
| Unity Transport | 6.6.0 |
| .NET SDK | 10.0.402 |
| NGO 접속 주소·포트 | 127.0.0.1:7777 |
| UDP 피어 | 같은 컴퓨터에서 `UdpPeer` 두 개 실행. A: 127.0.0.1:5000, B: 127.0.0.1:5001 (실행 시 입력한 Peer ID로 결정) |

## 1. NGO 코인 게임

화면 캡처(`Evidence/NGO/`)는 같은 컴퓨터에서 빌드 실행 파일(왼쪽)과 에디터(오른쪽)를 함께 띄워 찍었습니다. `NGO_01`~`03`은 빌드가 Host, 에디터가 Client이고, `NGO_04`~`05`는 에디터가 Host, 빌드가 Client입니다. 각 화면 위쪽의 상태 문구(`Host: Online` / `Client: Connected to Host`)로 구분할 수 있습니다.

| 시험 대상 | 실행 조건·재현 순서 | 기대 결과 | 실제 결과·측정값 | 수정·조정 내용 | 다시 확인한 결과 |
| --- | --- | --- | --- | --- | --- |
| 접속 | Host 시작 → Client가 127.0.0.1:7777로 접속 | 양쪽에 플레이어 2명 생성, 접속 상태 표시 | Host·Client 양쪽에 플레이어 2명 생성. Host 화면 `Host: Online \| Players: 2`, Client 화면 `Client: Connected to Host` (캡처: `Evidence/NGO/NGO_01_connect.png`) | 없음 (기대대로 동작) | 해당 없음 (수정 없음) |
| 본인 플레이어 조작 (에디터 첫 플레이) | Unity 에디터를 새로 열고 스크립트 수정 없이 Play → Start Host → WASD·방향키 입력 | Host 플레이어가 입력 방향으로 이동 | 수정 전: 플레이어가 움직이지 않음. 스크립트를 수정해 다시 컴파일하거나 빌드한 뒤 플레이하면 움직임 | 원인 추정: Enter Play Mode 설정이 Domain Reload를 끈 상태여서, 첫 플레이가 에디터 시작 시점의 Input System 상태를 그대로 사용했습니다. `Awake()`에서 미리 만든 이동 `InputAction`이 이 영향을 받았습니다. 수정 (커밋 `2d4e068`): ① `NetworkPlayerMovement`가 `InputAction`을 소유자 플레이어의 `OnNetworkSpawn`에서 만들고 디스폰·파괴 시 정리 ② `EditorSettings.asset`에서 Enter Play Mode 옵션을 끄고 Play마다 Domain Reload 수행 | 수정 후 에디터를 새로 열고 첫 플레이에서 Start Host 후 WASD·방향키로 이동 확인 |
| 본인 플레이어 조작 (에디터 첫 플레이, 재발) | 위와 같음 | Host 플레이어가 입력 방향으로 이동 | 위 수정 이후 같은 증상 재발 | 키보드를 직접 읽는 `E`·`F1`~`F4`와 같은 방식으로 통일: 이동 입력을 `InputAction` 대신 `Keyboard.current`(WASD·방향키)와 `Gamepad.current`(왼쪽 스틱)에서 직접 읽도록 변경 | 수정 후 에디터를 새로 열고 첫 플레이에서 Start Host 후 이동 확인 |
| 본인 플레이어 조작 (에디터 첫 플레이, 2차 재발) | 위와 같음 | Host 플레이어가 입력 방향으로 이동 | 에디터를 새로 열고 플레이하자 다시 움직이지 않음. WASD·`E`·`F1` 모두 반응과 로그가 없고 경고·오류도 없음. 마우스로 Start Host 클릭은 동작. Game 창 클릭, 한/영 전환 후에도 같음. Input Debugger에서는 Keyboard가 정상이고 W 입력 값도 바뀜. 빌드 실행 파일에서는 정상 | 원인 추정: 키보드 장치는 입력을 받지만, 에디터가 Game 창에 포커스가 없다고 판단해 키보드 입력을 게임이 아닌 에디터 쪽으로만 보낸 것으로 봅니다. 기본 설정(Pointers And Keyboards Respect Game View Focus)의 동작입니다. 수정: `Assets/Settings/InputSystemSettings.asset`을 만들고 Play Mode Input Behavior를 `All Device Input Always Goes To Game View`로 설정 (에디터 전용, 빌드에는 영향 없음, 커밋 `f34ea99`) | 수정을 받은 다른 PC에서 에디터를 새로 열고 첫 플레이에서 Start Host 후 이동 확인 |
| 본인 플레이어 조작 (에디터 첫 플레이, 3차 재발) | 위와 같음 | Host 플레이어가 입력 방향으로 이동 | 2차 수정 후 한 번은 동작했으나, 다시 에디터를 새로 열고 플레이하자 같은 증상 재발 | 원인 추정 (Input System 1.20.0 패키지 소스 `InputManager.cs` 기준. 아래 4차 진단으로 이 경로가 원인이 아님이 드러남): Play Mode Input Behavior가 `All Device Input Always Goes To Game View`이고 Player Settings의 Run In Background가 꺼져 있으면, 에디터가 응용 프로그램 포커스를 잃었다고 판단하는 동안 입력 이벤트를 **버린다** (`SkipEventDueToEditorBehaviour`). 즉 2차 수정은 포커스 판정이 어긋난 상태에서는 효과가 없고, 포커스 판정 자체는 `Application.isFocused`와 포커스 이벤트로 갱신되므로 에디터를 연 직후 상태에 따라 달라질 수 있다. 마우스 클릭은 동작했다는 점은 이 경로만으로 설명되지 않아 진단 표시를 추가했다. 수정: ① Background Behavior를 `Ignore Focus`로 변경. 소스에서 `Ignore Focus` + `All Device Input Always Goes To Game View`일 때만 포커스와 무관하게 게임이 입력을 받는다 (`gameShouldGetInputRegardlessOfFocus`). ② Player Settings Run In Background 켬. ③ `InputDiagnosticsOverlay` 추가: 플레이어 생성 후 화면에 Input System 설정·포커스·키보드 장치 상태·초당 키보드 이벤트 수를 표시해, 재발 시 원인을 화면 값으로 확인 | 재발. 진단 표시 값: 설정 반영됨(`background=IgnoreFocus`, `editorPlayMode=AllDeviceInputAlwaysGoesToGameView`, `runInBackground=True`), `app.isFocused=True`, `keyboard=enabled`, 키를 누르는 동안 `W=False`, `anyKey=False`, `keyboard events/s=0`, `owner=True`, `server=True`. 마우스로 Start Host 클릭은 동작 |
| 본인 플레이어 조작 (에디터 첫 플레이, 4차) | 위와 같음 | Host 플레이어가 입력 방향으로 이동 | 위 진단 값 기준: 설정·포커스·장치 상태는 정상인데 키보드 이벤트가 Input System의 `onEvent`까지 한 건도 오지 않음. 마우스는 동작하므로 키보드에만 해당하는 원인 | 소스(`InputManager.cs`) 기준으로 위 설정에서는 포커스 관련 폐기 경로가 모두 비활성이므로, 남는 후보는 ① 네이티브 키보드 이벤트의 장치 ID가 관리 측 `Keyboard`와 맞지 않아 "device not found"로 버려지는 경우 ② 한글 IME가 키 입력을 조합 입력으로 가로채는 경우. 조치: ⓐ `NetworkPlayerMovement`가 소유자 생성 시 `Keyboard.SetIMEEnabled(false)` 호출 (②의 수정 후보) ⓑ 진단 표시에 `imeSelected`, 마우스·기타 이벤트 수, `metrics events/s`, `updates/s`, 장치 ID, Unity 버전 추가 ⓒ 재발 시 Input Debugger → 옵션 메뉴 → Enable Event Diagnostics를 켜면 ①일 때 Console에 `Cannot find device for input event` 오류가 남음 | 재발. 진단 값: `imeSelected=False`, `onEvent keyboard=0`, `metrics events/s=0`. `metrics`는 모든 장치의 이벤트 수이므로 키보드만이 아니라 Input System이 처리한 이벤트 자체가 0이었음. IME(②)는 원인이 아님 |
| 본인 플레이어 조작 (에디터 첫 플레이, 정리) | 프로젝트 폴더를 지우고 저장소에서 다시 받아 열기 (`Library`·`UserSettings` 새로 생성) → Play → Start Host → WASD | Host 플레이어가 입력 방향으로 이동 | 다시 받은 PC에서 정상 동작 | 원인 미확정. 위 1~4차 수정 중 포커스 관련 추정은 진단 값과 맞지 않았고, 다시 받은 뒤 해결된 점으로 보아 에디터가 만든 `Library`·`UserSettings` 쪽 상태로 추정. 제출 정리: 원인과 무관한 진단 표시(`InputDiagnosticsOverlay`)와 IME 호출은 제거. 동작에 영향이 없는 입력 설정(Ignore Focus, All Device Input Always Goes To Game View)과 Host·Client 동시 실행에 필요한 Run In Background는 유지. 재발 시 대처법은 `README.md` 7장 | 다시 받은 직후 이동 확인 (진단 표시 제거 후에는 Unity 배치 모드 컴파일만 확인) |
| 본인 플레이어 조작 (Host·Client) | 각 창에서 이동 입력 | 본인 플레이어만 움직이고 상대 화면에 이동 반영 | 각 창에서 자기 플레이어만 움직이고, 그 이동이 상대 창에도 반영됨 | 없음 (기대대로 동작) | 해당 없음 (수정 없음) |
| 코인 생성 | Host 시작 | 서버가 코인을 생성하고 모든 클라이언트에 표시 | Host 시작 후 코인 10개 생성, Client 화면에도 같은 위치에 표시 (캡처: `Evidence/NGO/NGO_01_connect.png`) | 없음 (기대대로 동작) | 해당 없음 (수정 없음) |
| 접촉 획득 | 플레이어를 코인 위로 이동 (키 입력 없음) | 자동 요청 후 `Accepted`, 점수 1 증가, 양쪽에서 코인 제거 | 커밋 `2ab86ca`에서 E 요청만 남기면서 코인에 닿아도 획득되지 않음 | 소유 클라이언트가 수평 거리 1.0 이내 코인에 대해 코인당 한 번 획득 요청을 자동 전송하도록 추가. 판정은 그대로 서버 `TryCollectCoin` | 수정 후 Host에서 코인 위를 지나가면 획득됨 |
| 잘못된 코인 ID | `F1` (ID 999999 요청) | `InvalidCoinId`, 점수 변화 없음 | `My requests`에 `Coin 999999: InvalidCoinId`, 점수 변화 없음 (캡처: `Evidence/NGO/NGO_03_reject.png`) | 없음 (기대대로 동작) | 해당 없음 (수정 없음) |
| 거리 초과 | 코인에서 떨어진 위치에서 `F2` (가장 먼 코인 요청) | `TooFar`, 점수 변화 없음, 코인 유지 | `My requests`에 `TooFar`, 점수 변화 없음, 요청한 코인 유지 (캡처: `Evidence/NGO/NGO_03_reject.png`) | 없음 (기대대로 동작) | 해당 없음 (수정 없음) |
| 이미 획득한 코인 | 정상 획득 직후 `F4` (같은 ID 재요청) | `AlreadyCollected`, 점수 변화 없음 | `My requests`에 `AlreadyCollected`, 점수 변화 없음 | 없음 (기대대로 동작) | 해당 없음 (수정 없음) |
| 중복 요청 | 코인에 닿기 직전 거리(1.0~1.5)에서 `F3` (같은 코인 두 번 요청) | `Accepted` 1건, `AlreadyCollected` 1건, 점수 1만 증가 | `Accepted` 1건, `AlreadyCollected` 1건, 점수 1만 증가 | 없음 (기대대로 동작) | 해당 없음 (수정 없음) |
| 동시 획득 | Host와 Client가 같은 코인에 양쪽에서 거의 동시에 닿도록 이동 | 한쪽만 `Accepted`, 다른 쪽 `AlreadyCollected`, 보상 한 번 | 한쪽만 `Accepted`, 다른 쪽 `AlreadyCollected`, 보상은 한 번만 지급 | 없음 (기대대로 동작) | 해당 없음 (수정 없음) |
| 개인 점수 분리·상태 일치 | 두 플레이어가 각각 코인 획득 | 플레이어별 점수와 남은 코인이 양쪽 화면에서 일치 | 플레이어별 점수와 `Coins left`가 Host·Client 화면에서 일치 (캡처: `Evidence/NGO/NGO_02_pickup.png`, `NGO_03_reject.png`, `NGO_04_console.png`) | 없음 (기대대로 동작) | 해당 없음 (수정 없음) |
| 종료·재접속 | Client 종료 → 재접속 | Host 계속 실행, 재접속 Client가 현재 점수·남은 코인 수신 | Client 종료 후 Host 계속 실행. 재접속한 Client에 현재 점수판과 남은 코인이 그대로 표시되고, 재접속 플레이어는 0점부터 시작. Host Console에 `Client disconnected: 1` → `Client connected: 2`가 남고, 재접속한 Client는 `Player 2: 0`으로 표시됨 (캡처: `Evidence/NGO/NGO_05_reconnect.png`) | 없음 (기대대로 동작) | 해당 없음 (수정 없음) |

## 2. UDP 피어 통신

2026-10-23 09:39에 같은 컴퓨터(Windows 10, .NET SDK 10.0.402)에서 실행한 결과입니다. 3장의 메시지 크기 조정을 적용한 뒤의 프로그램으로 다시 실행했습니다. 2026-10-22에 조정 전 프로그램으로 실행했을 때도 아래 기능 시험의 결과는 같았습니다. 다만 잘못된 메시지 중 "조정 전 형식"은 이번에 추가한 항목입니다.

- 실행 방법: `Evidence/UDP/udp_test_driver.py`가 `UdpPeer` 두 개를 띄우고 콘솔에 명령을 입력합니다. 각 피어의 콘솔 출력은 받은 시각과 함께 기록합니다.
- 실행 명령: `UdpPeer` 폴더에서 `dotnet build`를 실행한 뒤 `최종화_GameNetwork` 폴더에서 `python Evidence/UDP/udp_test_driver.py UdpPeer/bin/Debug/net10.0/UdpPeer.dll <출력 폴더>`
- 기록 파일:
  - `UDP_peerA_console.txt`, `UDP_peerB_console.txt`: 각 피어의 전체 콘솔 출력. `<입력>`은 입력한 명령, `#####`는 시험 구분선입니다.
  - `UDP_invalid_sender.txt`: 잘못된 메시지 시험에서 보낸 데이터 목록입니다.
- 손으로 시험할 때는 콘솔 두 개에서 `dotnet run`을 실행하고 각각 `A`, `B`를 입력한 뒤 같은 명령을 입력합니다.

| 시험 대상 | 실행 조건·재현 순서 | 기대 결과 | 실제 결과·측정값 | 수정·조정 내용 | 다시 확인한 결과 |
| --- | --- | --- | --- | --- | --- |
| 상대 미수신 표시 | 실행 직후 각 피어에서 `show` | 상대 위치를 미수신으로 표시 | A·B 모두 `Remote position: Not received yet` | 없음 (기대대로 동작) | 잘못된 메시지 12개를 받은 뒤에도 A는 미수신 상태 유지 |
| 양쪽 위치 변경·전송 | A `move 3 5` → B `move 8 2` | 각자 자신의 위치로 Seq=1 메시지 송신 | A `[Sent] Seq=1, Data={"p":"A","s":1,"x":3,"y":5}`, B `[Sent] Seq=1, Data={"p":"B","s":1,"x":8,"y":2}` | 없음 | 이후 A의 `move`마다 Seq가 2, 3, 4로 증가 |
| 상대 위치 표시 | 위 시험 후 양쪽 `show` | A에 B (8, 2), B에 A (3, 5) 표시, 자신의 위치 유지 | A: Remote B (8, 2) Seq=1. B: My (8, 2) / Remote A (3, 5) Seq=1 | 없음 | 아래 시험 내내 자신의 위치가 상대 위치로 덮어써지지 않음 |
| 송신 피어 식별 | 시험 송신기가 A에게 피어 ID `A`(자기 자신)·`C`로 보냄 | 상대(B)가 아닌 ID는 반영하지 않음 | 두 경우 모두 `[Ignored] Invalid message fields.` | 없음 | `show` 결과 미수신 상태 유지 |
| 잘못된 메시지 | B 실행 전에 127.0.0.1:5001에서 A로 11종 송신: 빈 데이터·공백·형식 오류·`null`·x 문자열·x int 범위 초과·ID 오류 2종·Seq 0·Seq -5·조정 전 형식(긴 필드 이름). 포트 6000에서 1건 추가 | 프로그램 종료 없이 무시 로그 출력, 위치 변화 없음 | Empty 2건, Invalid JSON 3건, Invalid fields 6건, Unexpected sender endpoint 1건. `stats`: 수신 12 / 반영 0 / Invalid 11 | 없음 | 이후 B와의 정상 통신과 전송량 시험까지 A가 계속 동작 |
| 오래된 순서 번호 | A `move 4 6`(Seq 2) → `move 7 7`(Seq 3) → `replay 2`, `replay 1`로 당시 번호와 좌표를 그대로 재전송 | 무시 로그 출력, B의 표시는 (7, 7) Seq=3 유지 | B `[Ignored stale/duplicate] Peer=A, Seq=2, Last applied=3`, Seq=1도 같은 메시지 | 없음 | B `show`: Remote A (7, 7) Seq=3 |
| 중복 순서 번호 | A `replay 3` (이미 반영된 Seq 3 재전송) | 무시 로그 출력, 위치 유지 | B `[Ignored stale/duplicate] Peer=A, Seq=3, Last applied=3` | 없음 | B `show`: Remote A (7, 7) Seq=3. B `stats`의 Stale/duplicate 3 |
| 새 번호 반영 | A `move 3 5` (Seq 1과 같은 좌표지만 새 메시지) | Seq=4로 반영 | B `[Updated] Peer=A, Seq=4, Position=(3, 5)` | 없음 | B `show`: Remote A (3, 5) Seq=4 |

### 전송량 조건 비교

방법: 수신 측 `benchstart N` → 송신 측 `bench N` → 약 2초 대기 → 수신 측 `benchstop` (`DESIGN.md` 3.8)

| 항목 | 100개 (A→B) | 10,000개 (A→B) | 10,000개 (B→A) |
| --- | ---: | ---: | ---: |
| 요청 메시지 수 | 100 | 10,000 | 10,000 |
| 송신 완료 호출 수 | 100 | 10,000 | 10,000 |
| 송신 오류 수 | 0 | 0 | 0 |
| 보낸 페이로드 바이트 (메시지당 평균) | 3,079 (30.79) | 366,989 (36.70) | 366,677 (36.67) |
| 수신 데이터그램 수 | 100 | 10,000 | 10,000 |
| 측정 종료 시점의 관측 누락 수 | 0 | 0 | 0 |
| 송신 소요 시간 | 2.69 ms | 152.04 ms | 191.50 ms |
| 수신 측 측정 구간 시간 | 2,324.11 ms | 2,320.02 ms | 2,315.88 ms |
| 측정 후 수신 측이 반영한 위치·번호 | B의 A: (99, -99) Seq=104 | B의 A: (9999, -9999) Seq=10104 | A의 B: (9999, -9999) Seq=10001 |

마지막 `stats` 누적값:

| 피어 | 송신 완료 | 송신 오류 | 수신 | 반영 | Invalid | Stale/duplicate |
| --- | ---: | ---: | ---: | ---: | ---: | ---: |
| A | 10,107 | 0 | 10,013 | 10,001 | 11 | 0 |
| B | 10,001 | 0 | 10,107 | 10,104 | 0 | 3 |

- **송수신 수:** A가 보낸 10,107개를 B가 모두 받았습니다. 그중 10,104개를 반영하고 3개는 replay 시험의 오래된·중복 번호라 무시했습니다.
- **반영 결과:** 각 전송량 시험 뒤 수신 측이 반영한 번호는 송신 측이 마지막으로 보낸 번호와 같았습니다 (예: A의 Seq 4 + 100 = 104).
- **조건 비교:** 100개는 2.69 ms, 10,000개는 152.04 ms(A→B)·191.50 ms(B→A)로, 메시지 수에 거의 비례해 늘었습니다. 이 실행에서는 세 조건 모두 누락이 없었습니다.
- **수신 측 측정 시간:** 측정 시작부터 `benchstop`까지 스크립트가 일부러 기다린 시간(약 2.3초)이 대부분입니다. 그래서 네트워크 지연이나 처리 시간으로 해석할 수 없습니다. 비교에는 송신 소요 시간과 수신 수만 사용합니다.
- **관측 누락 수의 의미:** `benchstop` 시점까지 수신 측이 세지 못한 메시지 수입니다. 그 자체로는 아직 도착하지 않은 것인지 버려진 것인지 구분할 수 없습니다. 3장 1회차의 조정 전 측정에서는 누락이 나왔는데, 그때는 수신 측이 마지막 번호(예: Seq=21000)를 이미 반영한 뒤였습니다. 마지막 메시지보다 먼저 보낸 메시지가 그 뒤에 도착할 가능성은 낮으므로, 그 누락은 실제로 버려진 것으로 봅니다. 수신 소켓 버퍼가 가득 찬 것으로 추정하지만 버퍼 상태를 직접 확인하지는 않았습니다.
- **제한 사항:** `bench`는 시험용 좌표 (i, -i)를 위치 메시지 규약 그대로 보냅니다. 그래서 측정 후 상대에게는 (9999, -9999)가 표시되지만, 송신 피어 자신의 `My position`은 바뀌지 않습니다 (B는 계속 (8, 2)). 이는 `bench`가 `myX`·`myY`를 갱신하지 않기 때문입니다.

## 3. 조정 전후 비교: UDP 위치 메시지 크기

| 항목 | 내용 |
| --- | --- |
| 조정 항목 | 위치 메시지의 JSON 필드 이름을 줄임. 조정 전 `{"PeerId":"A","Sequence":1,"X":3,"Y":5}` → 조정 후 `{"p":"A","s":1,"x":3,"y":5}`. 네 필드와 값, 순서 번호 규칙은 그대로 |
| 변경 위치 | `UdpPeer/Program.cs`의 `PositionMessage`에 `[JsonPropertyName]` 지정. 측정을 위해 `bench` 결과에 보낸 페이로드 바이트 수 출력을 추가 (조정 전후 모두 같은 측정 코드) |
| 실행 조건 | 같은 컴퓨터, 127.0.0.1, A(5000) → B(5001), `bench 10000`. 1,000개 예열 1회는 제외하고 5회 측정. 각 회차마다 수신 측 `benchstart` → 송신 `bench` → 2초 대기 → `benchstop` |
| 실행 방법 | `Evidence/UDP/udp_bench_compare.py <UdpPeer.dll> <출력 폴더> <이름>` |
| 순서 | 1회차: 조정 전 → 조정 후. 2회차: 순서 영향을 보기 위해 조정 전(별도 빌드) → 조정 후를 다시 측정. 2026-10-23 09:36~09:39 |
| 기록 파일 | `Evidence/UDP/size_compare/` (`*_summary.txt`가 회차별 요약, `*_peer_A/B.txt`가 전체 콘솔 출력) |

| 측정값 (5회 합계 또는 평균) | 1회차 조정 전 | 1회차 조정 후 | 2회차 조정 전 | 2회차 조정 후 |
| ---: | ---: | ---: | ---: | ---: |
| 메시지당 평균 페이로드 | 49.78 B | 37.78 B | 49.78 B | 37.78 B |
| 10,000개 페이로드 합계 (2~5회) | 497,779 B | 377,779 B | 497,779 B | 377,779 B |
| 송신 오류 | 0 | 0 | 0 | 0 |
| 수신 / 송신 (5회 합계) | 43,520 / 50,000 | 50,000 / 50,000 | 50,000 / 50,000 | 50,000 / 50,000 |
| 회차별 수신 수 | 10,000 · 9,450 · 8,134 · 5,936 · 10,000 | 모두 10,000 | 모두 10,000 | 모두 10,000 |
| 송신 소요 시간 평균 | 113.46 ms | 104.49 ms | 134.37 ms | 142.94 ms |

1회차 첫 측정은 예열 직후라 순서 번호 자릿수가 짧아 페이로드가 조금 작습니다 (조정 전 488,780 B, 조정 후 368,780 B). 그래서 합계 비교에는 2~5회 값을 썼습니다.

판단:

- **메시지 크기:** 모든 측정에서 메시지당 12바이트(24.1%) 줄었습니다. 10,000개 기준 120,000바이트입니다. IPv4·UDP 헤더 28바이트를 더한 데이터그램 크기로는 77.8 B → 65.8 B(15.4%) 감소입니다. 이 효과는 재현됩니다.
- **송신 시간:** 1회차에는 7.9% 줄었고 2회차에는 6.4% 늘었습니다. 같은 컴퓨터 안에서는 크기 차이보다 실행할 때마다의 변동이 더 커서, 송신 시간이 개선됐다고 판단할 수 없습니다.
- **수신 누락:** 1회차 조정 전에만 5회 중 3회 누락(합계 6,480개)이 나왔습니다. 하지만 2회차 조정 전에는 누락이 없었으므로, 누락이 사라진 것을 이 조정의 효과로 볼 수 없습니다. 측정 당시 컴퓨터 상태에 따른 변동으로 판단합니다.
- **결론:** 조정 후 형식을 유지합니다. 전송량은 확실히 줄었고 기능 시험 결과(2장)는 같습니다. 대신 콘솔 로그에서 필드 이름을 읽기가 조금 어려워졌고, 조정 전 형식의 메시지는 받지 않습니다 (2장 잘못된 메시지 시험의 "조정 전 형식" 항목).
