# Win x64 Native AOT package

## Goal

Provide a repeatable Windows x64 Native AOT package at
`mcsl-future-win-x64-aot` with this layout:

```text
mcsl-future-win-x64-aot/
|- daemon/MCServerLauncher.Daemon.exe
`- winui/
   |- MCServerLauncher.WinUI.exe
   `- MCServerLauncher.DaemonClient.exe
```

The package must keep the WinUI runtime assets required by the unpackaged
WinUIIslands application, remove unnecessary managed DLL output, and avoid
publishing unused satellite language folders while preserving runtime
localization behavior.

## Touched areas

- `workflow`: reusable Windows x64 AOT publish entry point and package layout.
- `backend`: daemon Native AOT publish settings and trim-safe output.
- `frontend`: WinUI Native AOT publish settings and localized resource policy.
- `storage`: WinUI AOT-safe local settings and daemon configuration persistence.
- `integrations`: daemon client executable Native AOT output.

## Implementation

- Confirm Native AOT compatibility for daemon, daemon client, and WinUI.
- Add explicit publish properties for the three projects without changing
  ordinary Debug/Release development builds.
- Add a PowerShell publisher that creates the requested directory layout,
  publishes all three executables, and retains only required WinUI runtime
  files and the selected language resources.
- Document the command and the resulting package contract near the WinUI
  publish instructions.

## Verification

- Publish all three projects with `PublishAot=true`, `SelfContained=true`,
  `PublishSingleFile=true`, and `PublishTrimmed=true` where supported.
- Confirm the package contains the three requested executables and no
  application managed DLLs in `winui` beyond selected satellite resource
  assemblies.
- Confirm WinUI keeps its required `resources.pri`, native runtime assets, and
  only the selected language resource set.
- Run the daemon and daemon client startup checks, and launch WinUI when the
  current Windows session permits UI validation.
- Run the relevant project builds, protocol tests, `git diff --check`, and
  inspect repository status.

## Changelog

- Added `scripts/Publish-WinX64Aot.ps1`, which publishes daemon, daemon client,
  and WinUI as self-contained Native AOT win-x64 executables.
- Added the `mcsl-future-win-x64-aot/daemon` and `winui` package layout. WinUI
  is staged from a runtime-file whitelist and defaults to `en-US` plus
  `zh-CN`; `-AllLanguages` remains available.
- Removed the Native AOT-incompatible Windows CIM dependency from daemon
  system/process status paths and aligned TouchSocket packages at `4.2.17`.
- Fixed the WinUI first-run completion flow by replacing reflection-based JSON
  persistence in `SettingsStore` and `DaemonStore` with the generated
  `WinUiJsonContext`; AOT can now create and save settings during language
  selection, daemon setup, and the final Continue action.
- Fixed WinUI AOT XAML binding for `RefreshIntervalOption.Seconds` with
  `WinRT.GeneratedBindableCustomPropertyAttribute`.
- Replaced remaining download, event-rule, console-log, Forge installer, and
  daemon-client inbound JSON reflection paths with generated metadata or
  explicit JSON element parsing. Protocol enums now use an AOT-safe
  lower-snake-case converter, preserving explicit event payload `null` values.
- Marked create-instance WinRT controls and providers `partial`, removing the
  CsWinRT AOT metadata warnings from the WinUI publish.
- Verified daemon startup and WebSocket RPC with the final AOT executables,
  WinUI process startup, WPF/WinUI/daemon builds, and 366 protocol tests.
