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
- **64 languages**, following your system language
- **Native and lightweight**: SwiftUI on macOS, WPF on Windows. No bundled browser engine: a few MB instead of hundreds

## Requirements

- **macOS**: a Mac with Apple Silicon and macOS 13 Ventura or later
- **Windows**: Windows 10 or 11 (nothing else to install: .NET Framework 4.8 is built in)
- The **Adobe Creative Cloud** desktop app, installed and signed in

ZXP Manager uses Adobe's own plugin installer, which comes with the Creative Cloud app. It does not modify Adobe software.

## Download

Get the latest version from **[oddmoth.app/zxp-manager](https://oddmoth.app/zxp-manager/)** or from the [Releases](https://github.com/oddmoth/zxp-manager/releases) page:

| System | File |
|---|---|
| macOS (Apple Silicon) | `ZXP-Manager-macOS-x.y.z.dmg`: open it and drag ZXP Manager to Applications |
| Windows | `ZXP-Manager-Setup-x.y.z.exe`: installer, adds a Start menu entry and opens `.zxp` files with a double-click |
| Windows | `ZXP-Manager-Windows-Portable-x.y.z.zip`: no installation, just unzip and run |

The first time you open it, macOS and Windows ask you to confirm: see the steps below, or the step-by-step guide [How to open ZXP Manager](docs/how-to-open.html), which is also included in the downloads and available in all 64 languages.

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

The app is not yet signed with a code-signing certificate, so Windows SmartScreen may show a blue "Windows protected your PC" window. Click **More info**, then **Run anyway**. Windows asks only once: with the installer the question comes up when you run it, with the portable version when you first open `ZXPManager.exe`.

On computers with **Smart App Control** turned on, unsigned apps are blocked without a "Run anyway" option, so ZXP Manager can't be opened there for now.

## Good to know

- **Photoshop on Apple Silicon Macs** loads `.zxp` panels only when it is opened with Rosetta: in Finder select Photoshop in Applications, choose **File → Get Info** and turn on **Open using Rosetta**. If the extension also comes as a `.ccx` file, install that one instead: it works without Rosetta.
- **Signed extensions only**: Adobe apps load `.zxp` extensions only if they are signed by their developer. An unsigned package can be installed, but the app refuses to open it.
- After installing or removing an extension, restart the Adobe app to see the change.

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

### Publishing a new version

Create a tag such as `v1.0.1` (in GitHub Desktop: **History** → right-click the latest commit → **Create Tag…**, then **Push origin**). The **Release** workflow builds both apps, creates the `.dmg`, the Windows installer and the portable zip, and publishes them with checksums on the Releases page.

To try the packages without publishing anything, open **Actions → Release → Run workflow**.

## Translations

All texts live in [`i18n/`](i18n/), one JSON file per language, shared by every version of the app. `en.json` is the reference.

To fix or improve a translation, edit the file for your language and open a pull request. Please keep placeholders such as `%@` and `%d` unchanged.

The "How to open ZXP Manager" guide keeps its translations inside [`docs/how-to-open.html`](docs/how-to-open.html), one block per language. Each block also lists the names of buttons and settings as macOS and Windows show them in that language: corrections from people using their system in that language are especially helpful. Please keep names in braces such as `{openAnyway}` unchanged. Native speakers' reviews are very welcome, especially for the less common languages.

To see the app in another language without changing your system language:

```sh
# macOS
open -n "/Applications/ZXP Manager.app" --args -AppleLanguages "(de)"

# macOS, right-to-left languages (simulates a Mac set to that language, title bar included)
open -n "/Applications/ZXP Manager.app" --args -AppleLanguages "(ar)" -AppleTextDirection YES -NSForceRightToLeftWritingDirection YES

# Windows (Command Prompt or PowerShell, in the folder of the exe)
.\ZXPManager.exe --lang=de
```

Replace `de` with the code of the language (the name of its file in `i18n/`, e.g. `ja`, `ar`, `zh-Hans`).

## Project structure

```
i18n/      translations (one JSON file per language), shared by both apps
macos/     macOS app (SwiftUI): source code, build script, icon, .dmg layout
windows/   Windows app (WPF, .NET Framework 4.8) and its installer
docs/      "How to open ZXP Manager" guide, included in the downloads
assets/    shared artwork (brand/: Odd Moth logo and icon)
```

## Support

ZXP Manager is free and will stay free. If it saves you time, you can support its development at **[oddmoth.app/support](https://oddmoth.app/support/)**, or simply star this repository.

## Disclaimer

ZXP Manager is an independent project and is not affiliated with, endorsed by, or sponsored by Adobe. Adobe, Creative Cloud, After Effects, Photoshop, Illustrator and the other product names are trademarks of Adobe Inc.

## License

[MIT](LICENSE) © 2026 Odd Moth

---

<p align="center">
  <a href="https://oddmoth.app/">
    <picture>
      <source media="(prefers-color-scheme: dark)" srcset="assets/brand/oddmoth-logo-negative.svg">
      <img src="assets/brand/oddmoth-logo-positive.svg" width="220" alt="Odd Moth">
    </picture>
  </a>
  <br>
  Small native tools for creative work · <a href="https://oddmoth.app/">oddmoth.app</a>
</p>
