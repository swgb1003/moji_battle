# World Cup design release

Production: https://moji-fighter-x.vercel.app

- Rebuilt 17 World Cup nonbattle screens against the approved mockups; battle presentation was not changed.
- Added 13 GPT Images assets; retained the 7 previously generated World Cup icons. Source paths, reference images and full prompts are in `Designs/WorldCup/redesign-assets.json` and `Designs/WorldCup/icon-assets.json`.
- Rebuilt the typography, printed panels, decorative borders, tables, brackets, dynamic fighter previews, podium animation, champion scene and memorial photo.
- Native Unity PlayMode flow suite passed 6/6 after final import sizing. Verified championship, bronze placement, defeat, resume, shared M-only customization and computer league simulation.
- Reviewed native 1920x1080 screenshots and small-screen layouts. Comparison gallery: `Designs/WorldCup/redesign-comparison.html`.
- Source snapshot matched 919 files after the successful WebGL build. Production delivery matched all 12 expected build and license files byte-for-byte; gzip, WebAssembly header and JavaScript/WebAssembly MIME checks passed.
- Deployment `dpl_8bDbSJe3rXbJFa8bdeCuYsdKkWdi` is READY in production under `swgb1003-5441s-projects`.
- Large backgrounds retain 2048 maximum import resolution. Small artwork uses 1024, held cups and VS use 512. Original generated PNGs are unchanged. This resolves the initial static-file upload limit failure while retaining uncompressed texture edges.

Verification used Unity native rendering and production file delivery. It does not establish browser gameplay end-to-end testing.

The new heading font is Noto Sans CJK JP Black, distributed with its OFL license in the WebGL export. Official source: https://github.com/notofonts/noto-cjk/blob/main/Sans/OTF/Japanese/NotoSansCJKjp-Black.otf
