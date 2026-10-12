# Mode menu moving selection frame — 2026-10-10

## Change

- Removed the permanent red border from the challenge-mode card. Retained its NEW stamp.
- Added one shared selection frame that moves and fades around the five menu items: battle, tournament, challenges, friends, and achievements.
- The frame interpolates both position and size, fitting the three tall cards and two wider lower cards with the same four-pixel outset. Its four borders stretch with the rectangle rather than using fixed lengths.
- Whole-card pointer hover and nested action-button hover share one state. Whole-card clicks activate the existing action. Keyboard selection and arrow navigation also move the frame.
- Pointer hover takes precedence over keyboard focus; leaving the pointer restores any existing keyboard selection, or fades the frame out when nothing is selected.
- Frame animation uses unscaled time, settles immediately when reduced menu motion is enabled, and hides while settings are open. Its graphics and canvas group do not intercept clicks.
- Existing card colors, layout, tournament icon, and button pressed feedback remain. Button size/position motion is disabled for these cards because the shared frame supplies the hover animation.

## Verification

Unity 6000.0.61f1 PlayMode: **3 passed / 0 failed** (`menu-card-hover.xml`).

- New menu regression checks all five card targets, intermediate frame movement, dynamic lower-card size, nested CTA hover, fading after exit, animation while paused, reduced motion, actual arrow-key navigation, UI raycasting, and a whole-card click entering achievements.
- Existing menu/customizer/tournament flow passes and preserves both normal fighter builds.
- Existing button-motion and reduced-motion regression passes.
- Actual Unity renders visually inspected: initial menu without a fixed red outline (`UI/02_mode_select.png`), tournament hover (`UI/mode_hover_tournament.png`), and friends hover at 880×450 (`UI/mode_hover_friends_880x450.png`). Captures also include the other corresponding resolutions.
- Updated the challenge gallery menu screenshots to the current initial state.
- `git diff --check` passed.

Native Unity rendering/interaction and Web artifact delivery checks are separate. Automated browser rendering/console behavior is not claimed.

## Release target

Verified linked project and owner with Vercel CLI: `swgb1003-5441s-projects/moji-fighter-x`, project `prj_dysF5ncQzt0MeAgRoHEDSazVQ3eG`, team `team_T9S6jm2dWj9hQHWiULpbSwxx`. Workspace base commit `e2b60e25a4a4c7b34759b032832b2829f079bfc5` plus current game changes. Publish only generated static `Builds/WebGL` output.

## Deployment result

- Unity WebGL build succeeded (`menu-card-hover-web-build.log`); artifact timestamp `2026-10-10T12:54:48.3203419Z`.
- Local and production delivery each passed all 11 checks: HTTP success, exact generated-output hashes, gzip integrity, Wasm/JS MIME types, and font/license notices (`menu-card-hover-local-delivery.json`, `menu-card-hover-production-delivery.json`).
- Status **READY**, environment **production**, remote build duration **6 seconds**.
- Deployment ID: `dpl_BD2DWxJUH7KYnPM3CnR5SFdbBACK`.
- Deployment URL: https://moji-fighter-qdf1suvp6-swgb1003-5441s-projects.vercel.app
- Production URL: https://moji-fighter-x.vercel.app
- Inspector: https://vercel.com/swgb1003-5441s-projects/moji-fighter-x/BD2DWxJUH7KYnPM3CnR5SFdbBACK

Post-deploy observability: CLI inspection confirmed production/READY and the expected aliases. Error-level logs (`--since 1h --limit 50 --no-follow`) returned no entries. Public artifact delivery verification passed; static deployment logs do not establish browser console behavior. Log-drain configuration was not inspected. No protection settings were changed.
