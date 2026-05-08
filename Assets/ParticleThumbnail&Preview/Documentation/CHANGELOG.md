# Changelog

All notable changes to this package are documented in this file.

## [1.1.19] - 2026-05-08

- Document the current URP limitation where particle shaders that require the camera opaque texture may render pink in thumbnails and prefab previews.

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
