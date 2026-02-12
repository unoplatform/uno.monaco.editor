# fn-10-fix-reverted-emitter-docs-enable-cdp-ci.4 Add WSL2 desktop launch profile with display env vars

## Description
Add a VSCode launch profile and launchSettings.json profile for running the desktop Skia app under WSL2 with the required display environment variables.

**Size:** S
**Files:** `.vscode/launch.json` (create if absent), `MonacoEditorTestApp/Properties/launchSettings.json`

## Approach
- **Create `.vscode/launch.json`** if it does not exist. Use minimal, non-destructive config. If it already exists, add the new configuration without modifying existing entries.
- Add a configuration named "Uno Platform Desktop Debug (WSL2)"
- Target `net10.0-desktop` with coreclr debugger
- Set environment variables: `DISPLAY=:0` (WSLg X11 display), `GDK_GL=gles` (GLES rendering for WSL2 compatibility)
- Add a corresponding profile to `MonacoEditorTestApp/Properties/launchSettings.json` with the same env vars
- Follow the existing profile patterns in both files
- WSLg (Windows 11+) auto-sets DISPLAY but explicit config ensures it works in all setups
## Acceptance
- [ ] `.vscode/launch.json` exists (created if it was absent) with a "Uno Platform Desktop Debug (WSL2)" configuration
- [ ] Configuration targets `net10.0-desktop` with `coreclr` debugger type
- [ ] `DISPLAY` and `GDK_GL` environment variables set in the profile
- [ ] `launchSettings.json` has a WSL2 profile with matching env vars
- [ ] Existing launch profiles unchanged
## Completion summary
TBD

## Evidence
- Commits:
- Tests:
- PRs:
