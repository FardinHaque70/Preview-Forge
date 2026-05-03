# Changelog

All notable changes to this package are documented in this file.

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
- Sync preview default settings with current project settings for toolbar preset (`UnityBlue`), model skybox default (`enabled`), and model ambient light color.

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
