# Draft customization uses the existing screen ? 2026-10-11 JST

The draft-specific preparation layout has been removed. Draft now renders through the same `CustomizeScreen.BuildUi` and `Refresh` as ordinary customization. Panel dimensions, control placement, all four fonts, sizes, grips, styles, flips, draggable grip selection, reset, guides, eight combat-derived statistics, and the existing special-move selector are shared. Draft adds its score/round subtitle and return target, locks the drafted character, and offers the COM tab for read-only inspection. COM editing is blocked in the model as well as the controls. VS confirmation retains the established draft presentation and always uses the player and COM builds, independently of the selected inspection tab.

## Evidence

- EditMode: 12 passed, 0 failed (`draft-shared-edit.xml`), including character locks, COM mutation guards, player edits and isolation from both normal saved builds.
- PlayMode: 6 passed, 0 failed (`draft-shared-play-final.xml`), including the complete draft series, all shared options and COM inspection, VS return, battles, draws, defeats, restart, settings, interruption/resume, regular customization and the challenge journey.
- Preview bounds pass all four fonts ? four sizes ? three grip points. Native rendered screens were inspected at 1920?1080, 880?450 and 1280?960; gallery `UI/Draft/index.html` includes both player preparation and COM inspection.
- An initial PlayMode run found an expired draft-session dereference during scene teardown. The editability guard now handles a cleared session. The final suite supersedes that run and passes all six cases.
- Release source hashes and base commit are recorded in `draft-shared-release-source.json`. Existing unrelated workspace changes were preserved.

This game flow is local client state ? shared controls ? draft build ? existing battle. No new API or service credentials are needed. Native Unity rendering and scene/UI interactions are verified. Browser rendering, console output and IndexedDB reload are not claimed as automated checks; public static-artifact delivery is verified separately.

## Release

Verified release target: `swgb1003-5441s-projects/moji-fighter-x`, project `prj_dysF5ncQzt0MeAgRoHEDSazVQ3eG`, team `team_T9S6jm2dWj9hQHWiULpbSwxx`, production. Linked project and CLI project inspection match. Base commit `e2b60e25a4a4c7b34759b032832b2829f079bfc5` plus the existing working tree and this correction. Only `Builds/WebGL` is published.


- Final Unity WebGL build succeeded, editor/script exit 0 (`draft-shared-web-build-final.log`). Build timestamp: `2026-10-10T17:46:57.4119349Z`.
- The first build failed when `il2cpp.exe` crashed with native Windows exception `0xc0000005` (Bee exit -1073741819); Windows Application Error corroborated it. Re-running without changing game code succeeded. Initial diagnostics retained in `draft-shared-web-build.log` and `draft-shared-failed-build-bee.json`.
- Source hashes remained unchanged through the build (0 mismatches).
- Local delivery (`draft-shared-local-delivery.json`) and public production delivery (`draft-shared-production-delivery.json`) both passed all 11 checks: exact output hashes, gzip integrity, Wasm/JS MIME types, font/license notices.
- Deployment ID `dpl_AJcoyyoeUeY4SmDUEjRUTfbCzXji`, state READY, target production, correct team/project, remote build 4 seconds.
- Production: https://moji-fighter-x.vercel.app
- Unique deployment: https://moji-fighter-xph3vdj1r-swgb1003-5441s-projects.vercel.app
- Inspector: https://vercel.com/swgb1003-5441s-projects/moji-fighter-x/AJcoyyoeUeY4SmDUEjRUTfbCzXji
- Error-level runtime logs (`--since 1h --limit 50 --no-follow`) returned no entries. Log-drain configuration and browser console were not inspected. Deployment protection settings were unchanged.
