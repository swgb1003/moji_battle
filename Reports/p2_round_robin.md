# P2 round robin — 9 glyphs (Gothic), 36 pairs × 3 seeds

- matches: 108 / draws: 4 (4%) / sudden death: 14 / no damage at all: 4 / KO: 2 (2%) / avg total damage per match: 34
- duplicate body hits: 0 / env damage outside launch window: 0
- longest gap with no attack by either side: 30.1s (X vs 山 seed 5658)

| 文字 | 重量 | 勝率 | KO勝 | 攻撃/分 | 連撃 | 回避/分 | 跳び越え/分 | ガード/攻撃 | 命中率 | 空振り率 | 弾き率 | 被ガード率 | 振り始め距離/射程 | 与ダメ/試合 | 平均衝撃 | 溜め s | 振りの固着 | かすり/試合 | 壁床ダメ/試合 |
|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|
| X | Light | 71% | 0 | 15.9 | 1.43 | 2.0 | 1.06 | 0.04 | 16% | 53% | 32% | 5% | 1.22 | 15 | 3.5 | 0.37 | 20% | 0.2 | 0.04 |
| 山 | Medium | 63% | 0 | 12.0 | 1.09 | 2.6 | 0.04 | 0.11 | 11% | 59% | 31% | 5% | 1.94 | 17 | 7.6 | 0.60 | 5% | 0.1 | 0.33 |
| 鬱 | Heavy | 54% | 2 | 10.8 | 1.00 | 0.8 | 0.00 | 0.21 | 11% | 44% | 35% | 6% | 1.90 | 33 | 9.3 | 1.20 | 4% | 0.2 | 0.42 |
| 一 | Light | 46% | 0 | 17.3 | 1.28 | 5.2 | 0.88 | 0.09 | 23% | 46% | 32% | 2% | 1.32 | 25 | 3.2 | 0.21 | 38% | 0.4 | 0.13 |
| 口 | Medium | 46% | 0 | 14.2 | 1.13 | 1.4 | 0.04 | 0.07 | 7% | 74% | 19% | 4% | 1.98 | 14 | 8.1 | 0.60 | 2% | 0.0 | 0.25 |
| I | Light | 46% | 0 | 14.9 | 1.27 | 5.9 | 1.19 | 0.13 | 19% | 49% | 35% | 2% | 1.29 | 19 | 3.3 | 0.24 | 28% | 0.2 | 0.04 |
| A | Light | 38% | 0 | 12.7 | 1.16 | 3.0 | 1.45 | 0.06 | 8% | 66% | 24% | 1% | 1.56 | 6 | 3.7 | 0.38 | 16% | 0.1 | 0.00 |
| O | Medium | 38% | 0 | 7.9 | 1.10 | 4.4 | 0.37 | 0.31 | 20% | 38% | 42% | 4% | 1.39 | 6 | 2.6 | 0.45 | 28% | 0.3 | 0.04 |
| 火 | Medium | 33% | 0 | 12.6 | 1.12 | 2.4 | 0.12 | 0.17 | 8% | 61% | 27% | 7% | 1.90 | 12 | 5.3 | 0.53 | 13% | 0.1 | 0.04 |

## 対戦成績（各組の勝ち数）

| 組 | 勝ち |
|---|---|
| 一 vs 口 | 1 - 2 |
| 一 vs 山 | 0 - 3 |
| 一 vs 火 | 2 - 1 |
| 一 vs 鬱 | 2 - 1 |
| 一 vs A | 2 - 1 |
| 一 vs I | 2 - 1 |
| 一 vs O | 2 - 1 |
| 一 vs X | 0 - 3 |
| 口 vs 山 | 1 - 2 |
| 口 vs 火 | 2 - 1 |
| 口 vs 鬱 | 1 - 2 |
| 口 vs A | 1 - 1 |
| 口 vs I | 3 - 0 |
| 口 vs O | 0 - 3 |
| 口 vs X | 1 - 1 |
| 山 vs 火 | 1 - 2 |
| 山 vs 鬱 | 2 - 1 |
| 山 vs A | 1 - 2 |
| 山 vs I | 3 - 0 |
| 山 vs O | 2 - 1 |
| 山 vs X | 1 - 2 |
| 火 vs 鬱 | 1 - 2 |
| 火 vs A | 0 - 3 |
| 火 vs I | 0 - 3 |
| 火 vs O | 2 - 1 |
| 火 vs X | 1 - 2 |
| 鬱 vs A | 1 - 1 |
| 鬱 vs I | 2 - 1 |
| 鬱 vs O | 3 - 0 |
| 鬱 vs X | 1 - 2 |
| A vs I | 1 - 1 |
| A vs O | 0 - 3 |
| A vs X | 0 - 3 |
| I vs O | 3 - 0 |
| I vs X | 2 - 1 |
| O vs X | 0 - 3 |

## 最小検証ケース（山 vs 一、口 vs 火）

- 一 vs 山 (seed 5062): TIME_UP winner=山 | guards 0/0, grazes 0/0, clashes 5/3, hits 3/5, colliders 1/8
- 山 vs 一 (seed 5069): TIME_UP winner=山 | guards 1/1, grazes 1/0, clashes 4/2, hits 2/2, colliders 8/1
- 一 vs 山 (seed 5076): TIME_UP winner=山 | guards 0/1, grazes 1/0, clashes 6/1, hits 3/1, colliders 1/8
- 口 vs 火 (seed 5310): TIME_UP winner=火 | guards 0/0, grazes 0/0, clashes 0/2, hits 0/1, colliders 8/64
- 火 vs 口 (seed 5317): SUDDEN_DEATH winner=口 | guards 1/1, grazes 0/0, clashes 7/3, hits 0/1, colliders 64/8
- 口 vs 火 (seed 5324): TIME_UP winner=口 | guards 0/1, grazes 0/0, clashes 2/2, hits 2/1, colliders 8/64
- no damage: 口 vs A seed 5372 | attacks 20/6 clashes 2/0 whiffs 18/6 guards 0/0 evades 0/0
- no damage: X vs 口 seed 5472 | attacks 20/12 clashes 4/4 whiffs 16/8 guards 0/0 evades 4/5
- no damage: A vs 鬱 seed 5844 | attacks 20/12 clashes 7/6 whiffs 13/3 guards 1/0 evades 7/0
- no damage: I vs A seed 5968 | attacks 11/23 clashes 2/5 whiffs 9/18 guards 0/0 evades 1/4
