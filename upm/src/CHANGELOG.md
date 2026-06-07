# Changelog

All notable changes to this package are documented in this file.

## [1.1.34] - 2026-06-07

- Move the URP and HDRP preview bridge modules under `Editor/PreviewSystem` to keep the preview pipeline package layout consolidated.
- Remove the packaged editor test payload from the release trees so the Asset Store and Git UPM distributions stay aligned.
- Refresh the demo scene instructions to match the current thumbnail and custom preview workflow.

## [1.1.33] - 2026-06-07

- Harden URP and HDRP preview light-layer bridge behavior and expand light-layer support tests for safer SRP compatibility across editor variations.
- Add demo scene materials plus new 2D, 3D, local-motion, and travel-motion showcase prefabs for clearer thumbnail and preview examples.
- Retune the local and motion particle demo prefabs to better present playback and framing behavior in the included demo content.

## [1.1.32] - 2026-05-25

- Remove custom UI prefab preview support so `RectTransform`-based prefabs fall back to Unity's native preview path.
- Replace reflection-based SRP light-layer handling with pipeline bridge registration, including package-scoped URP and HDRP bridge implementations.
- Add preview light-layer support tests and normalize text-file line endings with repository `.editorconfig`/`.gitattributes` rules.

## [1.1.31] - 2026-05-22

- In SRP preview modes, use full rendering-layer coverage for preview lights and apply URP additional light rendering/shadow layer fields via guarded reflection.
- Keep built-in pipeline behavior on default rendering-layer masks while preserving safe fallback behavior when URP reflection bindings are unavailable.

## [1.1.30] - 2026-05-22

- Propagate preview light rendering layer masks from model and particle preview sessions into `PreviewLightingSystem` so preview lights target the same rendering layers as preview content.
- Add rendering-layer-aware light setup in preview lighting application to keep lit output consistent across SRP rendering layer configurations.

## [1.1.29] - 2026-05-20

- Skip custom inspector redraw intervention during unsafe editor transitions and fall back to Unity native redraw for safety.
- Harden inspector rebuild reflection calls to classify recoverable null-target exceptions and gracefully fall back instead of propagating failures.

## [1.1.28] - 2026-05-14

- Keep particle intensity-profile analysis renderers hidden throughout setup and simulation so analysis helper objects do not leak into visible preview output.
- Track and clear analysis renderer lists alongside particle analysis state for safer teardown between preview sessions.

## [1.1.27] - 2026-05-14

- Replace fixed-frame UI preview warm-up repaints with a time-based warm-up window (3 seconds) for more consistent first-load stabilization across editor frame rates.
- Stop editor update callbacks automatically once both warm-up and camera motion settle to reduce idle preview repaint churn.

## [1.1.26] - 2026-05-14

- Add short warm-up repaint scheduling for new UI prefab preview targets so first-frame UI card previews settle consistently after setup.
- Track last UI preview target identity and reset warm-up state on cleanup to avoid stale repaint carryover between selections.

## [1.1.25] - 2026-05-14

- Normalize UI prefab `RectTransform` local Z positions to the preview UI plane before layout/render so layered UI cards stay visually consistent in custom preview.
- Remove unused flat-wire bounds mesh resources from `PreviewBoundsVisualizer` to simplify preview bounds rendering internals.

## [1.1.24] - 2026-05-14

- Fix UPM `UiPreviewChecker.shader.meta` GUID alignment so the package uses the expected shader asset mapping in `upm/src`.

## [1.1.23] - 2026-05-14

- Add dedicated UI prefab preview routing for RectTransform-based prefabs (uGUI and TMP UGUI), including model-first precedence for mixed model+UI prefabs.
- Add `UiPrefabPreviewImplementation` and `UiPrefabPreviewSession` with Canvas auto-wrapping, forced layout rebuild before render, and focused 2D camera framing/pan/zoom behavior.
- Add project setting toggle `Draw UI Prefab Custom Preview` with persisted storage support and mirrored `Assets`/`upm` integration.
- Add EditMode coverage for UI target classification and UI preview helper behavior.

## [1.1.22] - 2026-05-12

- Sync UPM `Matcap_01/02/03.png` visual mode assets to the latest package state so Git UPM consumers receive the current matcap textures.

## [1.1.21] - 2026-05-12

- Persist package settings as project assets.
- Remove startup preview diagnostics.
- Defer particle preview intensity profiling.
- Harden inspector redraw preview patch behavior.
- Additional minor preview and thumbnail maintenance updates.

## [1.1.20] - 2026-05-12

