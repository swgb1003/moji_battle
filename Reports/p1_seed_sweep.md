# P1 一 vs 鬱 seed sweep (Gothic, provisional glyph masks)

- matches: 20 / 一 wins 9 / 鬱 wins 11 / draws 0 (一 win rate 45%)
- finishes: KO 5 / time up 15
- duplicate body hits: 0 / env damage events 27 (outside launch window 0)
- longest gap with no attack started by either side (stalemate metric, max over matches): 6.1s
- longest no-damage gap (max over matches): 38.1s
- knockdown recoveries: 10 (settle→stand 1.22–2.00s, timeouts 0, safe shifts 0)
- script cost per fixed step: avg 0.084 ms / max 3.80 ms

| metric (avg per match) | 一 (left) | 鬱 (right) |
|---|---|---|
| attacks started | 15.7 | 10.0 |
| body hits landed | 8.2 | 1.3 |
| damage dealt by hits | 52.1 | 45.8 |
| successful guards | 0.3 | 0.7 |
| evades | 3.7 | 0.7 |
| max combo hits | 6.9 | 0.9 |
| avg windup (s) | 0.21 | 1.20 |
| avg hit impulse | 2.95 | 12.66 |
| guard time fraction | 1.2% | 3.0% |
| guard entries per attack sequence | 0.07 | 0.22 |
| attacks per sequence (combo length) | 1.50 | 1.00 |
| jammed swings (moved < 40% of planned arc) | 59.1% | 2.0% |
| approach/hold time fraction | 72.1% | 48.4% |
| attack time fraction | 15.9% | 42.5% |

| seed | result | time | HP 一 | HP 鬱 | hits 一/鬱 | guards 一/鬱 | evades 一/鬱 | env | no-dmg gap | idle gap |
|---|---|---|---|---|---|---|---|---|---|---|
| 1037 | 鬱 KO | 36.1 | 0 | 113 | 5/3 | 0/0 | 1/0 | 1 | 11.9 | 5.9 |
| 1074 | 一 TIME_UP | 60.0 | 98 | 84 | 11/0 | 0/1 | 6/1 | 0 | 13.6 | 5.5 |
| 1111 | 鬱 TIME_UP | 60.0 | 18 | 86 | 8/2 | 1/1 | 4/0 | 2 | 18.2 | 5.3 |
| 1148 | 一 TIME_UP | 60.0 | 98 | 86 | 11/0 | 0/0 | 4/0 | 0 | 20.4 | 6.1 |
| 1185 | 一 TIME_UP | 60.0 | 54 | 73 | 11/1 | 0/1 | 6/0 | 1 | 11.3 | 6.1 |
| 1222 | 鬱 KO | 50.9 | 0 | 97 | 8/3 | 0/2 | 5/1 | 1 | 12.5 | 6.1 |
| 1259 | 鬱 TIME_UP | 60.0 | 46 | 115 | 4/1 | 0/0 | 4/2 | 3 | 25.0 | 6.1 |
| 1296 | 鬱 TIME_UP | 60.0 | 49 | 112 | 5/1 | 1/0 | 1/3 | 1 | 38.1 | 5.9 |
| 1333 | 鬱 TIME_UP | 60.0 | 43 | 90 | 9/1 | 1/1 | 3/1 | 1 | 19.2 | 6.1 |
| 1370 | 一 TIME_UP | 60.0 | 81 | 91 | 8/1 | 0/1 | 3/0 | 1 | 17.4 | 6.1 |
| 1407 | 鬱 KO | 33.0 | 0 | 118 | 5/2 | 0/0 | 2/0 | 1 | 10.9 | 6.1 |
| 1444 | 一 TIME_UP | 60.0 | 56 | 82 | 9/1 | 0/0 | 7/0 | 2 | 13.2 | 6.1 |
| 1481 | 一 TIME_UP | 60.0 | 98 | 94 | 9/0 | 1/0 | 4/0 | 2 | 12.4 | 5.1 |
| 1518 | 一 TIME_UP | 60.0 | 83 | 41 | 13/1 | 0/0 | 3/1 | 0 | 14.6 | 6.1 |
| 1555 | 鬱 TIME_UP | 60.0 | 49 | 78 | 11/2 | 1/1 | 4/0 | 0 | 22.3 | 6.1 |
| 1592 | 一 TIME_UP | 60.0 | 80 | 56 | 13/0 | 0/1 | 4/1 | 2 | 10.2 | 4.3 |
| 1629 | 一 TIME_UP | 60.0 | 98 | 78 | 10/0 | 0/1 | 4/1 | 1 | 15.0 | 5.3 |
| 1666 | 鬱 KO | 34.6 | 0 | 137 | 1/2 | 1/1 | 3/0 | 2 | 18.9 | 6.1 |
| 1703 | 鬱 TIME_UP | 60.0 | 35 | 80 | 9/2 | 0/2 | 2/0 | 3 | 16.9 | 6.1 |
| 1740 | 鬱 KO | 40.3 | 0 | 130 | 3/2 | 0/1 | 3/2 | 3 | 14.8 | 5.6 |
