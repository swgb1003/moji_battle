# Combat motion and pacing — 2026-10-11 JST

The default balance now enables combat tempo across normal, challenge and draft fights. Ordinary blade attacks prepare in 80% of the previous nominal time (maximum 0.75 s), and reset in 50% (maximum 0.4 s). Physical pose completion gets at most 0.12 s extra preparation/reset time, and actual blade swings keep their contact-based hit resolution with at most 0.3 s extra travel. This changes simulation timing rather than spectator playback speed.

Preparation/reset torque is increased while retaining inertia, weapon mass, collider geometry and angular-speed limits. Ordinary swing trajectories retain their original physical acceleration; damaging contact still comes from the moving physical weapon. The end of an ordinary reset can overlap a small spacing step after the damaging follow-through window. AI decisions, attack scheduling and cooldowns respond sooner, with shorter style-specific retreat periods. Large whiffs retain their penalty, and specialized jumps, throws, ranged releases, kicks and full-body spins retain their own timings.

Settled ordinary knockdowns wait 0.4–0.8 s instead of 1–2 s. A grounded 2.5 s fallback avoids extended sliding; airborne knockdowns retain the original hard timeout and KOs never stand up. Wall/overlap safety placement and recovery actions are retained. Small visual upper-body anticipation and follow-through accompany ordinary blade swings; hands still track the physical grip.

## Comparative evidence

Six configurations (S aggressive, M aggressive, XL two-handed aggressive, L horizontal defensive, M reverse hit-and-away, XL counter) × seeds 739 and 1518 × old/new pacing: 24 matches, each up to 20 simulation seconds, stopping on match end. Old pacing retains the previous heavy-only tempo feature, so the baseline matches the previous production behavior.

- Attack starts/minute: 26.96 → 35.23 (+30.66%).
- Physical hits/minute: 10.58 → 13.79 (+30.26%).
- Fraction of time with both fighters stationary in approach/guard/reset: −13.83%.
- Mean time to first active attack: −17.12%.
- Time per actual reset: 0.726 ? 0.530 s (about 27% shorter). Acceptance also checks reduced stationary time, increased actual attack attempts and retained physical hit frequency.
- No sustained grip stretch in the comparative set. Detailed CSV and metrics: `combat-tempo-comparison.csv`, `combat-tempo-metrics.json`.

`CombatTempo/index.html` compares 12-second native motion clips at normal simulation speed (25 captured frames/s, two 0.02 s simulation steps/frame). It uses a plain background and suppresses time effects to isolate motion; it is not a capture of the full themed browser UI.

An initial tuning reduced stationary waiting but did not sufficiently reduce per-reset waiting. It was superseded by the stronger reset/torque adjustment. Initial failures are retained as `combat-tempo-play.xml` and `combat-tempo-regression-initial.xml`. Regression findings led to explicitly preserving special-move timings, ordinary swing inertia response and manual AI-interval overrides. Release verification uses the final regression report.

Native Unity simulation and rendered motion are tested. Browser rendering, console, audio and IndexedDB behavior are not automated; public delivery is checked separately.

## Release target

Production `swgb1003-5441s-projects/moji-fighter-x`; project `prj_dysF5ncQzt0MeAgRoHEDSazVQ3eG`, team `team_T9S6jm2dWj9hQHWiULpbSwxx`. CLI inspection and local project link match. Base commit `e2b60e25a4a4c7b34759b032832b2829f079bfc5` plus the existing workspace and this change. Only generated `Builds/WebGL` is published; existing unrelated workspace changes are preserved.

## Final checks

- PlayMode: 66 passed, 0 failed (`combat-tempo-regression.xml`). Coverage includes 24 comparative fights, replay determinism, heavy attack/whiff distinction, settled/airborne/KO recovery, physical grip stability, complex glyph contact locks, sizes/grips, jumps/throws/walls, full spins/parries, both projectiles including jump evasion, expanded actions, all 30 builtin special chains, cheer/brace gauge use, and the complete draft UI/achievement flow.
- Final source fingerprints: `combat-tempo-source.json`.

- EditMode: 20 passed, 0 failed (`combat-tempo-edit.xml`), covering damage/impulse caps, collision resolution, environment/slam rules, determinism and customization data. Total final checks: 86 passed.

## Published release

- WebGL build completed with exit 0 (`combat-tempo-web-build.log`), UTC `2026-10-11T05:43:19.4840729Z`.
- Source fingerprint mismatches after build and deployment: 0.
- Local and public production delivery passed 11/11 artifact checks: generated hashes, gzip integrity, Wasm/JS MIME and notices.
- Deployment `dpl_3e7goYeHLuzU2DT8FAmAtqejQygJ`: READY / production; expected team and project; remote build 4 seconds.
- Production https://moji-fighter-x.vercel.app
- Unique deployment https://moji-fighter-9yfymvz7s-swgb1003-5441s-projects.vercel.app
- Error-level runtime logs returned no entries. Deployment protection unchanged; browser console/log-drain configuration not inspected.
- Structured release metadata: `combat-tempo-release.json`.
