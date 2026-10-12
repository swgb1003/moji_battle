# UI heading font consistency

Changed the draft guide title, draft win/loss/draw title, draft VS title, and challenge clear/retry/verdict headings to the existing RetroUi UI font. Draft headings retain centered alignment and best-fit sizing. Brush font remains scoped to weapon art, candidate kanji, medal artwork, and player-selectable equipment typography.

Validation: 15 / 15 existing native PlayMode UI flow tests passed (DraftFlowTests and ChallengeFlowTests). Native captures cover 1920x1080, 880x450, and 1280x960 for the draft guide/results/VS; challenge results cover 1920x1080 and 880x450. Visually inspected the updated guide, draft VS, win/loss/draw results, and challenge clear/retry results for wrapping, clipping, and alignment.

Browser automation was not used. Native rendering and UI flows were verified; WebGL delivery is checked separately through exact file hashes, gzip integrity and response headers.
