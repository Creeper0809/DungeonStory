# Rendered Gameplay 1024x1024 Performance Report

## Scope

- Build: Windows standalone `HumanPlaytest`, Unity 6000.3.8f1
- Launch path: normal new run into `GameplayScene`
- Resolution: 1600x900, VSync off, uncapped frame rate
- Hardware: Intel Core i9-14900HX, RTX 4080 Laptop GPU, 32 GB RAM
- Dense world: 1024x1024 Grid, 64 active floors, 8,192 modular facilities,
  4,028 interior doors, 12,366 live `BuildableObject` instances
- Characters: real pooled `CharacterActor` prefabs with AI, colliders, renderers,
  nameplates, and normal runtime injection
- Included: gameplay simulation, AI, physics, rendering, HUD, nameplates

The dense population is a deterministic stress setup entered after a normal new run. It is
not a save accumulated through hundreds of hours of play, but it uses the product scene and
real runtime objects rather than editor-only mock actors or data-only Grid occupants.

## Results

| Scenario | Actors | Buildings | Renderers | Sample | Avg | p95 | p99 | Max | 1% low | Frames >16.67ms |
|---|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|
| Normal new run | 8 | 146 | 91 | 5.42s | 0.753ms | 1.104ms | 2.230ms | 7.114ms | 448.51 FPS | 0 |
| Dense 100 | 100 | 12,366 | 12,612 | 12.00s | 3.755ms | 5.961ms | 34.209ms | 482.159ms | 29.23 FPS | 47 |
| Dense 500, initial | 502 | 12,366 | 13,016 | 12.00s | 15.241ms | 40.189ms | 364.437ms | 479.788ms | 2.74 FPS | 77 |
| Dense 500, steady | 502 | 12,366 | 12,999 | 30.06s | 17.871ms | 60.535ms | 410.953ms | 537.337ms | 2.43 FPS | 235 |

All four reports passed their structural validity checks and recorded zero Unity
`Error` and zero `Warning` messages. The 500-character log did report rejected persona
requests as normal log messages because the local LLM queue was full.

## Conclusion

- The normal game and the rendered 100-character dense world meet the 60 FPS target at p95.
- Dense 100 does not meet p99 or every-frame 60 FPS because periodic main-thread stalls remain.
- Dense 500 does not meet 60 FPS. The longer steady run is worse than the initial sample,
  so this is not only a spawn/setup spike.
- In the steady 500 run, the last sampled scheduler slice alone was 46.564ms. The prior
  data-only scheduler benchmark therefore did not represent the full product-scene cadence.
- A 500-character 60 FPS guarantee is not currently supportable. AI scheduling spikes,
  persona request fan-out, and presentation/nameplate lifecycle work need another measured
  optimization pass.

## Artifacts

- `Artifacts/QA/GameplayPerformance/baseline.json`
- `Artifacts/QA/GameplayPerformance/dense-100.json`
- `Artifacts/QA/GameplayPerformance/dense-500.json`
- `Artifacts/QA/GameplayPerformance/dense-500-steady.json`
- Matching `.png` screenshots and `-player.log` files are stored in the same directory.

## Product Defect Found During Measurement

The first standalone run failed before gameplay because `IGridTraversalCostPolicy` was not
registered in the product composition root. The editor-only benchmarks had hidden this.
`DungeonFoundationRegistration` now registers
`DefaultGridTraversalCostPolicy.Instance`, and all reported standalone runs start with
zero console errors.
