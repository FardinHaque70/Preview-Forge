# Agent Guidelines

## Project Intent
- This project is a Unity Editor extension/utility focused on improving developer workflow.
- The tool must:
  - Render particle system prefab thumbnails in the Project window.
  - Provide a custom preview window for particle prefabs and prefabs using `MeshRenderer`.

## Engineering Standards
- Keep implementation clean, lightweight, and maintainable.
- Prioritize editor safety and responsiveness at all times.
- Do not add runtime-heavy callbacks, polling loops, or frequent editor hooks that can degrade performance.
- Never ship changes that risk slowing down the Unity Editor. This is a strict requirement.
- Maintain professional-grade UX quality appropriate for production editor tooling.
- After each change, remove unused variables, methods, and dead code.
- For Unity Editor feature implementation decisions, verify approach against official Unity Editor documentation instead of guessing.
- Use online documentation lookup only when necessary (for unclear, version-sensitive, or high-risk decisions), not by default for every step.
- When asked to create a Git commit, always use a clear, descriptive commit message that accurately summarizes the changes.

## Code Organization
- Use `#region` blocks to group meaningful, coherent sections (for example: initialization, rendering, caching, cleanup, UI actions).
- Keep regions purposeful; avoid over-fragmentation.

## Comments
- Add brief, meaningful comments where behavior or intent is not immediately obvious.
- Keep comments professional and concise; avoid redundant comments that restate the code.

## Compatibility and Conflict Handling
- Design the tool to coexist with third-party and custom editor tooling, including Odin Inspector and user-defined inspector/preview overrides.
- Detect and handle preview system conflicts gracefully when possible.
- If the custom preview window is overridden or conflicts with another tool, emit a clear, actionable log message.
- Logs should explain likely cause and provide practical remediation options (for example: change script execution/registration order, adjust integration settings, or temporarily disable this tool's preview subsystem).
- Treat compatibility as a release-blocking requirement: support Unity `2021.x` and newer.
- Ensure features work across built-in render pipeline, URP, and HDRP wherever technically feasible.
- When implementing a feature, prefer cross-version and cross-pipeline-safe APIs/patterns over version-specific shortcuts.
- If compatibility of a planned feature is uncertain, do not assume it works: validate first and provide safer alternative approaches or fallbacks.

## Packaging and Isolation
- Use clear, consistent C# namespaces to contain this tool's code and avoid collisions.
- Use assembly definition files (`asmdef` / "asmdf" as requested) to isolate editor-only code.
- Keep editor tooling in editor-only assemblies so it does not introduce runtime build dependencies or runtime performance overhead.
- Versioning is release-blocking:
  - Canonical UPM package root is `upm/src`.
  - Public Git UPM install URL must use `?path=upm/src` (no leading slash path variant).
  - When package behavior/code/docs changes are release-worthy and `CHANGELOG` gets a new top entry, update `upm/src/package.json` `version` to the same version in the same commit.
  - Before every push that includes package changes, ensure `Assets/ParticleThumbnail&Preview` and `upm/src/ParticleThumbnail&Preview` are synced and all install/docs links point to `upm/src`.
  - Never leave `CHANGELOG` and `package.json` versions mismatched.
