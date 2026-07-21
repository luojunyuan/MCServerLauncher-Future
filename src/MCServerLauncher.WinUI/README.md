# MCServerLauncher.WinUI

The optional WinUIIslands desktop client for MCServerLauncher Future.

This project is an independent client. It hosts WinUI 2 XAML through
WinUIIslands and keeps the WPF client as the behavioral reference. The WPF
project is deliberately not referenced and must remain unchanged.

## Fixed platform constraints

- Target framework: `net10.0-windows10.0.26100.0`.
- Minimum Windows version: `10.0.18362.0`.
- Runtime identifiers: `win-x64` and `win-arm64` only.
- Deployment: self-contained, unpackaged file-system folders (`.exe`), with
  no package identity, MSIX bundle, or `Package.appxmanifest`.
- UI host: `WinUIIslands` + `Microsoft.UI.Xaml` 2.8.7 (WinUI 2).
- Editor: `WinUIEdit.Uwp` `0.0.5-prerelease`; do not reintroduce AvalonEdit.
- Do not add Windows App SDK/WinUI 3 references, WPF references, or WPF
  assemblies.

`UseUwp=true` is required by the WinUIIslands XAML build. The Windows SDK may
restore build-time PRI/XAML tooling packages, but they are not runtime
dependencies and no package artifacts are published.

## Project references and packages

The only project references are:

- `../MCServerLauncher.Common/MCServerLauncher.Common.csproj`
- `../MCServerLauncher.DaemonClient/MCServerLauncher.DaemonClient.csproj`

Important packages include `WinUIIslands`, `Microsoft.UI.Xaml`,
`WinUIEdit.Uwp`, `CommunityToolkit.Mvvm`, `Serilog`, `Downloader`, and
`Microsoft.Extensions.DependencyInjection`.

## Shell and folders

`MainWindow` is intentionally only the native WinUIIslands window shell. It
creates the window, applies the backdrop and minimum size, hosts `MainPage`,
and wires the title bar. The real application root belongs in `MainPage`:

- startup loading overlay and startup-error surface;
- first-setup selection and completion;
- title-bar download-history flyout;
- `NavigationView`, content `Frame`, and notification `InfoBar`;
- compiled `x:Bind` expressions and runtime localization.

The active folders follow the retained WPF feature areas:

```text
View/Pages/                 Main pages
View/Components/            Reusable page controls
View/Features/              Create-instance feature flow and providers
View/FirstSetupHelper/      Language, EULA, daemon, and welcome steps
ViewModels/                 MVVM state and daemon commands
InstanceConsole/View/       Board, command, file, component, settings, event pages
InstanceConsole/Editing/    IEditorAdapter and WinUIEdit adapter
Core/Localization/          ResourceManager-based runtime localization
Core/Services/              Dialog, daemon, navigation, theme, picker, clipboard, notification
Core/Storage/               Compatible settings, daemon, and data-directory storage
Resources/                  Application images, icons, and font assets
```

Do not create empty folders solely to mirror WPF. Add a folder when its
corresponding workflow is actually migrated.

## Localization

The six WPF resource files are the single source of truth and are linked into
this project by `MCServerLauncher.WinUI.csproj`:

```text
../MCServerLauncher.WPF/Translations/Lang.en-US.resx
../MCServerLauncher.WPF/Translations/Lang.ja-JP.resx
../MCServerLauncher.WPF/Translations/Lang.ru-RU.resx
../MCServerLauncher.WPF/Translations/Lang.zh-CN.resx
../MCServerLauncher.WPF/Translations/Lang.zh-HK.resx
../MCServerLauncher.WPF/Translations/Lang.zh-TW.resx
```

Do not copy these files or add a WPF project reference. `LocalizationService`
uses .NET `ResourceManager`; `LocalizedStrings` raises `PropertyChanged` for
the indexer and language-change events so compiled bindings refresh in place.

User-facing XAML text must use compiled bindings, for example:

```xml
Text="{x:Bind Texts['Main_HomeNavMenu'], Mode=OneWay}"
```

