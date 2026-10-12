# Tournament menu icon cleanup — 2026-10-10

## Change

- Replaced the mode-select card's scaled collection of lines and concave trophy polygon fans with `TournamentMenuGraphic`.
- The trophy uses a convex curved bowl and continuous curved handle strips; the handles retain their open centers. All silhouettes include transparent edge fringes sized to approximately one screen pixel.
- The eight-entry bracket uses symmetric coordinates, consistent strokes, and continuous corners. Removed the isolated short line above its final round and centered the bracket under the trophy.
- The graphic follows the existing card entrance animation, does not receive pointer events, and refreshes its edge width when the canvas scale changes. Existing card bounds, description, button, and tournament behavior remain unchanged.

## Verification

- Unity 6000.0.61f1 PlayMode: `MenuUiTests.Menus_Render_AndControlsPreserveBothBuilds` **1 passed / 0 failed** (`tournament-icon.xml`). The existing test also checks customization controls, both fighters' builds, and entry into the tournament.
- Visually inspected actual Unity renders at 1920×1080 and 880×450: `UI/02_mode_select.png` and `UI/02_mode_select_880x450.png`. Smooth open handles, aligned bracket corners, eight nodes, no stray upper stub, and button inside card frame confirmed.
- Updated the challenge gallery's menu screenshots to the same current render.
- Capture helper now refreshes all uGUI graphics after changing the capture canvas scale, so custom geometry uses the correct scale during small-screen verification.
- `git diff --check` passed (existing repository line-ending warnings only).
- Native rendering/interaction verification and Web artifact delivery checks are separate. No claim is made that an automated browser rendered or interacted with the production WebGL build.

## Release

Existing production project: `swgb1003-5441s-projects/moji-fighter-x`, project `prj_dysF5ncQzt0MeAgRoHEDSazVQ3eG`, team `team_T9S6jm2dWj9hQHWiULpbSwxx`.

Preflight: verified the linked project and owner using Vercel CLI. Workspace base commit `e2b60e25a4a4c7b34759b032832b2829f079bfc5`, including current uncommitted game changes. Publishing only the generated static `Builds/WebGL` output.

- Unity WebGL build succeeded. Artifact timestamp: `2026-10-10T12:00:37.4516127Z` (`tournament-icon-web-build.log`).
- Local and production delivery each passed all 11 checks: HTTP success, byte hashes matching the generated output, gzip integrity, Wasm/JS MIME types, and font/license notices. Reports: `tournament-icon-local-delivery.json` and `tournament-icon-production-delivery.json`.

Deployment result:

- Status: **READY**, environment: **production**, remote build duration: **7 seconds**.
- Deployment: `dpl_4sVDnR4SqSXL3fWXWL2y5vfWguSX`.
- Deployment URL: https://moji-fighter-bqlkgkkr4-swgb1003-5441s-projects.vercel.app
- Production URL: https://moji-fighter-x.vercel.app
- Inspector: https://vercel.com/swgb1003-5441s-projects/moji-fighter-x/4sVDnR4SqSXL3fWXWL2y5vfWguSX

Post-deploy observability: CLI inspection confirmed production/READY and the expected alias. Error-level deployment logs (`--since 1h --limit 50 --no-follow`) returned no entries. Public delivery checks passed; static-site logs do not establish browser console behavior. Log-drain configuration was not inspected. No protection settings were changed.
