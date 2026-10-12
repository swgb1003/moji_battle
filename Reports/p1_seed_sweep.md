# P1 一 vs 鬱 seed sweep (Gothic, provisional glyph masks)

- matches: 20 / 一 wins 15 / 鬱 wins 5 / draws 0 (一 win rate 75%)
- finishes: KO 20 / time up 0
- duplicate body hits: 0 / env damage events 16 (outside launch window 0)
- longest gap with no attack started by either side (stalemate metric, max over matches): 6.1s
- longest no-damage gap (max over matches): 22.5s
- knockdown recoveries: 3 (settle→stand 1.18–1.60s, timeouts 0, safe shifts 1)
- script cost per fixed step: avg 0.267 ms / max 7.91 ms

| metric (avg per match) | 一 (left) | 鬱 (right) |
|---|---|---|
| attacks started | 11.6 | 6.8 |
| body hits landed | 5.7 | 3.0 |
| damage dealt by hits | 103.4 | 58.0 |
| successful guards | 0.5 | 2.1 |
| evades | 4.3 | 1.0 |
| max combo hits | 2.9 | 1.4 |
| avg windup (s) | 0.31 | 0.70 |
| avg hit impulse | 1.83 | 4.93 |
| guard time fraction | 1.4% | 8.2% |
| guard entries per attack sequence | 0.09 | 0.55 |
| attacks per sequence (combo length) | 1.29 | 1.01 |
| jammed swings (moved < 40% of planned arc) | 27.1% | 2.5% |
| approach/hold time fraction | 52.6% | 44.3% |
| attack time fraction | 33.4% | 44.1% |

| seed | result | time | HP 一 | HP 鬱 | hits 一/鬱 | guards 一/鬱 | evades 一/鬱 | env | no-dmg gap | idle gap |
|---|---|---|---|---|---|---|---|---|---|---|
| 1037 | 一 KO | 45.3 | 80 | 0 | 8/1 | 1/2 | 7/1 | 0 | 19.5 | 3.8 |
| 1074 | 鬱 KO | 11.3 | 0 | 94 | 1/3 | 1/0 | 2/0 | 1 | 5.5 | 5.4 |
| 1111 | 一 KO | 23.8 | 80 | 0 | 4/1 | 1/1 | 2/2 | 0 | 13.9 | 4.0 |
| 1148 | 一 KO | 32.3 | 22 | 0 | 9/4 | 0/3 | 4/0 | 1 | 5.3 | 3.1 |
| 1185 | 一 KO | 35.4 | 80 | 0 | 7/1 | 2/1 | 5/2 | 0 | 10.9 | 4.3 |
| 1222 | 一 KO | 50.8 | 6 | 0 | 7/5 | 0/4 | 2/3 | 4 | 6.4 | 4.6 |
| 1259 | 一 KO | 27.6 | 27 | 0 | 9/3 | 0/1 | 7/0 | 0 | 7.4 | 5.0 |
| 1296 | 一 KO | 31.4 | 51 | 0 | 9/3 | 1/2 | 4/0 | 3 | 7.1 | 3.8 |
| 1333 | 一 KO | 12.4 | 63 | 0 | 5/2 | 0/1 | 2/1 | 0 | 3.3 | 3.2 |
| 1370 | 一 KO | 22.6 | 80 | 0 | 4/1 | 0/1 | 2/1 | 0 | 7.0 | 4.8 |
| 1407 | 一 KO | 18.5 | 97 | 0 | 3/0 | 1/1 | 5/1 | 0 | 15.4 | 3.5 |
| 1444 | 一 KO | 26.6 | 14 | 0 | 5/3 | 0/2 | 5/1 | 0 | 10.5 | 4.8 |
| 1481 | 一 KO | 46.1 | 72 | 0 | 4/2 | 1/6 | 8/0 | 3 | 15.9 | 3.9 |
| 1518 | 一 KO | 31.7 | 44 | 0 | 4/2 | 0/4 | 3/1 | 1 | 9.6 | 6.1 |
| 1555 | 一 KO | 26.5 | 20 | 0 | 4/4 | 0/1 | 5/1 | 0 | 7.0 | 3.8 |
| 1592 | 鬱 KO | 48.7 | 0 | 72 | 3/6 | 0/4 | 5/3 | 1 | 22.5 | 4.0 |
| 1629 | 一 KO | 33.8 | 38 | 0 | 6/5 | 1/2 | 5/1 | 0 | 8.8 | 3.8 |
| 1666 | 鬱 KO | 39.4 | 0 | 6 | 9/5 | 1/2 | 7/1 | 0 | 11.7 | 5.1 |
| 1703 | 鬱 KO | 25.9 | 0 | 48 | 4/5 | 0/0 | 3/0 | 1 | 11.3 | 5.5 |
| 1740 | 鬱 KO | 33.6 | 0 | 22 | 8/4 | 0/3 | 3/1 | 1 | 6.3 | 4.6 |
