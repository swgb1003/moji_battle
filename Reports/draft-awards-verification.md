# Draft achievements — 2026-10-11 JST

Added IDs 57–64 and the Draft category, preserving IDs 1–56 and existing saved progress. The eight rewards cover a completed draft match, a series win, a 2–0 win, a 0–1 comeback to 2–1, a COM-first round win, wins with 5/15 distinct characters, and 10 series wins. Draws complete a match but do not advance wins or the distinct-character collection. Quitting before a result grants nothing. A series that first leads 1–0 does not qualify as a 0–1 comeback.

Each draft has a persisted UUID and processed-history cursor. Replaying results or resuming/reloading cannot increment counters again. Old unfinished draft saves receive an ID on resume, and old achievement profiles retain unlocked/seen titles. New draft notifications precede the existing queue and appear on results; picking, customization, battle and settings pause their visibility and timers.

Eight standalone vector letter-card icons avoid the original 50-title atlas. Crown, stacked-card and arrow variants distinguish the rewards. The achievement screen derives its total/category/page counts from the catalog: 64 titles, 8 draft titles across 2 pages.

## Verification

- EditMode: 27 passed, 0 failed (`draft-awards-edit.xml`). Includes every reward threshold, wins/losses/draws, valid/invalid comebacks, duplicate characters, 10 series wins, JSON persistence, aborts, legacy saves and duplicate processing.
- PlayMode: 10 passed, 0 failed (`draft-awards-play.xml`). Includes the actual draft-to-customization-to-battle-to-result journey, first-match and championship notifications, queue preservation, settings pause, all eight vector meshes, category navigation, existing challenge/title/menu flows, click selection and draft BGM regression checks.
- Actual rendered screenshots checked at 1920×1080, 880×450 and 1280×960: result notification, earned/locked progress, and both pages of all eight icons. `UI/Draft/index.html` contains the gallery. The last two icon pages use test-only unlocks; tests restore the user's previous saved data.
- Source fingerprints and base commit recorded in `draft-awards-source.json`. Existing unrelated workspace edits are preserved.

Native Unity scene/control/render verification is complete. Browser rendering, browser console, IndexedDB persistence and audible browser playback have not been automated. Public artifact delivery is verified separately.

## Release target

Production `swgb1003-5441s-projects/moji-fighter-x`; project `prj_dysF5ncQzt0MeAgRoHEDSazVQ3eG`, team `team_T9S6jm2dWj9hQHWiULpbSwxx`. CLI inspection and the local link match. Base commit `e2b60e25a4a4c7b34759b032832b2829f079bfc5` plus the existing workspace and this addition. Only generated `Builds/WebGL` is published.

## Release verification

- Unity WebGL build succeeded with exit 0; built at `2026-10-11T02:27:01.9624500Z` (`draft-awards-web-build.log`).
- Source hashes unchanged after build and deployment: 0 mismatches.
- Local and public production artifact checks each passed all 11 files: exact hashes, gzip integrity, Wasm/JS MIME and bundled notices.
- Deployment `dpl_GUqEDQi1cBMwMuqScJPZ7vvHNwBv`: READY / production; expected project/team; remote build 3 seconds.
- Production https://moji-fighter-x.vercel.app
- Unique deployment https://moji-fighter-3k9qmgpiv-swgb1003-5441s-projects.vercel.app
- Error-level runtime logs returned no entries. Deployment protection was unchanged. Browser console and log-drain configuration were not inspected.
- Structured metadata: `draft-awards-release.json`.
