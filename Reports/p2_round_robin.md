# P2 round robin — 9 glyphs (Gothic), 36 pairs × 3 seeds

- matches: 108 / draws: 3 (3%) / sudden death: 11 / no damage at all: 3 / KO: 4 (4%) / avg total damage per match: 35
- duplicate body hits: 0 / env damage outside launch window: 0
- longest gap with no attack by either side: 6.6s (I vs 一 seed 5193)

| 文字 | 重量 | 勝率 | KO勝 | 攻撃/分 | 連撃 | 回避/分 | 跳び越え/分 | ガード/攻撃 | 命中率 | 空振り率 | 弾き率 | 被ガード率 | 振り始め距離/射程 | 与ダメ/試合 | 平均衝撃 | 溜め s | 振りの固着 | かすり/試合 | 壁床ダメ/試合 |
|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|
| 鬱 | Heavy | 88% | 4 | 11.4 | 1.00 | 0.5 | 0.00 | 0.17 | 17% | 43% | 36% | 6% | 1.87 | 55 | 11.0 | 1.20 | 3% | 0.3 | 0.50 |
| 口 | Medium | 63% | 0 | 14.4 | 1.11 | 1.6 | 0.00 | 0.07 | 9% | 67% | 24% | 2% | 2.04 | 14 | 6.4 | 0.60 | 3% | 0.2 | 0.25 |
| X | Light | 63% | 0 | 17.0 | 1.34 | 1.4 | 0.52 | 0.05 | 9% | 53% | 31% | 6% | 1.27 | 11 | 4.1 | 0.37 | 22% | 0.2 | 0.08 |
| I | Light | 46% | 0 | 13.3 | 1.28 | 3.9 | 0.74 | 0.10 | 19% | 45% | 35% | 2% | 1.37 | 18 | 3.9 | 0.24 | 26% | 0.2 | 0.17 |
| 一 | Light | 42% | 0 | 16.1 | 1.32 | 3.9 | 0.46 | 0.11 | 23% | 43% | 35% | 2% | 1.37 | 27 | 3.4 | 0.21 | 38% | 0.3 | 0.13 |
| 山 | Medium | 42% | 0 | 12.6 | 1.10 | 1.8 | 0.00 | 0.10 | 7% | 58% | 30% | 6% | 1.98 | 10 | 6.3 | 0.60 | 5% | 0.1 | 0.13 |
| 火 | Medium | 38% | 0 | 13.7 | 1.12 | 2.0 | 0.08 | 0.13 | 6% | 63% | 25% | 5% | 1.90 | 13 | 8.0 | 0.53 | 8% | 0.1 | 0.21 |
| A | Light | 29% | 0 | 9.8 | 1.20 | 1.9 | 0.82 | 0.06 | 7% | 52% | 38% | 2% | 1.50 | 4 | 4.4 | 0.38 | 20% | 0.1 | 0.04 |
| O | Medium | 29% | 0 | 8.8 | 1.12 | 4.9 | 0.20 | 0.35 | 17% | 40% | 43% | 3% | 1.53 | 8 | 3.9 | 0.45 | 21% | 0.2 | 0.00 |

## 対戦成績（各組の勝ち数）

| 組 | 勝ち |
|---|---|
| 一 vs 口 | 1 - 2 |
| 一 vs 山 | 1 - 2 |
| 一 vs 火 | 1 - 2 |
| 一 vs 鬱 | 0 - 3 |
| 一 vs A | 2 - 1 |
| 一 vs I | 2 - 1 |
| 一 vs O | 2 - 1 |
| 一 vs X | 1 - 2 |
| 口 vs 山 | 3 - 0 |
| 口 vs 火 | 2 - 1 |
| 口 vs 鬱 | 1 - 2 |
| 口 vs A | 3 - 0 |
| 口 vs I | 2 - 1 |
| 口 vs O | 1 - 2 |
| 口 vs X | 1 - 1 |
| 山 vs 火 | 2 - 0 |
| 山 vs 鬱 | 1 - 2 |
| 山 vs A | 2 - 1 |
| 山 vs I | 2 - 1 |
| 山 vs O | 1 - 2 |
| 山 vs X | 0 - 3 |
| 火 vs 鬱 | 0 - 3 |
| 火 vs A | 2 - 1 |
| 火 vs I | 1 - 2 |
| 火 vs O | 2 - 1 |
| 火 vs X | 1 - 2 |
| 鬱 vs A | 3 - 0 |
| 鬱 vs I | 3 - 0 |
| 鬱 vs O | 3 - 0 |
| 鬱 vs X | 2 - 1 |
| A vs I | 0 - 2 |
| A vs O | 3 - 0 |
| A vs X | 1 - 2 |
| I vs O | 2 - 1 |
| I vs X | 2 - 1 |
| O vs X | 0 - 3 |

## 最小検証ケース（山 vs 一、口 vs 火）

- 一 vs 山 (seed 5062): TIME_UP winner=一 | guards 0/0, grazes 0/0, clashes 3/3, hits 1/0, colliders 1/8
- 山 vs 一 (seed 5069): TIME_UP winner=山 | guards 0/1, grazes 0/0, clashes 6/1, hits 1/1, colliders 8/1
- 一 vs 山 (seed 5076): TIME_UP winner=山 | guards 0/0, grazes 0/0, clashes 3/2, hits 0/2, colliders 1/8
- 口 vs 火 (seed 5310): TIME_UP winner=火 | guards 1/1, grazes 0/0, clashes 7/6, hits 5/4, colliders 8/64
- 火 vs 口 (seed 5317): TIME_UP winner=口 | guards 1/0, grazes 0/0, clashes 2/3, hits 0/1, colliders 64/8
- 口 vs 火 (seed 5324): TIME_UP winner=口 | guards 0/0, grazes 1/0, clashes 4/0, hits 2/0, colliders 8/64
- no damage: 口 vs X seed 5465 | attacks 21/10 clashes 6/3 whiffs 14/6 guards 0/0 evades 1/3
- no damage: 山 vs 火 seed 5496 | attacks 18/23 clashes 2/3 whiffs 14/18 guards 1/2 evades 2/3
- no damage: I vs A seed 5968 | attacks 16/9 clashes 2/4 whiffs 13/5 guards 0/0 evades 0/1
