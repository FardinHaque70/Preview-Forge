# Changelog

All notable changes to this package are documented in this file.

## [1.1.0] - 2026-05-02

- Add unified custom prefab preview routing for particle and model prefabs.
- Add lean mesh/skinned model preview with robust framing and orbit/pan/zoom interaction.
- Add shared preview mode architecture with `Auto`, forced `2D`, and forced `3D` mode support for model previews.
- Generalize preview target gating and competing-preview suppression to classifier-driven prefab support.
- Add model preview settings (`Enable Model Preview`, `Mode Override`) in project settings.
- Add EditMode tests for target classification and preview mode resolution helpers.

## [1.0.3] - 2026-05-02

- Stop preview auto-selection from re-running on `projectChanged` refreshes triggered by prefab save and import work.
- Remove the forced internal previewable rebuild path to keep prefab Inspector edits from briefly dropping selection and surfacing third-party shader parsing popups.

## [1.0.2] - 2026-05-02

- Fix prefab Inspector edits briefly clearing the selected prefab and flashing the save prompt when the custom particle preview auto-select refreshes.
- Keep preview auto-selection stable for the same prefab across transient inspector/project refreshes.

## [1.0.1] - 2026-04-26

- Minor update
- performance optimization attempt for unity 2022
- Push all pending updates including docs, preview changes, and old iteration assets
- Add VFX Graph thumbnail limitation note to README
- Document scoped Harmony preview hook compatibility
- Update particle thumbnail workflow and simplify package docs
- Remove tag guidance from user-facing README install docs
- Clarify UPM install guidance before release tags
- Remove leftover ImprovedAssetTools project settings
- Fix UPM immutable package warnings with meta files
- Update README showcase gifs and thumbnail settings

## [1.0.0] - 2026-04-26

- Initial release of Particle Thumbnail & Preview.
- Added particle prefab thumbnail rendering in Project grid/list views.
- Added memory and persistent cache flow with invalidation on prefab changes.
- Added custom particle prefab Inspector preview with playback controls and timeline scrubbing.
- Added project-scoped settings providers for thumbnail and preview systems.
- Added Git UPM package structure and sync workflow.
