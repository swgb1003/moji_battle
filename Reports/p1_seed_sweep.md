# P1 一 vs 鬱 seed sweep (Gothic, provisional glyph masks)

- matches: 20 / 一 wins 5 / 鬱 wins 15 / draws 0 (一 win rate 25%)
- finishes: KO 19 / time up 1
- duplicate body hits: 0 / env damage events 18 (outside launch window 0)
- longest gap with no attack started by either side (stalemate metric, max over matches): 6.7s
- longest no-damage gap (max over matches): 31.9s
- knockdown recoveries: 9 (settle→stand 1.30–2.00s, timeouts 1, safe shifts 2)
- script cost per fixed step: avg 0.160 ms / max 3.19 ms

| metric (avg per match) | 一 (left) | 鬱 (right) |
|---|---|---|
| attacks started | 14.3 | 7.2 |
| body hits landed | 7.0 | 2.5 |
| damage dealt by hits | 78.0 | 72.7 |
| successful guards | 0.6 | 0.9 |
| evades | 4.3 | 1.5 |
| max combo hits | 4.5 | 1.4 |
| avg windup (s) | 0.29 | 1.28 |
| avg hit impulse | 2.49 | 6.07 |
| guard time fraction | 1.6% | 6.1% |
| guard entries per attack sequence | 0.11 | 0.45 |
| attacks per sequence (combo length) | 1.66 | 1.00 |
| jammed swings (moved < 40% of planned arc) | 17.0% | 1.7% |
| approach/hold time fraction | 59.5% | 41.1% |
| attack time fraction | 30.0% | 47.5% |

| seed | result | time | HP 一 | HP 鬱 | hits 一/鬱 | guards 一/鬱 | evades 一/鬱 | env | no-dmg gap | idle gap |
|---|---|---|---|---|---|---|---|---|---|---|
| 1037 | 一 KO | 42.4 | 16 | 0 | 8/2 | 0/1 | 5/1 | 0 | 10.1 | 5.8 |
| 1074 | 鬱 KO | 53.8 | 0 | 31 | 8/4 | 1/0 | 5/2 | 0 | 17.5 | 6.1 |
| 1111 | 一 KO | 46.2 | 28 | 0 | 12/2 | 2/0 | 2/2 | 1 | 11.5 | 6.1 |
| 1148 | 鬱 KO | 58.2 | 0 | 85 | 3/3 | 0/3 | 5/3 | 1 | 23.5 | 5.9 |
| 1185 | 鬱 KO | 42.8 | 0 | 68 | 4/3 | 1/1 | 6/1 | 1 | 9.7 | 6.2 |
| 1222 | 鬱 KO | 19.2 | 0 | 73 | 6/3 | 1/0 | 0/0 | 2 | 7.3 | 4.3 |
| 1259 | 鬱 KO | 32.4 | 0 | 64 | 5/3 | 0/0 | 3/2 | 2 | 9.1 | 5.9 |
| 1296 | 一 KO | 28.7 | 73 | 0 | 10/0 | 0/0 | 1/1 | 2 | 7.7 | 4.7 |
| 1333 | 鬱 KO | 54.6 | 0 | 77 | 3/2 | 1/1 | 6/2 | 0 | 31.9 | 6.0 |
| 1370 | 鬱 KO | 59.7 | 0 | 5 | 11/2 | 1/2 | 9/0 | 1 | 14.3 | 6.4 |
| 1407 | 鬱 TIME_UP | 60.0 | 2 | 26 | 10/2 | 2/2 | 6/3 | 1 | 17.6 | 6.7 |
| 1444 | 一 KO | 51.8 | 29 | 0 | 12/3 | 1/1 | 7/2 | 0 | 13.0 | 5.9 |
| 1481 | 鬱 KO | 19.6 | 0 | 113 | 1/2 | 0/0 | 1/2 | 0 | 14.2 | 6.1 |
| 1518 | 鬱 KO | 51.9 | 0 | 23 | 6/2 | 1/1 | 7/2 | 0 | 15.8 | 6.2 |
| 1555 | 一 KO | 42.1 | 37 | 0 | 10/2 | 0/2 | 6/2 | 1 | 9.7 | 5.0 |
| 1592 | 鬱 KO | 39.9 | 0 | 30 | 11/3 | 0/0 | 5/1 | 2 | 10.5 | 6.2 |
| 1629 | 鬱 KO | 35.0 | 0 | 52 | 6/3 | 0/1 | 2/1 | 2 | 9.7 | 6.1 |
| 1666 | 鬱 KO | 28.4 | 0 | 51 | 5/3 | 0/0 | 5/1 | 0 | 8.2 | 4.6 |
| 1703 | 鬱 KO | 19.3 | 0 | 45 | 4/3 | 0/2 | 0/0 | 2 | 10.2 | 4.4 |
| 1740 | 鬱 KO | 38.7 | 0 | 51 | 4/3 | 0/0 | 5/2 | 0 | 9.5 | 4.9 |
