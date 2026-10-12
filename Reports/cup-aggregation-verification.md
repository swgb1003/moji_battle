# Cup aggregation optimization

Applied to Watanabe Challenge Cup and World Character Cup computer fixtures.

- Shared computer-match runner uses a 24 ms board budget and larger batches; battle background work retains a 3 ms budget checked every step.
- Computer fixtures omit per-step diagnostic cost measurements and presentation pose captures. Active player battles keep both enabled.
- Collider ownership is cached instead of repeatedly scanning collider arrays. Collision-gate removal caches pair counts while preserving list order and duplicate removal behavior.
- Physics timestep, solver iterations, AI, intervention logic and exact collision-distance queries are unchanged. A proposed bounds shortcut was rejected because the result comparison detected a changed winner.
- Four final PlayMode tests passed: six-fixture equivalence/performance comparison, real 23-fixture league computation and resume, starting the player match during aggregation with background physics restored, and the Watanabe three-win championship flow.

Native benchmark: 29.27 seconds before, 24.00 seconds after (18.0% shorter elapsed time). Six identical fixtures have equal winners, finish reasons, elapsed match times, remaining HP, maximum HP and simulation step counts. This timing is a native Unity PlayMode comparison, not a browser performance measurement.

Reports: `cup-aggregation-final-tests.xml`, `cup-aggregation-benchmark.json`, `cup-aggregation-source.json`. Production delivery will be recorded separately after successful publication.

Production release: https://moji-fighter-x.vercel.app (`dpl_AjNQ55rao16Thg9YTXb8Fm2B2TjC`, READY). All 12 public export files match the validated local build; 921 source hashes matched after build. Gzip integrity and JavaScript/WebAssembly delivery MIME checks passed. See `cup-aggregation-release.json` and `cup-aggregation-production-delivery.json`.
