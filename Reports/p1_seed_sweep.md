# P1 一 vs 鬱 seed sweep (Gothic, provisional glyph masks)

- matches: 20 / 一 wins 1 / 鬱 wins 19 / draws 0 (一 win rate 5%)
- finishes: KO 3 / time up 17
- duplicate body hits: 0 / env damage events 17 (outside launch window 0)
- longest gap with no attack started by either side (stalemate metric, max over matches): 9.9s
- longest no-damage gap (max over matches): 26.8s
- knockdown recoveries: 17 (settle→stand 1.02–1.98s, timeouts 2, safe shifts 2)
- script cost per fixed step: avg 0.083 ms / max 8.49 ms

| metric (avg per match) | 一 (left) | 鬱 (right) |
|---|---|---|
| attacks started | 15.1 | 11.2 |
| body hits landed | 8.3 | 1.9 |
| damage dealt by hits | 47.7 | 61.9 |
| successful guards | 0.3 | 0.5 |
| evades | 5.3 | 0.4 |
| max combo hits | 5.7 | 1.0 |
| avg windup (s) | 0.21 | 1.20 |
| avg hit impulse | 2.88 | 10.69 |
| guard time fraction | 2.0% | 3.1% |
| guard entries per attack sequence | 0.11 | 0.23 |
| attacks per sequence (combo length) | 1.32 | 1.00 |
| jammed swings (moved < 40% of planned arc) | 41.8% | 0.0% |
| approach/hold time fraction | 71.9% | 48.8% |
| attack time fraction | 14.3% | 41.0% |

| seed | result | time | HP 一 | HP 鬱 | hits 一/鬱 | guards 一/鬱 | evades 一/鬱 | env | no-dmg gap | idle gap |
|---|---|---|---|---|---|---|---|---|---|---|
| 1037 | 鬱 TIME_UP | 60.0 | 12 | 117 | 13/4 | 0/0 | 5/1 | 0 | 15.9 | 6.1 |
| 1074 | 鬱 TIME_UP | 60.0 | 56 | 110 | 10/1 | 0/0 | 6/1 | 1 | 9.9 | 5.6 |
| 1111 | 鬱 TIME_UP | 60.0 | 52 | 108 | 8/2 | 0/1 | 2/0 | 3 | 19.1 | 5.2 |
| 1148 | 鬱 TIME_UP | 60.0 | 54 | 101 | 11/1 | 1/1 | 6/1 | 0 | 9.3 | 6.6 |
| 1185 | 鬱 TIME_UP | 60.0 | 43 | 123 | 7/1 | 0/1 | 3/0 | 1 | 10.7 | 5.4 |
| 1222 | 鬱 TIME_UP | 60.0 | 10 | 128 | 7/2 | 0/2 | 4/0 | 2 | 13.9 | 5.8 |
| 1259 | 鬱 TIME_UP | 60.0 | 62 | 143 | 7/2 | 1/1 | 8/0 | 0 | 9.2 | 5.6 |
| 1296 | 鬱 TIME_UP | 60.0 | 21 | 131 | 8/2 | 0/1 | 6/0 | 1 | 11.7 | 6.2 |
| 1333 | 鬱 KO | 38.0 | 0 | 160 | 3/2 | 0/0 | 5/0 | 1 | 11.6 | 4.8 |
| 1370 | 鬱 TIME_UP | 60.0 | 15 | 130 | 10/2 | 0/0 | 6/0 | 0 | 10.6 | 8.4 |
| 1407 | 鬱 TIME_UP | 60.0 | 9 | 148 | 5/3 | 0/0 | 6/0 | 2 | 11.8 | 9.9 |
| 1444 | 一 TIME_UP | 60.0 | 94 | 85 | 12/0 | 0/0 | 7/0 | 0 | 10.1 | 6.4 |
| 1481 | 鬱 TIME_UP | 60.0 | 45 | 125 | 8/1 | 0/0 | 4/0 | 1 | 14.9 | 6.9 |
| 1518 | 鬱 TIME_UP | 60.0 | 6 | 109 | 9/2 | 0/0 | 7/0 | 0 | 16.6 | 5.4 |
| 1555 | 鬱 TIME_UP | 60.0 | 35 | 144 | 7/2 | 1/0 | 4/0 | 1 | 26.8 | 8.3 |
| 1592 | 鬱 TIME_UP | 60.0 | 36 | 130 | 8/1 | 1/1 | 5/1 | 1 | 11.0 | 7.2 |
| 1629 | 鬱 KO | 49.4 | 0 | 117 | 10/3 | 0/1 | 5/0 | 1 | 11.7 | 8.0 |
| 1666 | 鬱 TIME_UP | 60.0 | 44 | 132 | 8/1 | 0/1 | 7/2 | 1 | 19.3 | 6.1 |
| 1703 | 鬱 KO | 47.7 | 0 | 133 | 6/3 | 1/0 | 4/1 | 1 | 14.9 | 6.4 |
| 1740 | 鬱 TIME_UP | 60.0 | 10 | 101 | 9/2 | 0/0 | 7/0 | 0 | 9.0 | 4.8 |
