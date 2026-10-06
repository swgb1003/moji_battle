# P1 一 vs 鬱 seed sweep (Gothic, provisional glyph masks)

- matches: 20 / 一 wins 7 / 鬱 wins 13 / draws 0 (一 win rate 35%)
- finishes: KO 20 / time up 0
- duplicate body hits: 0 / env damage events 5 (outside launch window 0)
- longest gap with no attack started by either side (stalemate metric, max over matches): 6.1s
- longest no-damage gap (max over matches): 20.3s
- knockdown recoveries: 2 (settle→stand 1.30–1.30s, timeouts 1, safe shifts 0)
- script cost per fixed step: avg 0.156 ms / max 7.76 ms

| metric (avg per match) | 一 (left) | 鬱 (right) |
|---|---|---|
| attacks started | 13.0 | 5.1 |
| body hits landed | 4.9 | 1.8 |
| damage dealt by hits | 81.6 | 61.8 |
| successful guards | 0.5 | 0.6 |
| evades | 2.9 | 1.5 |
| max combo hits | 3.6 | 1.1 |
| avg windup (s) | 0.23 | 1.29 |
| avg hit impulse | 2.42 | 5.22 |
| guard time fraction | 2.7% | 8.3% |
| guard entries per attack sequence | 0.13 | 0.65 |
| attacks per sequence (combo length) | 1.69 | 1.00 |
| jammed swings (moved < 40% of planned arc) | 47.2% | 6.8% |
| approach/hold time fraction | 65.8% | 44.4% |
| attack time fraction | 24.8% | 43.6% |

| seed | result | time | HP 一 | HP 鬱 | hits 一/鬱 | guards 一/鬱 | evades 一/鬱 | env | no-dmg gap | idle gap |
|---|---|---|---|---|---|---|---|---|---|---|
| 1037 | 鬱 KO | 24.8 | 0 | 20 | 3/2 | 1/1 | 4/2 | 1 | 10.3 | 4.6 |
| 1074 | 鬱 KO | 21.6 | 0 | 31 | 6/1 | 0/0 | 3/0 | 0 | 10.7 | 4.2 |
| 1111 | 一 KO | 36.5 | 61 | 0 | 9/1 | 2/1 | 1/1 | 0 | 9.9 | 6.1 |
| 1148 | 鬱 KO | 41.0 | 0 | 22 | 6/3 | 1/1 | 3/1 | 0 | 11.6 | 5.6 |
| 1185 | 鬱 KO | 22.6 | 0 | 8 | 7/3 | 0/0 | 1/0 | 0 | 8.5 | 4.0 |
| 1222 | 鬱 KO | 17.3 | 0 | 90 | 1/2 | 1/0 | 0/1 | 1 | 8.3 | 5.1 |
| 1259 | 鬱 KO | 43.8 | 0 | 27 | 5/2 | 1/1 | 3/2 | 0 | 16.6 | 5.8 |
| 1296 | 鬱 KO | 39.5 | 0 | 68 | 2/3 | 0/0 | 3/2 | 0 | 15.3 | 4.8 |
| 1333 | 鬱 KO | 4.8 | 0 | 106 | 0/1 | 0/0 | 0/0 | 0 | 4.8 | 3.2 |
| 1370 | 一 KO | 33.2 | 35 | 0 | 6/1 | 1/0 | 3/3 | 0 | 14.7 | 5.0 |
| 1407 | 一 KO | 44.2 | 67 | 0 | 6/0 | 0/0 | 5/2 | 0 | 20.3 | 4.6 |
| 1444 | 一 KO | 48.7 | 38 | 0 | 6/1 | 0/1 | 7/1 | 2 | 12.6 | 5.0 |
| 1481 | 一 KO | 39.5 | 40 | 0 | 5/2 | 0/0 | 5/1 | 0 | 11.3 | 6.1 |
| 1518 | 一 KO | 19.0 | 42 | 0 | 6/1 | 0/0 | 1/2 | 0 | 7.4 | 4.0 |
| 1555 | 鬱 KO | 38.8 | 0 | 34 | 4/3 | 0/1 | 5/2 | 0 | 16.1 | 5.4 |
| 1592 | 鬱 KO | 38.0 | 0 | 27 | 5/2 | 0/0 | 3/4 | 1 | 9.8 | 5.9 |
| 1629 | 一 KO | 27.5 | 30 | 0 | 7/1 | 0/3 | 3/1 | 0 | 5.3 | 4.2 |
| 1666 | 鬱 KO | 25.0 | 0 | 73 | 2/2 | 1/0 | 2/2 | 0 | 9.0 | 3.9 |
| 1703 | 鬱 KO | 20.7 | 0 | 20 | 9/3 | 0/1 | 1/0 | 0 | 4.5 | 4.2 |
| 1740 | 鬱 KO | 32.6 | 0 | 28 | 4/2 | 1/2 | 4/3 | 0 | 10.0 | 5.1 |