Do not use `{Binding}`, `x:Uid`, or `x:Static` as the source of WinUI UI text.
The lookup order is current culture, `en-US`, then the resource key itself.
Changing language updates `CurrentCulture`, `CurrentUICulture`, the settings
file, and all active localized view-model properties without restarting.

## WPF interaction parity

The WPF workflow is the compatibility contract. Port platform APIs and
controls, but do not simplify or redesign the interaction sequence.

First setup remains:

1. Language
2. EULA
3. Daemon
4. Welcome

Navigation remains locked until each preceding step completes. The EULA keeps
the localized external browser URL, disabled 15-second accept countdown,
original refuse/accept confirmation dialogs, and original button meanings.
Daemon setup keeps local/remote selection, `ws://`/`wss://` validation,
skip confirmation, add/edit/remove/reconnect behavior, and existing-host
loading.

The retained workflows include daemon management, instance filtering and
lifecycle actions, all supported create-instance providers, resource
downloads and retry/history, settings and themes, help/debug, file transfer,
event rules, component management, and the independent instance console.
Persisted JSON field names, daemon action/event protocols, endpoint rules, and
path-safety behavior must remain compatible with WPF and the daemon.

## Storage and services

The client first uses the compatible writable directory:

```text
<application base>\Data\Configuration\MCSL
```

If it is not writable, it falls back to `%LOCALAPPDATA%\MCServerLauncher-Future\Data`.
Existing `Settings.json`, `Daemons.json`, and related data are imported or
copied without deleting the old files. Settings and daemon JSON writes use
serialized asynchronous, atomic replacement. The legacy
`App.IsFontInstalled` field remains round-trippable even though this client
does not install fonts.

Platform services live under `Core/Services` and replace WPF dispatchers,
dialogs, browser launch, clipboard, file pickers, themes, notifications, and
daemon connection handling. Unpackaged system notifications are best effort;
the in-app `InfoBar` is always the fallback.

## Instance console and editor

The console is a separate WinUIIslands window with Board, Command, File Manager,
Component Manager, Instance Settings, and Event Trigger views.

`IEditorAdapter` is the only place that knows the WinUIEdit API. It exposes
load/read/set text, language and encoding, undo/redo, clipboard, zoom, line
numbers, save/reload, and file-transfer support. Scintilla uses UTF-8
internally; selected external encodings are used for file I/O. Extension
highlighting is mapped centrally (`csharp`, `json`, `xml`, `yaml`, `java`,
`javascript`, `batch`, `shell`, or `text`).

The editor retains the WPF behaviors for large-file warnings, unsaved-change
confirmation, save retry, reload, encoding changes, search, zoom, line
numbers, theme changes, and daemon upload/download.

## Build, run, and publish

From the repository root:

```powershell
# Publish self-contained x64 and ARM64 folders
& scripts\Publish-WinUI.ps1 -Architecture @('x64','ARM64') -Configuration Release

# Run the first-setup and shell UI automation against the x64 publish
& scripts\Test-WinUIFirstSetup.ps1
```

The publish script restores each RID, stages the WinUIIslands/WinUI 2 and
WinUIEdit native runtime, copies the application `resources.pri`, and fails
if required runtime files are missing or a package manifest is present.

## Verification checklist

Use the smallest relevant checks for a change, and keep the WPF project
untouched:

```powershell
dotnet build src\MCServerLauncher.WPF\MCServerLauncher.WPF.csproj /m:1
dotnet test tests\MCServerLauncher.ProtocolTests\MCServerLauncher.ProtocolTests.csproj -c Release /m:1
& scripts\Publish-WinUI.ps1 -Architecture @('x64','ARM64') -Configuration Release
```

Also check that the WinUI project contains no WPF/WASDK/WinUI 3/AvalonEdit
references, no `{Binding}` or `x:Uid`, no x86 publish output, and no MSIX/AppX
artifacts. Run `git diff --check` and preserve CRLF line endings in all text
files.
