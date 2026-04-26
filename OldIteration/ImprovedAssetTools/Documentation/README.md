# Improved Thumbnail and Preview

This package includes two editor-only Unity workflow tools:

- `Improved Thumbnail`
- `Improved Preview`

## Improved Thumbnail

- Generates static custom thumbnails for supported assets.
- Supported sources:
  - particle prefabs (root `ParticleSystem`)
  - UI prefabs (`uGUI`; TMP-aware badge)
  - general prefabs and prefab variants
  - model assets (`.fbx`, `.obj`, `.blend`, `.dae`)
  - material assets (`.mat`)
- Draw targets:
  - Project Grid view
  - Project List view
  - Object Picker (via Harmony patch integration)
- Includes provider priority ordering, per-type toggles, badges, and cache controls.
- Uses in-memory and persistent disk cache (`Library/ImprovedThumbnailCache`).

## Improved Preview

- Adds custom Inspector previews for Particle Systems, GameObjects and Materials .
- Particle System preview includes playback, scrubbing, and motion path controls.
- GameObject preview types:
  - model prefab preview (stats, bounds, visual modes, animation clip playback)
  - particle prefab preview (playback + scrub + motion path controls)
  - sprite prefab preview (2D-oriented)
  - UI prefab preview (2D-oriented with UI stats)
  - non-visual prefab placeholder view
- Material preview includes mesh mode selection (sphere/cube/torus/quad) and environment toggles.

## Render Pipeline Behavior

- Built-in and URP 3D support full preview controls.
- URP 2D (and Editor 2D fallback contexts) disable unsupported features such as skybox/reflection/custom light rig.

## Tested Environment

- Unity `6000.3.10f1`
- Editor platform: macOS
- Authored and validated in URP; fallback behavior handled for Built-in and 2D compatibility contexts
- `com.unity.ugui` is required for UI-specific thumbnail/preview behavior

## Package Contents

- `Assets/ImprovedAssetTools/Common`
- `Assets/ImprovedAssetTools/ImprovedThumbnail`
- `Assets/ImprovedAssetTools/ImprovedPreview`
- `Assets/ImprovedAssetTools/Samples`
- `Assets/ImprovedAssetTools/Documentation`

## Important Notes

- Editor-only package; no player build runtime impact.

## Third-Party Dependency

- Includes Harmony (`0Harmony.dll`) for editor patching support.
- See `THIRD_PARTY_NOTICES.md` for attribution and license details.
