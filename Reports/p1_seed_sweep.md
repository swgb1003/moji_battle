# P1 一 vs 鬱 seed sweep (Gothic, provisional glyph masks)

- matches: 20 / 一 wins 5 / 鬱 wins 15 / draws 0 (一 win rate 25%)
- finishes: KO 18 / time up 2
- duplicate body hits: 0 / env damage events 17 (outside launch window 0)
- longest gap with no attack started by either side (stalemate metric, max over matches): 6.4s
- longest no-damage gap (max over matches): 20.2s
- knockdown recoveries: 11 (settle→stand 1.20–1.88s, timeouts 3, safe shifts 1)
- script cost per fixed step: avg 0.154 ms / max 2.77 ms

| metric (avg per match) | 一 (left) | 鬱 (right) |
|---|---|---|
| attacks started | 13.4 | 7.0 |
| body hits landed | 7.5 | 2.6 |
| damage dealt by hits | 85.8 | 70.5 |
| successful guards | 0.5 | 0.9 |
| evades | 4.2 | 1.8 |
| max combo hits | 4.7 | 1.4 |
| avg windup (s) | 0.29 | 1.28 |
| avg hit impulse | 2.26 | 5.59 |
| guard time fraction | 1.6% | 6.1% |
| guard entries per attack sequence | 0.08 | 0.49 |
| attacks per sequence (combo length) | 1.61 | 1.00 |
| jammed swings (moved < 40% of planned arc) | 26.4% | 0.8% |
| approach/hold time fraction | 62.3% | 41.5% |
| attack time fraction | 29.2% | 46.2% |

| seed | result | time | HP 一 | HP 鬱 | hits 一/鬱 | guards 一/鬱 | evades 一/鬱 | env | no-dmg gap | idle gap |
|---|---|---|---|---|---|---|---|---|---|---|
| 1037 | 鬱 KO | 40.6 | 0 | 52 | 7/4 | 0/3 | 6/0 | 1 | 9.7 | 5.8 |
| 1074 | 鬱 KO | 20.0 | 0 | 73 | 4/3 | 0/0 | 2/1 | 0 | 6.7 | 4.6 |
| 1111 | 鬱 KO | 37.2 | 0 | 28 | 6/3 | 0/2 | 4/0 | 1 | 10.3 | 4.0 |
| 1148 | 鬱 KO | 43.8 | 0 | 52 | 7/2 | 1/1 | 4/3 | 1 | 12.3 | 5.6 |
| 1185 | 鬱 TIME_UP | 60.0 | 1 | 22 | 10/3 | 0/1 | 8/0 | 2 | 13.8 | 6.2 |
| 1222 | 鬱 KO | 41.7 | 0 | 6 | 10/4 | 0/0 | 2/2 | 0 | 12.0 | 5.1 |
| 1259 | 一 TIME_UP | 60.0 | 11 | 1 | 10/2 | 2/0 | 6/6 | 1 | 12.0 | 6.4 |
| 1296 | 一 KO | 27.9 | 52 | 0 | 8/1 | 1/1 | 2/1 | 0 | 6.2 | 4.7 |
| 1333 | 一 KO | 37.0 | 52 | 0 | 11/1 | 1/0 | 5/3 | 1 | 12.6 | 4.3 |
| 1370 | 鬱 KO | 58.0 | 0 | 8 | 9/2 | 1/1 | 5/1 | 3 | 16.3 | 5.1 |
| 1407 | 鬱 KO | 42.8 | 0 | 13 | 12/4 | 1/0 | 5/3 | 1 | 12.6 | 5.2 |
| 1444 | 一 KO | 39.4 | 73 | 0 | 9/0 | 0/1 | 3/1 | 1 | 13.7 | 6.0 |
| 1481 | 鬱 KO | 11.1 | 0 | 118 | 0/2 | 0/0 | 0/0 | 0 | 6.9 | 4.3 |
| 1518 | 鬱 KO | 55.6 | 0 | 44 | 9/4 | 0/0 | 6/2 | 2 | 10.5 | 6.1 |
| 1555 | 一 KO | 59.5 | 29 | 0 | 12/1 | 1/1 | 5/6 | 1 | 17.8 | 6.1 |
| 1592 | 鬱 KO | 48.4 | 0 | 23 | 7/3 | 0/1 | 7/2 | 0 | 20.2 | 5.9 |
| 1629 | 鬱 KO | 53.7 | 0 | 3 | 8/4 | 2/2 | 7/2 | 0 | 11.9 | 6.1 |
| 1666 | 鬱 KO | 18.1 | 0 | 49 | 4/3 | 0/1 | 2/0 | 0 | 6.1 | 4.7 |
| 1703 | 鬱 KO | 14.3 | 0 | 76 | 4/2 | 0/1 | 2/1 | 1 | 5.2 | 3.4 |
| 1740 | 鬱 KO | 28.3 | 0 | 63 | 3/3 | 0/1 | 3/1 | 1 | 9.9 | 6.1 |