- Suspend thumbnail rendering queue work and persistent-cache fetch paths during unsafe editor transition windows (compile/update/playmode switching) to reduce transition-time churn.
- Clear particle systems before assigning deterministic random seeds in particle preview and thumbnail rendering sessions for safer seed resets.

## [1.1.19] - 2026-05-08

- Document the current URP limitation where particle shaders that require the camera opaque texture may render pink in thumbnails and prefab previews.

## [1.1.18] - 2026-05-07

- Add `PreviewEditorTransitionGuard` and gate preview auto-select, inspector repaint, and Harmony preview suppression logic during unsafe compile/update/playmode transitions.
- Harden preview target suppression and property-editor inspection checks to avoid fallback mis-targeting and transition-time reflection failures.
- Add `PreviewHookSafetyTests` coverage for transition guard behavior and preview target-gate fallback rules.

## [1.1.17] - 2026-05-06

- Support clean single-root installs for both Asset Store and Git UPM distributions by resolving bundled preview assets from either `Assets/ParticleThumbnail&Preview` or `Packages/com.fardinhaque.particle-thumbnail-preview`.
- Remove install-time dependency on creating `Assets/ParticleThumbnail&Preview` for preview skybox resources and keep packaged default assets preauthored.
- Prevent preview settings reads from auto-writing project settings while keeping explicit Project Settings edits and resets persisted normally.
- Document the dual-channel distribution workflow and add install-layout coverage tests.

## [1.1.16] - 2026-05-05

- Expand multi-selection guarding in both prefab and model importer preview editors by checking `Selection.count` and editor `targets` count before resolving custom preview ownership.
- Improve preview resolve diagnostics by logging both selection and target counts when multi-selection fallback is applied.
- Update project-level preview/thumbnail settings in this repository to start disabled by default (`ParticlePreviewSettings.active/modelPreviewActive/modelImporterPreviewActive` and `ParticleThumbnailSettings.enabled`).

## [1.1.15] - 2026-05-05

- Guard prefab custom preview resolution against multi-selection states by early-cleaning active preview implementations when `Selection.count != 1`.
- Keep mirrored `Assets` and `upm` `PrefabPreviewEditor` behavior synchronized.

## [1.1.14] - 2026-05-05

- Remove forced hierarchy activation during model preview root instantiation to preserve prefab active-state intent inside preview sessions.
- Keep mirrored `Assets` and `upm` preview session logic synchronized.

## [1.1.13] - 2026-05-04

- Replace in-session collider overlay drawing with a dedicated `ModelColliderOverlayRenderer` for cleaner model preview overlay architecture.
- Improve thumbnail generation queue handling and cache flow behavior in `ParticleThumbnailService` to reduce redundant work and improve responsiveness.
- Tighten thumbnail render size limits in `ParticleThumbnailSettings` for better default performance and memory balance.
- Keep mirrored `Assets` and `upm` implementations synchronized.

## [1.1.11] - 2026-05-04

- Refine model importer preview toolbar behavior by removing the collider toggle in importer contexts and keeping model prefab contexts unchanged.
- Improve model preview toolbar wiring to use configuration-driven control indexing for safer toggle composition and reuse.
- Include mirrored preview pipeline updates across model/particle preview sessions, thumbnail rendering, and compatibility utilities in both `Assets` and `upm`.

## [1.1.10] - 2026-05-04

- Promote current Project Settings preview values to code defaults (`Orbit Smoothing`, `Pan Smoothing`, shared grid alpha, and shared grid fade start scale/padding) so new projects start with the same tuned baseline.
- Keep mirrored `Assets` and `upm` preview settings/code synchronized for consistent package behavior.

## [1.1.9] - 2026-05-04

- Add configurable shared-grid fade start controls (`Scale` and `Padding`) to particle preview settings.
- Route model preview adaptive grid sizing through the new shared-grid fade start settings instead of hardcoded constants.
- Keep mirrored `Assets`/`upm` preview settings and model preview session logic synchronized.

## [1.1.8] - 2026-05-04

- Add adaptive model preview grid sizing based on framed bounds so large prefabs keep useful grid coverage.
- Increase maximum shared grid half size from `50` to `200` to support broader model scales without clipping the preview grid.
- Keep mirrored `Assets` and `upm` preview session/settings changes synchronized.

## [1.1.7] - 2026-05-03

- Update all package/documentation Git URLs to the canonical repository location `Particle-Thumbnail-And-Preview` to avoid redirect-dependent UPM resolution.
- Keep Git UPM install path at `?path=upm/src` with the new canonical repository URL.

