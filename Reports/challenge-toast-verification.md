# Challenge first-clear achievement notification — 2026-10-10

## Fix

Two conditions prevented the notification: challenge rewards were explicitly removed from the saved notification queue, and `AchievementToast` hid its canvas throughout challenge mode, including results.

- Keep the first-clear reward in the persistent queue and place it ahead of ordinary achievements acquired in the same match.
- Allow notifications on challenge results. Hide and pause them during preparation/battle and while settings are open.
- When a priority reward moves ahead of an already active notification, present the reward and retain the older notification for later display.
- Existing result-screen reward presentation remains. Already acquired rewards do not generate another notification on repeated clears. Previously earned achievements remain saved.

## Verification

- Unity EditMode `ChallengeTests`: **11 passed / 0 failed** (`challenge-toast-edit.xml`). Includes first-clear queue persistence, one reward per match, repeated-clear deduplication, extra goal requirements, defeat/draw, and independent saved drafts.
- Updated the first-clear persistence test to require the queued notification, reload it from saved data, simulate consuming it, and verify a subsequent clear does not add it again.
- Added an end-to-end PlayMode regression: clear an actual challenge while an older notification is paused; require the correct reward toast on results; retain the older notification; preserve timing while settings are open; survive a scene transition; and avoid a duplicate reward toast after clearing again.
- Selected PlayMode regressions also cover a failed extra goal and ordinary notifications surviving scene changes.

- Unity PlayMode selected regressions: **3 passed / 0 failed** (`challenge-toast-play.xml`). New first-clear notification regression, failed-extra-goal result, and existing ordinary notification/category/scene-change flow all passed. Combined related test total: **14 passed / 0 failed**.
- Visually inspected actual reward-toast result renders at 1920×1080 and 880×450 (`UI/Challenges/14_clear_reward_toast.png` and `_880x450.png`): correct reward icon/name and visible toast on the first-clear result. The persistent overlay does not alter result card layout.
- `git diff --check` passed.

Native Unity rendering/interaction checks and Web artifact delivery checks are separate; automated browser rendering is not claimed.

## Release target

Existing production project `swgb1003-5441s-projects/moji-fighter-x`; project `prj_dysF5ncQzt0MeAgRoHEDSazVQ3eG`, team `team_T9S6jm2dWj9hQHWiULpbSwxx`. Build from workspace base commit `e2b60e25a4a4c7b34759b032832b2829f079bfc5` plus current game changes. Publish only generated static `Builds/WebGL` output.

## Deployment result

- Preflight: confirmed linked team/project and owner with Vercel CLI before publishing.
- Unity WebGL build succeeded (`challenge-toast-web-build.log`); artifact timestamp `2026-10-10T12:18:14.9208980Z`.
- Local and production delivery each passed all 11 checks: HTTP success, exact generated-output hashes, gzip integrity, Wasm/JS MIME types, and font/license notices (`challenge-toast-local-delivery.json`, `challenge-toast-production-delivery.json`).
- Status **READY**, target **production**, remote build duration **5 seconds**.
- Deployment ID: `dpl_5jWuHcQFZTnuohRPk7xNj7ejVQ34`.
- Deployment URL: https://moji-fighter-r6x09w9rn-swgb1003-5441s-projects.vercel.app
- Production URL: https://moji-fighter-x.vercel.app
- Inspector: https://vercel.com/swgb1003-5441s-projects/moji-fighter-x/5jWuHcQFZTnuohRPk7xNj7ejVQ34

Post-deploy observability: CLI inspection confirmed production/READY and the expected aliases. Error-level logs (`--since 1h --limit 50 --no-follow`) returned no entries. Public artifact delivery verification passed; static deployment logs do not establish browser console behavior. Log-drain configuration was not inspected, and no protection settings were changed.
