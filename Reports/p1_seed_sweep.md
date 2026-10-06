# P1 一 vs 鬱 seed sweep (Gothic, provisional glyph masks)

- matches: 20 / 一 wins 5 / 鬱 wins 15 / draws 0 (一 win rate 25%)
- finishes: KO 20 / time up 0
- duplicate body hits: 0 / env damage events 2 (outside launch window 0)
- longest gap with no attack started by either side (stalemate metric, max over matches): 6.7s
- longest no-damage gap (max over matches): 32.3s
- knockdown recoveries: 1 (settle→stand 1.40–1.40s, timeouts 0, safe shifts 0)
- script cost per fixed step: avg 0.184 ms / max 24.46 ms

| metric (avg per match) | 一 (left) | 鬱 (right) |
|---|---|---|
| attacks started | 12.2 | 5.8 |
| body hits landed | 5.1 | 2.1 |
| damage dealt by hits | 71.4 | 82.0 |
| successful guards | 0.4 | 0.5 |
| evades | 4.1 | 1.6 |
| max combo hits | 3.3 | 1.1 |
| avg windup (s) | 0.24 | 1.31 |
| avg hit impulse | 2.33 | 6.65 |
| guard time fraction | 1.3% | 5.2% |
| guard entries per attack sequence | 0.08 | 0.36 |
| attacks per sequence (combo length) | 1.59 | 1.00 |
| jammed swings (moved < 40% of planned arc) | 54.0% | 0.0% |
| approach/hold time fraction | 68.6% | 43.6% |
| attack time fraction | 22.8% | 47.7% |

| seed | result | time | HP 一 | HP 鬱 | hits 一/鬱 | guards 一/鬱 | evades 一/鬱 | env | no-dmg gap | idle gap |
|---|---|---|---|---|---|---|---|---|---|---|
| 1037 | 鬱 KO | 52.6 | 0 | 35 | 5/3 | 0/3 | 6/0 | 0 | 16.3 | 6.1 |
| 1074 | 鬱 KO | 59.7 | 0 | 79 | 1/3 | 1/1 | 9/7 | 0 | 27.5 | 6.7 |
| 1111 | 一 KO | 42.1 | 33 | 0 | 8/2 | 2/0 | 2/3 | 0 | 14.1 | 6.1 |
| 1148 | 鬱 KO | 36.4 | 0 | 57 | 3/2 | 0/1 | 5/1 | 0 | 15.9 | 6.2 |
| 1185 | 鬱 KO | 23.6 | 0 | 37 | 4/3 | 0/0 | 2/0 | 0 | 8.3 | 4.7 |
| 1222 | 一 KO | 30.2 | 44 | 0 | 6/1 | 1/0 | 4/0 | 0 | 12.6 | 6.1 |
| 1259 | 一 KO | 43.0 | 38 | 0 | 5/2 | 0/1 | 3/2 | 0 | 18.4 | 5.4 |
| 1296 | 鬱 KO | 22.9 | 0 | 52 | 3/2 | 0/0 | 1/0 | 0 | 9.3 | 3.8 |
| 1333 | 鬱 KO | 9.7 | 0 | 87 | 4/2 | 0/0 | 0/0 | 0 | 4.7 | 3.2 |
| 1370 | 鬱 KO | 42.4 | 0 | 37 | 7/3 | 0/0 | 4/1 | 0 | 22.0 | 5.6 |
| 1407 | 鬱 KO | 27.5 | 0 | 22 | 7/2 | 0/1 | 3/0 | 1 | 9.2 | 3.9 |
| 1444 | 鬱 KO | 33.8 | 0 | 6 | 8/3 | 1/0 | 5/2 | 0 | 9.8 | 5.6 |
| 1481 | 鬱 KO | 36.6 | 0 | 85 | 2/2 | 0/1 | 7/1 | 0 | 19.0 | 5.9 |
| 1518 | 一 KO | 12.5 | 67 | 0 | 5/0 | 0/0 | 1/1 | 0 | 4.7 | 4.0 |
| 1555 | 鬱 KO | 7.8 | 0 | 106 | 0/1 | 0/0 | 1/0 | 0 | 7.8 | 3.8 |
| 1592 | 鬱 KO | 41.6 | 0 | 56 | 4/2 | 1/0 | 4/2 | 0 | 9.9 | 5.8 |
| 1629 | 鬱 KO | 58.2 | 0 | 14 | 8/3 | 1/0 | 8/5 | 0 | 32.3 | 4.5 |
| 1666 | 鬱 KO | 45.3 | 0 | 29 | 5/1 | 0/0 | 6/1 | 0 | 18.1 | 6.1 |
| 1703 | 鬱 KO | 43.7 | 0 | 29 | 6/3 | 1/0 | 7/1 | 1 | 23.4 | 5.9 |
| 1740 | 一 KO | 35.8 | 56 | 0 | 10/1 | 0/1 | 4/4 | 0 | 9.9 | 4.2 |
