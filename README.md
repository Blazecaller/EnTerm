# ENterm

A Windows desktop terminal for testing the EN device AT command / test-mode protocol
over a serial (RS-485) connection. Built with .NET 8 WinForms.

## Building the app

Requires the [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0).

```
cd ENTestTerminal
dotnet build
```

Run it with:

```
dotnet run
```

## Building the installer (MSI)

The installer packages a self-contained build (no .NET runtime required on the target machine)
into an MSI that installs the app to Program Files and creates Start Menu and Desktop shortcuts.

Requires PowerShell and the .NET 8 SDK. The [WiX Toolset](https://wixtoolset.org/) CLI and its UI
extension will be installed automatically on first run if missing.

```
cd installer
.\build.ps1
```

The resulting installer is written to `installer/bin/ENterm.msi`.
