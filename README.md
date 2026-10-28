<p align="center">
  <img src="assets/icon-freeform.png" width="160" alt="ZXP Manager icon">
</p>

<h1 align="center">ZXP Manager</h1>

<p align="center">
  Install, list and remove extensions for Adobe Creative Cloud apps.<br>
  Native on macOS (Apple Silicon) and Windows. Free, open source and tiny.
</p>

---

## Features

- **Install** `.zxp` and `.ccx` extensions by dragging them onto the window (or onto the app icon), or with the **+** button
- **Preview before installing**: name, version and target apps (After Effects, Photoshop, Illustrator…) are read from the package, and you're told if the extension is already installed
- **See everything that is installed**, grouped by Adobe app
- **Remove** extensions with one click
- **38 languages**, following your system language
- **Native and lightweight**: SwiftUI on macOS, WPF on Windows. No bundled browser engine: a few MB instead of hundreds

## Requirements

- **macOS**: a Mac with Apple Silicon and macOS 13 Ventura or later
- **Windows**: Windows 10 or 11 (nothing else to install: .NET Framework 4.8 is built in)
- The **Adobe Creative Cloud** desktop app, installed and signed in

ZXP Manager uses Adobe's own plugin installer, which comes with the Creative Cloud app. It does not modify Adobe software.

## Download

Get the latest version from the [Releases](https://github.com/oddmoth/zxp-manager/releases) page.

### Opening the app the first time on macOS

ZXP Manager is free and open source, and it is not yet notarized by Apple. For this reason macOS shows a warning the first time you open it. The code is public in this repository, so anyone can check what it does.

To open it:

1. Open ZXP Manager. macOS says it can't verify the app: click **Done**.
2. Open **System Settings → Privacy & Security**, scroll down and click **Open Anyway** next to ZXP Manager.
3. Confirm with your password. From now on the app opens normally.

Alternatively, in Terminal:

```sh
xattr -dr com.apple.quarantine "/Applications/ZXP Manager.app"
```

### Opening the app the first time on Windows

The app is not yet signed with a code-signing certificate, so Windows SmartScreen may show a blue "Windows protected your PC" window. Click **More info**, then **Run anyway**. No installation is needed: `ZXPManager.exe` runs as is, from any folder.

## Build from source

Both apps are built automatically by GitHub Actions at every change: open the **Actions** tab, choose the latest **macOS build** or **Windows build** run and download the file at the bottom of the page.

### macOS

Requirements: Xcode Command Line Tools (`xcode-select --install`). Full Xcode is optional: if present, it is used to compile the adaptive icon for macOS 26.

```sh
git clone https://github.com/oddmoth/zxp-manager.git
cd zxp-manager/macos
zsh build.sh
```

The app is created in `macos/build/`.

### Windows

To build it yourself, install the [.NET SDK](https://dotnet.microsoft.com/download) and run:

```sh
dotnet build windows/ZXPManager.csproj --configuration Release --output out
```

## Translations

All texts live in [`i18n/`](i18n/), one JSON file per language, shared by every version of the app. `en.json` is the reference.

To fix or improve a translation, edit the file for your language and open a pull request. Please keep placeholders such as `%@` and `%d` unchanged. Native speakers' reviews are very welcome, especially for the less common languages.

## Project structure

```
i18n/      translations (one JSON file per language), shared by both apps
macos/     macOS app (SwiftUI): source code, build script, icon
windows/   Windows app (WPF, .NET Framework 4.8)
assets/    shared artwork
```

## Disclaimer

ZXP Manager is an independent project and is not affiliated with, endorsed by, or sponsored by Adobe. Adobe, Creative Cloud, After Effects, Photoshop, Illustrator and the other product names are trademarks of Adobe Inc.

## License

[MIT](LICENSE) © 2026 Odd Moth
