# Changelog

All notable changes to this package are documented in this file.

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
