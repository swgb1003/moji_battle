# 文字ドラフト implementation and verification — 2026-10-11 JST

## Implemented flow

Mode menu → rules → six-character draft → locked-character customization → VS confirmation → existing auto battle with interventions → round/series result. A fresh shared pool is dealt per scored round; each side takes one exclusive character. The first picker alternates each round, and two wins finish the series. Draws keep the score, round and characters for a replay. An unfinished series can be resumed from the guide screen, including partially completed picks and interrupted battles. Normal saved builds remain separate.

The four main menu cards and two lower cards retain the moving hover/keyboard selection frame. The tournament illustration now scales into its narrower card. All draft screens use the existing washi, ink, red/blue teams, double frames and rectangular controls. Preview and candidate meters use the same glyph/modifier calculations as combat. The 30 candidate kanji are baked in all four game fonts; calibration and balance assets were not regenerated. The draft preparation UI exposes the three fonts and three grip/style choices in the approved design. Special selection uses the existing special-move screen.

Battle rematch and debug glyph-change shortcuts cannot replace draft picks or score a result twice. COM uses the existing special/cheer/brace controller. Results show the latest match stats, score and round history, with next-draft, replay, restart and menu actions appropriate to the outcome. No new achievements or currency were added.

## Verification evidence

- Unity 6000.0.61f1 EditMode: **30 passed / 0 failed**, covering draft, existing challenge, customization and tournament data (`draft-edit-final.xml`).
- PlayMode: **15 distinct tests passed** across `draft-play.xml`, `draft-play-final.xml` and `draft-visual-final.xml`. Later repetitions supersede earlier results; the final full-series test also checks the VS decoration's actual renderer mesh. Exact cases are in `draft-test-summary.json`.
- Real scene/UI journey covers exclusive selections, COM first turn, settings delaying the COM pick, build locks, font/size/grip/style/special controls, VS return, actual battle, duplicate-result/rematch guards, 2–1 victory, 0–2 defeat, draws, interrupted battle, saved-session resume, menu exit and restart.
- Regression covers all six menu hover targets, nested actions, keyboard navigation, reduced motion, existing challenge screens/rewards and ordinary customization/tournament flow.
- Preview bounds checked for three fonts × four sizes × three grip positions, including grips near the edges; the entire weapon sprite stays inside its camera viewport.
- Actual rendered screenshots inspected at 1920×1080, 880×450 and 1280×960. Interactive screenshot gallery: `UI/Draft/index.html`. These are Unity renders, separate from the approved generated designs in `Design/DraftBattle-20261011`.
- `git diff --check` passed. Existing unrelated working-tree changes were retained; no commit/reset was performed.

Native Unity rendering and interactions establish the game flow. Browser rendering, console behavior and browser IndexedDB reload were not verified through automation; this report does not claim those checks. Public Web artifact delivery is checked separately below.

## Release target

Vercel CLI confirmed `swgb1003-5441s-projects/moji-fighter-x`, project `prj_dysF5ncQzt0MeAgRoHEDSazVQ3eG`, team `team_T9S6jm2dWj9hQHWiULpbSwxx`, environment production. Source is base commit `e2b60e25a4a4c7b34759b032832b2829f079bfc5` plus the existing workspace changes and this implementation. Relevant source hashes are in `draft-release-source.json`. Only the generated static `Builds/WebGL` folder is published.

## Deployment and delivery

- Unity WebGL build succeeded, editor exit code **0** (`draft-web-build.log`). Artifact timestamp: `2026-10-10T17:22:11.6510389Z`.
- Local and public production delivery each passed **all 11 file checks**, including exact generated-output hashes, gzip integrity, Wasm/JS MIME types and font/license notices (`draft-local-delivery.json`, `draft-production-delivery.json`).
- CLI inspection confirmed **READY / production**, the intended project/team and production alias. Remote build duration: **5 seconds**.
- Deployment ID: `dpl_HNoas3yDTJxpUg43B5pLGPMZYSTB`.
- Production: https://moji-fighter-x.vercel.app
- Deployment: https://moji-fighter-ikbvtsbe4-swgb1003-5441s-projects.vercel.app
- Inspector: https://vercel.com/swgb1003-5441s-projects/moji-fighter-x/HNoas3yDTJxpUg43B5pLGPMZYSTB
- Post-deployment error-level logs (`--since 1h --limit 50 --no-follow`) returned no entries. This is separate from browser console verification. Log-drain configuration was not inspected. Deployment protection settings were not changed.
