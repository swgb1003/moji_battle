# P1 一 vs 鬱 seed sweep (Gothic, provisional glyph masks)

- matches: 20 / 一 wins 7 / 鬱 wins 13 / draws 0 (一 win rate 35%)
- finishes: KO 6 / time up 14
- duplicate body hits: 0 / env damage events 12 (outside launch window 0)
- longest gap with no attack started by either side (stalemate metric, max over matches): 6.1s
- longest no-damage gap (max over matches): 31.9s
- knockdown recoveries: 11 (settle→stand 1.04–1.86s, timeouts 2, safe shifts 2)
- script cost per fixed step: avg 0.082 ms / max 4.19 ms

| metric (avg per match) | 一 (left) | 鬱 (right) |
|---|---|---|
| attacks started | 15.8 | 11.7 |
| body hits landed | 8.5 | 1.4 |
| damage dealt by hits | 50.6 | 51.6 |
| successful guards | 0.2 | 0.5 |
| evades | 4.3 | 0.5 |
| max combo hits | 7.0 | 1.0 |
| avg windup (s) | 0.21 | 1.20 |
| avg hit impulse | 2.91 | 12.74 |
| guard time fraction | 0.6% | 2.0% |
| guard entries per attack sequence | 0.04 | 0.15 |
| attacks per sequence (combo length) | 1.48 | 1.00 |
| jammed swings (moved < 40% of planned arc) | 50.7% | 0.5% |
| approach/hold time fraction | 72.4% | 46.8% |
| attack time fraction | 15.3% | 44.8% |

| seed | result | time | HP 一 | HP 鬱 | hits 一/鬱 | guards 一/鬱 | evades 一/鬱 | env | no-dmg gap | idle gap |
|---|---|---|---|---|---|---|---|---|---|---|
| 1037 | 一 TIME_UP | 60.0 | 94 | 128 | 7/0 | 0/0 | 3/0 | 0 | 23.9 | 5.1 |
| 1074 | 一 TIME_UP | 60.0 | 94 | 98 | 10/0 | 0/0 | 8/1 | 0 | 20.5 | 5.1 |
| 1111 | 一 TIME_UP | 60.0 | 94 | 109 | 11/0 | 0/1 | 3/0 | 0 | 15.8 | 5.9 |
| 1148 | 鬱 TIME_UP | 60.0 | 29 | 145 | 5/2 | 0/0 | 2/1 | 0 | 18.8 | 6.1 |
| 1185 | 鬱 KO | 50.8 | 0 | 139 | 6/4 | 0/1 | 2/0 | 1 | 17.9 | 6.1 |
| 1222 | 鬱 KO | 37.3 | 0 | 139 | 6/2 | 0/0 | 4/0 | 1 | 10.8 | 5.8 |
| 1259 | 一 TIME_UP | 60.0 | 94 | 103 | 10/0 | 0/0 | 9/1 | 0 | 14.2 | 6.1 |
| 1296 | 一 TIME_UP | 60.0 | 94 | 136 | 7/0 | 0/1 | 4/1 | 0 | 31.9 | 6.1 |
| 1333 | 鬱 TIME_UP | 60.0 | 5 | 128 | 8/2 | 0/0 | 2/0 | 1 | 28.5 | 6.1 |
| 1370 | 鬱 KO | 58.0 | 0 | 145 | 5/2 | 1/0 | 4/0 | 1 | 30.4 | 6.1 |
| 1407 | 鬱 KO | 53.4 | 0 | 127 | 7/2 | 0/0 | 7/1 | 1 | 22.2 | 5.6 |
| 1444 | 一 TIME_UP | 60.0 | 94 | 93 | 13/0 | 0/0 | 2/0 | 0 | 10.9 | 4.6 |
| 1481 | 鬱 TIME_UP | 60.0 | 43 | 132 | 8/1 | 0/2 | 5/0 | 1 | 23.7 | 5.0 |
| 1518 | 鬱 KO | 53.7 | 0 | 99 | 10/2 | 1/0 | 8/0 | 2 | 16.0 | 5.1 |
| 1555 | 鬱 KO | 57.1 | 0 | 123 | 8/4 | 0/0 | 4/1 | 0 | 20.7 | 5.6 |
| 1592 | 鬱 TIME_UP | 60.0 | 8 | 120 | 11/2 | 0/1 | 3/1 | 1 | 13.9 | 5.5 |
| 1629 | 鬱 TIME_UP | 60.0 | 53 | 121 | 8/2 | 0/1 | 4/0 | 1 | 16.8 | 5.8 |
| 1666 | 一 TIME_UP | 60.0 | 94 | 94 | 11/0 | 1/0 | 4/1 | 0 | 22.7 | 5.4 |
| 1703 | 鬱 TIME_UP | 60.0 | 18 | 124 | 9/2 | 1/1 | 2/1 | 1 | 19.7 | 6.1 |
| 1740 | 鬱 TIME_UP | 60.0 | 45 | 126 | 9/1 | 0/1 | 6/0 | 1 | 13.9 | 6.1 |
