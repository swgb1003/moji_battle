# P2 round robin — 9 glyphs (Gothic), 36 pairs × 3 seeds

- matches: 108 / draws: 1 (1%) / sudden death: 3 / slams: 92 / no damage at all: 1 / KO: 2 (2%) / avg total damage per match: 34
- duplicate body hits: 0 / env damage outside launch window: 0
- longest gap with no attack by either side: 6.4s (X vs A seed 6030)

| 文字 | 重量 | 勝率 | KO勝 | 攻撃/分 | 連撃 | 回避/分 | 跳び越え/分 | ガード/攻撃 | 命中率 | 空振り率 | 弾き率 | 被ガード率 | 振り始め距離/射程 | 与ダメ/試合 | 平均衝撃 | 溜め s | 振りの固着 | かすり/試合 | 壁床ダメ/試合 |
|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|
| X | Light | 79% | 0 | 16.3 | 1.29 | 3.0 | 1.02 | 0.06 | 13% | 51% | 33% | 4% | 1.36 | 13 | 3.5 | 0.37 | 13% | 0.3 | 0.54 |
| 口 | Medium | 63% | 0 | 14.5 | 1.13 | 2.3 | 0.21 | 0.09 | 10% | 61% | 26% | 5% | 1.88 | 14 | 5.1 | 0.60 | 7% | 0.3 | 0.50 |
| 山 | Medium | 63% | 0 | 12.2 | 1.09 | 2.7 | 0.16 | 0.12 | 9% | 57% | 29% | 6% | 2.07 | 13 | 5.6 | 0.60 | 7% | 0.1 | 0.29 |
| 一 | Light | 50% | 0 | 15.8 | 1.27 | 4.9 | 0.67 | 0.07 | 23% | 41% | 34% | 3% | 1.40 | 25 | 3.3 | 0.21 | 35% | 0.3 | 0.54 |
| 鬱 | Heavy | 50% | 2 | 9.8 | 1.00 | 1.3 | 0.00 | 0.31 | 11% | 40% | 40% | 9% | 1.98 | 27 | 8.2 | 1.20 | 9% | 0.2 | 0.46 |
| A | Light | 38% | 0 | 11.1 | 1.24 | 3.1 | 1.18 | 0.08 | 15% | 39% | 41% | 5% | 1.45 | 9 | 2.5 | 0.38 | 28% | 0.3 | 0.42 |
| I | Light | 38% | 0 | 14.5 | 1.25 | 5.2 | 0.50 | 0.07 | 17% | 43% | 38% | 1% | 1.42 | 17 | 3.3 | 0.24 | 32% | 0.1 | 0.50 |
| 火 | Medium | 33% | 0 | 12.9 | 1.08 | 3.0 | 0.29 | 0.19 | 8% | 61% | 30% | 4% | 1.87 | 13 | 5.9 | 0.53 | 13% | 0.2 | 0.50 |
| O | Medium | 33% | 0 | 8.7 | 1.15 | 5.2 | 0.37 | 0.34 | 16% | 39% | 36% | 9% | 1.66 | 7 | 3.8 | 0.45 | 21% | 0.1 | 0.58 |

## 対戦成績（各組の勝ち数）

| 組 | 勝ち |
|---|---|
| 一 vs 口 | 1 - 2 |
| 一 vs 山 | 1 - 2 |
| 一 vs 火 | 1 - 2 |
| 一 vs 鬱 | 1 - 2 |
| 一 vs A | 2 - 1 |
| 一 vs I | 3 - 0 |
| 一 vs O | 2 - 1 |
| 一 vs X | 1 - 2 |
| 口 vs 山 | 1 - 2 |
| 口 vs 火 | 2 - 1 |
| 口 vs 鬱 | 3 - 0 |
| 口 vs A | 2 - 1 |
| 口 vs I | 2 - 1 |
| 口 vs O | 2 - 1 |
| 口 vs X | 1 - 2 |
| 山 vs 火 | 2 - 1 |
| 山 vs 鬱 | 3 - 0 |
| 山 vs A | 2 - 1 |
| 山 vs I | 3 - 0 |
| 山 vs O | 0 - 3 |
| 山 vs X | 1 - 2 |
| 火 vs 鬱 | 1 - 2 |
| 火 vs A | 1 - 2 |
| 火 vs I | 1 - 2 |
| 火 vs O | 1 - 2 |
| 火 vs X | 0 - 3 |
| 鬱 vs A | 1 - 1 |
| 鬱 vs I | 2 - 1 |
| 鬱 vs O | 3 - 0 |
| 鬱 vs X | 2 - 1 |
| A vs I | 1 - 2 |
| A vs O | 2 - 1 |
| A vs X | 0 - 3 |
| I vs O | 3 - 0 |
| I vs X | 0 - 3 |
| O vs X | 0 - 3 |

## 最小検証ケース（山 vs 一、口 vs 火）

- 一 vs 山 (seed 5062): TIME_UP winner=一 | guards 1/0, grazes 0/0, clashes 5/4, hits 3/0, colliders 1/8
- 山 vs 一 (seed 5069): TIME_UP winner=山 | guards 0/1, grazes 0/0, clashes 5/3, hits 1/1, colliders 8/1
- 一 vs 山 (seed 5076): TIME_UP winner=山 | guards 0/0, grazes 1/0, clashes 6/5, hits 4/2, colliders 1/8
- 口 vs 火 (seed 5310): TIME_UP winner=口 | guards 0/1, grazes 0/0, clashes 2/2, hits 1/0, colliders 8/64
- 火 vs 口 (seed 5317): TIME_UP winner=口 | guards 0/0, grazes 0/1, clashes 7/8, hits 2/3, colliders 64/8
- 口 vs 火 (seed 5324): TIME_UP winner=火 | guards 0/0, grazes 1/0, clashes 4/3, hits 4/3, colliders 8/64
- no damage: A vs 鬱 seed 5844 | attacks 13/13 clashes 11/5 whiffs 2/4 guards 0/0 evades 5/1
