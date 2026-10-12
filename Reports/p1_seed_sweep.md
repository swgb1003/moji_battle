# P1 一 vs 鬱 seed sweep (Gothic, provisional glyph masks)

- matches: 20 / 一 wins 15 / 鬱 wins 5 / draws 0 (一 win rate 75%)
- finishes: KO 20 / time up 0
- duplicate body hits: 0 / env damage events 14 (outside launch window 0)
- longest gap with no attack started by either side (stalemate metric, max over matches): 5.0s
- longest no-damage gap (max over matches): 21.6s
- knockdown recoveries: 6 (settle→stand 0.42–0.74s, timeouts 0, safe shifts 1)
- script cost per fixed step: avg 0.236 ms / max 5.27 ms

| metric (avg per match) | 一 (left) | 鬱 (right) |
|---|---|---|
| attacks started | 12.9 | 8.3 |
| body hits landed | 6.5 | 3.0 |
| damage dealt by hits | 105.4 | 62.7 |
| successful guards | 0.6 | 2.5 |
| evades | 5.8 | 1.2 |
| max combo hits | 3.5 | 1.6 |
| avg windup (s) | 0.26 | 0.57 |
| avg hit impulse | 1.54 | 5.02 |
| guard time fraction | 3.7% | 11.6% |
| guard entries per attack sequence | 0.16 | 0.55 |
| attacks per sequence (combo length) | 1.27 | 1.00 |
| jammed swings (moved < 40% of planned arc) | 27.8% | 5.8% |
| approach/hold time fraction | 47.1% | 33.3% |
| attack time fraction | 33.4% | 49.5% |

| seed | result | time | HP 一 | HP 鬱 | hits 一/鬱 | guards 一/鬱 | evades 一/鬱 | env | no-dmg gap | idle gap |
|---|---|---|---|---|---|---|---|---|---|---|
| 1037 | 鬱 KO | 33.5 | 0 | 42 | 6/4 | 1/6 | 5/2 | 0 | 20.5 | 2.8 |
| 1074 | 一 KO | 17.2 | 88 | 0 | 6/1 | 1/0 | 5/0 | 1 | 4.7 | 4.0 |
| 1111 | 一 KO | 42.2 | 10 | 0 | 7/4 | 1/5 | 8/1 | 4 | 15.5 | 3.3 |
| 1148 | 鬱 KO | 31.5 | 0 | 92 | 2/4 | 0/3 | 8/2 | 1 | 13.0 | 3.0 |
| 1185 | 一 KO | 19.0 | 97 | 0 | 5/0 | 0/2 | 3/0 | 1 | 6.2 | 5.0 |
| 1222 | 一 KO | 19.9 | 44 | 0 | 10/3 | 1/0 | 3/0 | 0 | 3.9 | 2.8 |
| 1259 | 一 KO | 23.6 | 85 | 0 | 6/1 | 1/3 | 6/0 | 0 | 7.2 | 4.2 |
| 1296 | 一 KO | 33.3 | 1 | 0 | 8/5 | 1/3 | 8/1 | 0 | 6.0 | 3.2 |
| 1333 | 一 KO | 32.5 | 80 | 0 | 5/1 | 0/4 | 5/5 | 0 | 17.4 | 3.0 |
| 1370 | 一 KO | 48.5 | 18 | 0 | 8/4 | 0/2 | 10/4 | 1 | 14.1 | 3.7 |
| 1407 | 一 KO | 49.1 | 40 | 0 | 9/3 | 0/5 | 10/2 | 1 | 21.6 | 3.7 |
| 1444 | 一 KO | 22.8 | 63 | 0 | 7/2 | 0/0 | 4/1 | 0 | 5.9 | 3.7 |
| 1481 | 鬱 KO | 26.1 | 0 | 6 | 5/3 | 1/4 | 3/0 | 0 | 9.5 | 3.2 |
| 1518 | 一 KO | 32.5 | 13 | 0 | 8/3 | 0/3 | 5/1 | 0 | 7.4 | 3.8 |
| 1555 | 鬱 KO | 21.0 | 0 | 50 | 6/4 | 1/1 | 4/0 | 0 | 5.6 | 2.2 |
| 1592 | 一 KO | 46.4 | 26 | 0 | 8/5 | 1/3 | 9/3 | 1 | 12.4 | 3.4 |
| 1629 | 一 KO | 15.6 | 80 | 0 | 4/1 | 1/2 | 4/0 | 0 | 5.2 | 4.1 |
| 1666 | 一 KO | 36.0 | 22 | 0 | 8/5 | 1/2 | 5/1 | 1 | 11.2 | 3.6 |
| 1703 | 一 KO | 12.3 | 84 | 0 | 4/1 | 0/0 | 4/0 | 0 | 7.6 | 3.3 |
| 1740 | 鬱 KO | 24.1 | 0 | 55 | 7/5 | 1/2 | 7/1 | 3 | 5.8 | 3.4 |
