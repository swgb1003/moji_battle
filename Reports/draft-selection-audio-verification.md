# Draft click selection and music ? 2026-10-11 JST

The candidate button's activation now changes the pending character; pointer entry and keyboard focus no longer change it. The selected red frame, right-hand preview and confirmation label persist across hover/focus changes and update together after another click or keyboard submit. Confirmation still locks the chosen character and COM still cannot pick the same character.

`Assets/Audio/draft.mp3` is linked through the existing `BattleMusicSettings` asset with streaming stereo music import settings. `ScreenAudioDirector` plays it on the Draft scene, covering the guide and candidate picker. The existing persistent source continues playback through the guide-to-picker transition, follows the BGM volume setting and returns to existing customization/menu tracks when leaving those screens. No duplicate music player was added.

## Verification

- PlayMode: 7 passed, 0 failed (`draft-selection-audio-play.xml`). Cases include pointer entry before selection, clicking the first card, hovering/focusing all five alternatives without changing pending selection or red frame, clicking a different card, keyboard focus vs submit, confirming the pick and COM exclusion.
- Music verified against the actual imported `draft` clip: streaming load type, loop flag, playback and advancing sample position; guide-to-picker continuity, one audio director/listener, settings open/close and volume gain, customization/back/menu switching.
- Full three-round draft journey and existing title/menu/customization/battle/tournament audio tests passed.
- Actual rendered selected-state screenshots captured at 1920?1080, 880?450 and 1280?960. `UI/Draft/15_click_locked*.png` shows the first choice remaining selected after hovering/focusing the other candidates.
- Source hashes unchanged before Web build; recorded in `draft-selection-audio-source.json`. Existing unrelated workspace edits are preserved.

Native Unity scene, control and playback-state verification is complete. Browser rendering, browser console and audible browser playback have not been automated; public artifact delivery is checked separately.

## Release

Verified target: production `swgb1003-5441s-projects/moji-fighter-x`, project `prj_dysF5ncQzt0MeAgRoHEDSazVQ3eG`, team `team_T9S6jm2dWj9hQHWiULpbSwxx`. CLI project inspection and local link match. Source is base `e2b60e25a4a4c7b34759b032832b2829f079bfc5` plus the existing workspace and this correction. Only generated `Builds/WebGL` is published.


- Unity WebGL build completed successfully, editor/script exit 0 (`draft-selection-audio-web-build.log`), timestamp `2026-10-11T01:57:18.9471054Z`.
- Source hashes remained unchanged after build (0 mismatches).
- Local and public production delivery each passed all 11 file checks, including exact generated hashes, gzip integrity, Wasm/JS MIME types and font/license notices (`draft-selection-audio-local-delivery.json`, `draft-selection-audio-production-delivery.json`).
- Deployment `dpl_CKeMzKvMFwdvEX7iuG5364zYoLUi`: READY / production, expected team/project; remote build 8 seconds.
- Production https://moji-fighter-x.vercel.app
- Unique deployment https://moji-fighter-8g8alfrim-swgb1003-5441s-projects.vercel.app
- Inspector https://vercel.com/swgb1003-5441s-projects/moji-fighter-x/CKeMzKvMFwdvEX7iuG5364zYoLUi
- Error-level runtime logs (`--since 1h --limit 50 --no-follow`) returned no entries. Browser console and log-drain configuration were not inspected. Deployment protection was unchanged.
