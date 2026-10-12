# World Cup illustrated UI

Replaced the wire globe and simplified tournament drawing with seven generated, transparent PNG assets based on the approved tournament selection and guide designs. The guide now includes the podium illustration, three illustrated rule cards, navy/red headings and Fuji decoration. Shared paper framing, live text, buttons and the Watanabe Challenge Cup name remain native UI.

- Generation: built-in `image_gen`; references, final prompts and asset paths are recorded in [icon-assets.json](Designs/WorldCup/icon-assets.json).
- Assets: `Assets/Resources/UI/WorldCup/` (7 RGBA PNGs; alpha spans 0–255).
- Import: no mipmaps, no texture compression, transparent edges, clamped sampling.
- Rendering: proportional fitting; illustrations do not intercept pointer input.
- Unity PlayMode: `IllustratedSelectionAndGuide_AssetsAndNavigationRenderAtDesktopAndSmallScreen` passed. Selection → guide → shared M-size customization was exercised.
- Screenshots: [selection](UI/WorldCup/01_tournament_select.png), [guide](UI/WorldCup/02_guide.png), [small selection](UI/WorldCup/01_tournament_select_880x450.png), [small guide](UI/WorldCup/02_guide_880x450.png). Inspected at 1920×1080 and 880×450; no overlaps or stretched art.
- Test report: [world-cup-icons-play.xml](world-cup-icons-play.xml).

WebGL build succeeded and was deployed to https://moji-fighter-x.vercel.app as `dpl_9Tc6KdFUyo3aWdBDu9Aa725f2Ntr` (production, READY). All 884 source files matched the pre-build snapshot. Public delivery verified all 11 expected files against the local build, including gzip integrity and Wasm/JS MIME headers. See [release evidence](world-cup-icons-release.json) and [production delivery](world-cup-icons-production-delivery.json).
