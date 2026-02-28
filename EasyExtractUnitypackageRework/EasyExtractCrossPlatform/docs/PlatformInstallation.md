# EasyExtract Installation Guide

These instructions assume you downloaded the latest release from the official release page.

## Verify Downloads (All Platforms)

1. Keep the checksum file (`SHA256SUMS` or similar) next to the downloaded files.
2. Run the matching command below and compare the reported hash.

    - **Windows (PowerShell)**
      ```pwsh
      Get-FileHash .\EasyExtractCrossPlatform-Setup.exe -Algorithm SHA256
      ```
    - **Linux**
      ```bash
      sha256sum EasyExtractCrossPlatform-2.8.2-linux-x64.AppImage
      ```
    - **macOS**
      ```bash
      shasum -a 256 EasyExtractCrossPlatform-2.8.2-macOS-arm64.zip
      ```

Only continue if the hash matches exactly.

## Windows (Setup.exe)

*Note: We now use Velopack for automatic updates and easier installation.*

1. Download `EasyExtractCrossPlatform-Setup.exe`.
2. Run the executable.
3. The app will install itself automatically to your `%LocalAppData%` folder and create a Desktop Shortcut.
4. It will launch immediately after installation.
5. **SmartScreen Warning:** First launch may trigger SmartScreen because the binary is unsigned—choose “More info” →
   “Run anyway”.

## Linux (AppImage)

*Note: We provide an AppImage for maximum compatibility.*

1. Install `libfuse2` (Required for AppImages on Ubuntu 22.04+):
   ```bash
   sudo apt install -y libfuse2
2. Download `EasyExtractCrossPlatform-2.8.2-linux-x64.AppImage`.
3. Make it executable:

```bash
chmod +x EasyExtractCrossPlatform-2.8.2-linux-x64.AppImage

```

4. Launch:

```bash
./EasyExtractCrossPlatform-2.8.2-linux-x64.AppImage

```

5. The AppImage contains the auto-updater logic inside.

## macOS (macOS-arm64.zip)

1. Unzip the archive; it contains `EasyExtractCrossPlatform.app`.
2. Drag the `.app` into `/Applications` (recommended) or keep it in `~/Applications`.
3. First launch requires approval because the app isn’t notarized yet:

* Open via Finder → right-click → Open → confirm.
* Alternatively run `xattr -d com.apple.quarantine /Applications/EasyExtractCrossPlatform.app`.


4. Start normally afterward via Spotlight or Launchpad.

## Troubleshooting

* **Windows Installer does nothing:** Check if your AntiVirus blocked `Setup.exe`.
* **Missing libraries (Linux):** If the AppImage fails, ensure you have basic GTK libs installed (`libgtk-3-0`,
  `libx11-6`).
* **Display scaling issues:** Avalonia respects OS scaling. Adjust OS-level scaling first; if needed, pass
  `--dpi-awareness=unaware` on Windows.
* **Updater fails to download:** Confirm you’re online and that firewalls allow outbound HTTPS to GitHub.

Need more help? Open an issue on the GitHub repo with logs from `%AppData%\EasyExtractCrossPlatform\logs`
(Windows) or `~/.config/EasyExtractCrossPlatform/logs` (Linux/macOS).
