# P1 一 vs 鬱 seed sweep (Gothic, provisional glyph masks)

- matches: 20 / 一 wins 6 / 鬱 wins 14 / draws 0 (一 win rate 30%)
- finishes: KO 19 / time up 1
- duplicate body hits: 0 / env damage events 4 (outside launch window 0)
- longest gap with no attack started by either side (stalemate metric, max over matches): 6.7s
- longest no-damage gap (max over matches): 27.5s
- knockdown recoveries: 3 (settle→stand 1.40–1.46s, timeouts 1, safe shifts 0)
- script cost per fixed step: avg 0.196 ms / max 25.68 ms

| metric (avg per match) | 一 (left) | 鬱 (right) |
|---|---|---|
| attacks started | 12.8 | 5.7 |
| body hits landed | 5.5 | 2.1 |
| damage dealt by hits | 69.1 | 80.2 |
| successful guards | 0.4 | 1.1 |
| evades | 3.9 | 1.6 |
| max combo hits | 3.3 | 1.3 |
| avg windup (s) | 0.24 | 1.27 |
| avg hit impulse | 2.11 | 6.60 |
| guard time fraction | 1.5% | 7.3% |
| guard entries per attack sequence | 0.10 | 0.47 |
| attacks per sequence (combo length) | 1.77 | 1.02 |
| jammed swings (moved < 40% of planned arc) | 35.9% | 3.3% |
| approach/hold time fraction | 65.7% | 40.8% |
| attack time fraction | 24.9% | 48.3% |

| seed | result | time | HP 一 | HP 鬱 | hits 一/鬱 | guards 一/鬱 | evades 一/鬱 | env | no-dmg gap | idle gap |
|---|---|---|---|---|---|---|---|---|---|---|
| 1037 | 一 TIME_UP | 60.0 | 48 | 3 | 8/1 | 0/2 | 9/4 | 0 | 16.3 | 5.4 |
| 1074 | 鬱 KO | 59.7 | 0 | 79 | 1/3 | 1/1 | 9/7 | 0 | 27.5 | 6.7 |
| 1111 | 鬱 KO | 24.8 | 0 | 19 | 5/4 | 0/0 | 2/2 | 0 | 6.1 | 4.5 |
| 1148 | 鬱 KO | 36.4 | 0 | 57 | 3/2 | 0/1 | 5/1 | 0 | 15.9 | 6.2 |
| 1185 | 鬱 KO | 13.8 | 0 | 69 | 3/2 | 0/0 | 1/0 | 0 | 8.3 | 4.5 |
| 1222 | 鬱 KO | 19.9 | 0 | 58 | 3/2 | 1/0 | 3/0 | 0 | 10.1 | 5.5 |
| 1259 | 鬱 KO | 32.6 | 0 | 47 | 3/3 | 0/1 | 4/1 | 0 | 16.9 | 5.3 |
| 1296 | 一 KO | 47.5 | 56 | 0 | 10/1 | 1/5 | 2/2 | 1 | 8.2 | 6.1 |
| 1333 | 鬱 KO | 41.1 | 0 | 7 | 9/2 | 0/1 | 5/1 | 0 | 13.5 | 6.1 |
| 1370 | 一 KO | 48.7 | 20 | 0 | 7/3 | 0/1 | 4/3 | 1 | 16.1 | 5.0 |
| 1407 | 鬱 KO | 24.1 | 0 | 6 | 9/2 | 1/1 | 2/1 | 0 | 7.2 | 3.5 |
| 1444 | 鬱 KO | 27.9 | 0 | 44 | 4/2 | 1/0 | 4/1 | 0 | 13.3 | 3.8 |
| 1481 | 鬱 KO | 14.3 | 0 | 103 | 1/1 | 0/2 | 4/0 | 0 | 12.8 | 5.9 |
| 1518 | 一 KO | 50.4 | 36 | 0 | 10/2 | 0/1 | 5/3 | 0 | 9.9 | 5.4 |
| 1555 | 鬱 KO | 7.9 | 0 | 106 | 0/1 | 0/0 | 1/0 | 0 | 7.9 | 3.8 |
| 1592 | 鬱 KO | 36.0 | 0 | 76 | 5/3 | 1/2 | 4/0 | 0 | 16.1 | 5.3 |
| 1629 | 鬱 KO | 21.3 | 0 | 35 | 6/2 | 0/0 | 2/1 | 0 | 8.0 | 3.9 |
| 1666 | 一 KO | 34.2 | 22 | 0 | 5/2 | 0/0 | 5/0 | 2 | 14.4 | 5.0 |
| 1703 | 一 KO | 16.7 | 48 | 0 | 8/1 | 1/1 | 1/0 | 0 | 7.5 | 3.4 |
| 1740 | 鬱 KO | 43.7 | 0 | 18 | 9/3 | 0/2 | 5/4 | 0 | 10.3 | 4.2 |
