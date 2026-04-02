# EasyExtract Installation Guide

These instructions assume you downloaded the latest release from the
[official release page](https://github.com/HakuSystems/EasyExtractUnitypackage/releases/latest).

## Windows

### Setup (recommended)

1. Download `EasyExtractCrossPlatform-win-Setup.exe`.
2. Run the executable — it installs automatically to `%LocalAppData%` and creates a Desktop shortcut.
3. The app launches immediately after installation and auto-updates itself.
4. **SmartScreen Warning:** First launch may trigger SmartScreen because the binary is unsigned — choose
   "More info" → "Run anyway".

### Portable

1. Download `EasyExtractCrossPlatform-win-Portable.zip`.
2. Extract anywhere and run `EasyExtractCrossPlatform.exe`.

## Linux (AppImage)

1. Install `libfuse2` (required on Ubuntu 22.04+):
   ```bash
   sudo apt install -y libfuse2
   ```
2. Download `EasyExtractCrossPlatform.AppImage`.
3. Make it executable:
   ```bash
   chmod +x EasyExtractCrossPlatform.AppImage
   ```
4. Launch:
   ```bash
   ./EasyExtractCrossPlatform.AppImage
   ```
5. The AppImage includes auto-updater logic.

## macOS (arm64)

### Installer (recommended)

1. Download `EasyExtractCrossPlatform-osx-Setup.pkg`.
2. Double-click the `.pkg` to install.
3. First launch requires approval because the app isn't notarized:
   - Open via Finder → right-click → Open → confirm.
   - Or run: `xattr -d com.apple.quarantine /Applications/EasyExtractCrossPlatform.app`
4. Start normally afterward via Spotlight or Launchpad.

### Portable

1. Download `EasyExtractCrossPlatform-osx-Portable.zip`.
2. Unzip the archive and drag `EasyExtractCrossPlatform.app` into `/Applications`.
3. Follow the same quarantine approval steps as above.

## Troubleshooting

* **Windows installer does nothing:** Check if your antivirus blocked `Setup.exe`.
* **Missing libraries (Linux):** Ensure you have `libgtk-3-0` and `libx11-6`.
* **Display scaling issues:** Avalonia respects OS scaling. Adjust OS-level scaling first; if needed, pass
  `--dpi-awareness=unaware` on Windows.
* **Updater fails to download:** Confirm you're online and that firewalls allow outbound HTTPS to GitHub.

Need more help? Open an issue on the GitHub repo with logs from `%AppData%\EasyExtractCrossPlatform\logs`
(Windows) or `~/.config/EasyExtractCrossPlatform/logs` (Linux/macOS).
