# EasyExtract Installation Guide

These instructions assume you downloaded a release bundle named
`EasyExtractCrossPlatform-<version>-<platform>-<arch>.<zip|tar.gz>` plus the accompanying
`EasyExtractCrossPlatform-<version>-sha256.txt` file from the official release page.

## Verify Downloads (All Platforms)

1. Keep the checksum file next to the archives.
2. Run the matching command below and compare the reported hash with the entry in the `.txt`.

    - **Windows (PowerShell)**
      ```pwsh
      Get-FileHash .\EasyExtractCrossPlatform-2.0.7.5-win-x64.zip -Algorithm SHA256
      ```
    - **Linux**
      ```bash
      sha256sum EasyExtractCrossPlatform-2.0.7.5-linux-x64.tar.gz
      ```
    - **macOS**
      ```bash
      shasum -a 256 EasyExtractCrossPlatform-2.0.7.5-macOS-arm64.zip
      ```

Only continue if the hash matches exactly.

## Windows (win-x64.zip)

1. Extract the `.zip` anywhere you have write access (e.g., `C:\Tools\EasyExtract`).
2. Inside the extracted folder run `EasyExtractCrossPlatform.exe`.
3. Optional: create a shortcut to the EXE and pin it to Start/Taskbar.
4. First launch may trigger SmartScreen because the binary is unsigned—choose “More info” → “Run anyway”.
5. For automatic updates, keep the entire extracted folder intact; the updater writes temporary files there.

## Linux (linux-x64.tar.gz)

1. Install Avalonia’s native deps (Ubuntu example):
   ```bash
   sudo apt install -y libgtk-3-0 libwebkit2gtk-4.1 libxcb1 libx11-6 libxrender1 libxcomposite1 libasound2
   ```
   Fedora/RHEL: `sudo dnf install gtk3 webkit2gtk4.1 xcb-util libX11 libXrender libXcomposite alsa-lib`.
2. Extract the archive:
   ```bash
   tar -xzf EasyExtractCrossPlatform-2.0.7.5-linux-x64.tar.gz
   cd EasyExtractCrossPlatform-2.0.7.5-linux-x64
   ```
3. Ensure the main binary is executable:
   ```bash
   chmod +x EasyExtractCrossPlatform
   ```
4. Launch:
   ```bash
   ./EasyExtractCrossPlatform
   ```
5. (Optional) Add a desktop entry by creating `~/.local/share/applications/easyextract.desktop`
   pointing to the extracted path if you want menu integration.

## macOS (macOS-arm64.zip)

1. Unzip the archive; it contains `EasyExtractCrossPlatform.app`.
2. Drag the `.app` into `/Applications` (recommended) or keep it in `~/Applications`.
3. First launch requires approval because the app isn’t notarized yet:
    - Open via Finder → right-click → Open → confirm.
    - Alternatively run `xattr -d com.apple.quarantine /Applications/EasyExtractCrossPlatform.app`.
4. Start normally afterward via Spotlight or Launchpad.
5. Updating replaces the app bundle automatically; ensure the `.app` isn’t write-protected.

## Troubleshooting

- **Missing libraries (Linux):** if the app fails with GTK/WebKit errors, re-run package installation for
  `libgtk-3`, `libwebkit2gtk-4.1`, and `libxcb`.
- **Display scaling issues:** Avalonia respects OS scaling. Adjust OS-level scaling first; if needed, pass
  `--dpi-awareness=unaware` on Windows or `QT_SCALE_FACTOR=1.25` equivalents on Linux/macOS.
- **Updater fails to download:** confirm you’re online and that firewalls allow outbound HTTPS to GitHub.
- **Permissions errors:** make sure the extracted directory isn’t read-only. On Linux/macOS, keep
  execute permission on the main binary (`chmod +x`).

Need more help? Open an issue on the GitHub repo with logs from `%AppData%/EasyExtractCrossPlatform/logs`
(Windows) or `~/.config/EasyExtractCrossPlatform/logs` (Linux/macOS).
