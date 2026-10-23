# Evidence

시험 로그와 화면 캡처를 보관하는 폴더입니다. 각 파일은 `TEST_REPORT.md`의 시험 항목에서 참조합니다.

| 경로 | 내용 | 관련 시험 |
| --- | --- | --- |
| `NGO/` | 코인 게임 Host·Client 화면 캡처 | `TEST_REPORT.md` 1장 |
| `UDP/UDP_peerA_console.txt`, `UDP/UDP_peerB_console.txt` | 피어 A·B의 전체 콘솔 출력 (위치 교환, 잘못된 메시지, 오래된·중복 번호, 전송량) | 2장 |
| `UDP/UDP_invalid_sender.txt` | 잘못된 메시지 시험에서 보낸 데이터 목록 | 2장 |
| `UDP/udp_test_driver.py` | 위 기능·전송량 시험을 자동으로 실행하는 스크립트 | 2장 |
| `UDP/size_compare/` | 메시지 크기 조정 전후 측정 로그. `*_summary.txt`가 회차별 요약 | 3장 |
| `UDP/udp_bench_compare.py` | 조정 전후 측정 스크립트 (`bench 10000` 5회) | 3장 |

스크립트는 Python 3로 실행합니다. 사용법은 각 스크립트 첫 부분과 `README.md` 3.4절에 있습니다.