## [1.1.6] - 2026-05-03

- Move the UPM package root from `upm/com.fardinhaque.particle-thumbnail-preview` to `upm/src` to simplify Git URL installation paths.
- Update Git UPM install/documentation URLs to `?path=upm/src` for consistent package resolution across projects.
- Update package/documentation metadata and sync tooling to use the new `upm/src` package root.
- Add AGENTS policy requirements to keep UPM path, changelog, package version, and Assets/UPM sync checks aligned before push.

## [1.1.5] - 2026-05-03

- Add model importer preview support and selection flow improvements for model assets.
- Guard preview GUI style initialization so named styles are only bound during active IMGUI events.
- Consolidate preview API naming from particle-prefixed types to generic prefab preview types across mirrored `Assets` and `upm` code.
- Refine model importer auto-selection to respect active model-tab state in importer inspectors.

## [1.1.4] - 2026-05-03

- Harden preview hook safety by adding a bounded Harmony retry budget to prevent unbounded delayed retry scheduling in incompatible editor states.
- Reduce model importer auto-selection update churn so rearm monitoring only stays subscribed while pending work exists.
- Remove stale preview asset fallback roots and keep preview asset loading scoped to current package paths.
- Add actionable compatibility diagnostics to Harmony patch warnings so third-party preview/inspector integration issues are easier to resolve.
- Restore missing editor test assembly/files for preview mode, target classification, and SRP environment behavior checks.
- Document supported Unity baseline (`2021.3+`), SRP coverage, integration notes, and extension guidance.

## [1.1.3] - 2026-05-03

- Remove the `Buildbox` toolbar color preset from preview theme options.
- Add fallback handling for legacy stored toolbar preset values that are no longer defined.
- Sync preview default settings with current project settings for toolbar preset (`Cobalt`), model skybox default (`enabled`), and model ambient light color.

## [1.1.2] - 2026-05-03

- Sync preview default constants with current Project Settings values (toolbar height, shared grid alpha, ambient light, sun intensity, rim intensity).
- Keep mirrored `Assets` and `upm` preview settings/code paths synchronized for matching package behavior.
- Preserve existing particle preview camera pivot initialization behavior in mirrored preview session code.

## [1.1.1] - 2026-05-03

- Fix missing `.meta` for `PreviewAssets/VisualModes` in the UPM package to prevent immutable-folder asset ignore warnings.

## [1.1.0] - 2026-05-03

- Add unified prefab preview routing for particle prefabs and mesh/skinned model prefabs.
- Add model prefab preview with auto-framing, orbit/pan/zoom controls, and shared toolbar behavior.
- Add model preview mode controls with `Auto`, forced `2D`, and forced `3D`, plus project settings (`Enable Model Preview`, `Mode Override`).
- Add EditMode test coverage for preview target classification and preview mode resolution helpers.
- Improve model preview defaults for grid, lighting, and skybox handling across preview contexts.
- Refactor preview asset and toolbar icon loading to keep project and UPM package paths synchronized.
- Fix model preview grid pan anchoring and roll back the broader 2D prefab preview pipeline path to preserve stable behavior.

## [1.0.3] - 2026-05-02

- Stop preview auto-selection from re-running on `projectChanged` refreshes triggered by prefab save and import work.
- Remove the forced internal previewable rebuild path to keep prefab Inspector edits from briefly dropping selection and surfacing third-party shader parsing popups.

## [1.0.2] - 2026-05-02

- Fix prefab Inspector edits briefly clearing the selected prefab and flashing the save prompt when the custom particle preview auto-select refreshes.
- Keep preview auto-selection stable for the same prefab across transient inspector/project refreshes.

## [1.0.1] - 2026-04-26

- Improve README content and UPM install guidance, including updated showcase media.
- Add VFX Graph thumbnail limitation documentation.
- Document scoped Harmony preview hook compatibility.
- Fix UPM immutable package warnings by including missing meta files.
- Remove leftover legacy ImprovedAssetTools project settings from the package.
- Apply early Unity 2022 editor-flow performance tuning.

## [1.0.0] - 2026-04-26

- Initial release of Particle Thumbnail & Preview.
- Added particle prefab thumbnail rendering in Project grid/list views.
- Added memory and persistent cache flow with invalidation on prefab changes.
- Added custom particle prefab Inspector preview with playback controls and timeline scrubbing.
- Added project-scoped settings providers for thumbnail and preview systems.
- Added Git UPM package structure and sync workflow.
