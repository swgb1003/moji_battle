# P2 round robin — 9 glyphs (Gothic), 36 pairs × 3 seeds

- matches: 108 / draws: 1 (1%) / sudden death: 8 / slams: 63 / no damage at all: 1 / KO: 4 (4%) / avg total damage per match: 34
- duplicate body hits: 0 / env damage outside launch window: 0
- longest gap with no attack by either side: 6.7s (I vs A seed 5968)

| 文字 | 重量 | 勝率 | KO勝 | 攻撃/分 | 連撃 | 回避/分 | 跳び越え/分 | ガード/攻撃 | 命中率 | 空振り率 | 弾き率 | 被ガード率 | 振り始め距離/射程 | 与ダメ/試合 | 平均衝撃 | 溜め s | 振りの固着 | かすり/試合 | 壁床ダメ/試合 |
|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|
| 鬱 | Heavy | 67% | 2 | 9.7 | 1.00 | 0.6 | 0.00 | 0.28 | 12% | 42% | 39% | 7% | 1.95 | 36 | 11.7 | 1.20 | 6% | 0.2 | 0.58 |
| O | Medium | 67% | 0 | 8.7 | 1.08 | 4.3 | 0.08 | 0.32 | 23% | 35% | 47% | 2% | 1.63 | 10 | 3.9 | 0.45 | 22% | 0.4 | 0.33 |
| 口 | Medium | 63% | 1 | 13.8 | 1.13 | 1.4 | 0.04 | 0.08 | 7% | 74% | 22% | 2% | 2.14 | 12 | 7.0 | 0.60 | 0% | 0.1 | 0.29 |
| 山 | Medium | 54% | 0 | 12.2 | 1.09 | 2.1 | 0.00 | 0.07 | 7% | 66% | 24% | 3% | 2.24 | 11 | 6.5 | 0.60 | 5% | 0.1 | 0.29 |
| A | Light | 46% | 0 | 10.3 | 1.19 | 2.4 | 0.77 | 0.07 | 11% | 52% | 35% | 2% | 1.45 | 7 | 3.4 | 0.38 | 15% | 0.1 | 0.21 |
| 一 | Light | 42% | 0 | 16.4 | 1.35 | 4.4 | 0.39 | 0.08 | 19% | 41% | 36% | 5% | 1.38 | 21 | 3.2 | 0.21 | 43% | 0.1 | 0.54 |
| I | Light | 42% | 1 | 13.6 | 1.22 | 4.1 | 0.42 | 0.09 | 22% | 38% | 40% | 2% | 1.40 | 18 | 3.0 | 0.24 | 34% | 0.3 | 0.25 |
| X | Light | 42% | 0 | 14.5 | 1.29 | 1.9 | 0.74 | 0.05 | 10% | 53% | 30% | 3% | 1.23 | 9 | 3.9 | 0.37 | 15% | 0.1 | 0.46 |
| 火 | Medium | 25% | 0 | 12.3 | 1.08 | 2.1 | 0.04 | 0.14 | 6% | 63% | 28% | 2% | 1.98 | 9 | 8.0 | 0.53 | 8% | 0.2 | 0.42 |

## 対戦成績（各組の勝ち数）

| 組 | 勝ち |
|---|---|
| 一 vs 口 | 2 - 1 |
| 一 vs 山 | 1 - 2 |
| 一 vs 火 | 2 - 1 |
| 一 vs 鬱 | 0 - 3 |
| 一 vs A | 0 - 3 |
| 一 vs I | 2 - 1 |
| 一 vs O | 1 - 2 |
| 一 vs X | 2 - 1 |
| 口 vs 山 | 2 - 1 |
| 口 vs 火 | 2 - 0 |
| 口 vs 鬱 | 1 - 2 |
| 口 vs A | 1 - 2 |
| 口 vs I | 3 - 0 |
| 口 vs O | 2 - 1 |
| 口 vs X | 3 - 0 |
| 山 vs 火 | 1 - 2 |
| 山 vs 鬱 | 3 - 0 |
| 山 vs A | 3 - 0 |
| 山 vs I | 2 - 1 |
| 山 vs O | 0 - 3 |
| 山 vs X | 1 - 2 |
| 火 vs 鬱 | 0 - 3 |
| 火 vs A | 1 - 2 |
| 火 vs I | 0 - 3 |
| 火 vs O | 1 - 2 |
| 火 vs X | 1 - 2 |
| 鬱 vs A | 2 - 1 |
| 鬱 vs I | 2 - 1 |
| 鬱 vs O | 2 - 1 |
| 鬱 vs X | 2 - 1 |
| A vs I | 1 - 2 |
| A vs O | 0 - 3 |
| A vs X | 2 - 1 |
| I vs O | 2 - 1 |
| I vs X | 0 - 3 |
| O vs X | 3 - 0 |

## 最小検証ケース（山 vs 一、口 vs 火）

- 一 vs 山 (seed 5062): TIME_UP winner=一 | guards 0/1, grazes 0/1, clashes 2/3, hits 2/1, colliders 1/8
- 山 vs 一 (seed 5069): TIME_UP winner=山 | guards 0/0, grazes 0/0, clashes 2/3, hits 1/1, colliders 8/1
- 一 vs 山 (seed 5076): TIME_UP winner=山 | guards 0/0, grazes 0/0, clashes 1/4, hits 1/2, colliders 1/8
- no damage: 口 vs 火 seed 5310 | attacks 19/20 clashes 1/3 whiffs 18/16 guards 0/0 evades 1/1
- 口 vs 火 (seed 5310): SUDDEN_DEATH_DRAW winner=draw | guards 0/0, grazes 0/0, clashes 1/3, hits 0/0, colliders 8/64
- 火 vs 口 (seed 5317): TIME_UP winner=口 | guards 0/1, grazes 0/0, clashes 2/1, hits 0/1, colliders 64/8
- 口 vs 火 (seed 5324): TIME_UP winner=口 | guards 0/0, grazes 0/0, clashes 2/0, hits 1/0, colliders 8/64
