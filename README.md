# WindowSwitch

WindowSwitch is a lightweight Windows virtual desktop switcher built with WPF. It provides a compact floating overlay for switching desktops, creating or closing virtual desktops, and using keyboard or mouse activation shortcuts.

## Features

- Floating overlay for Windows virtual desktops.
- Click a desktop tile to switch to that desktop.
- Bottom action buttons for common virtual desktop commands:
  - Open Task View
  - Create desktop
  - Switch left
  - Switch right
  - Close current desktop
- Configurable columns per row.
- Optional colored desktop labels.
- Configurable window opacity.
- Optional auto-hide after switching.
- Keyboard hotkey activation.
- Mouse button activation with slide-to-select gesture.
- Desktop number hotkeys, including two-digit desktop selection.
- Automatic overlay height fitting based on desktop rows and action buttons.
- Double-click top or bottom resize border to fit the window height to its content.
- Tray icon with show and exit actions.

## Requirements

- Windows 10 19041 or later.
- .NET 8 SDK for building from source.

The app uses WPF and Windows Forms interop, plus the `Slions.VirtualDesktop.WPF` package for virtual desktop integration.

## Build

```powershell
dotnet restore WindowSwitch.sln
dotnet build WindowSwitch.sln -c Release
```

## Test

```powershell
dotnet test WindowSwitch.sln --no-restore
```

## Publish

```powershell
dotnet publish WindowSwitch/WindowSwitch.csproj -c Release -r win-x64 --self-contained false
```

The published output is written under:

```text
WindowSwitch/bin/Release/net8.0-windows10.0.19041.0/win-x64/publish/
```

## Development Workflow

This repository uses local Git hooks from `.githooks`.

Configure them once per clone:

```powershell
git config core.hooksPath .githooks
```

Pre-commit currently runs formatting and tests:

```powershell
dotnet format WindowSwitch.sln --verify-no-changes --no-restore
dotnet test WindowSwitch.sln --no-restore
```

Post-commit checks that no C# code file exceeds 300 lines:

```powershell
python scripts/check_code_file_length.py --max-lines 300
```

If that check fails, split the oversized file and commit the refactor separately.

## Repository Notes

- Build outputs and local artifacts are ignored through `.gitignore`.
- Do not commit `bin/`, `obj/`, `.vs/`, `TestResults/`, publish output, logs, or temporary files.
- Keep commits small and focused.
