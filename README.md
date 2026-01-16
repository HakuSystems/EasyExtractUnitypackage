# EasyExtract.net

<div align="center">
  <img src="https://github.com/user-attachments/assets/84d5c498-2f6b-45f9-895f-ac04471941f1" width="100%" alt="EasyExtract Desktop App"/>
  
  <br/>
  
  [![GitHub Releases](https://img.shields.io/github/downloads/HakuSystems/EasyExtractUnitypackage/total?style=for-the-badge&color=blue)](https://github.com/HakuSystems/EasyExtractUnitypackage/releases)
  [![Discord](https://img.shields.io/discord/11234567890?label=Discord&style=for-the-badge&color=5865F2)](https://discord.gg/Wn7XfhPCyD)
  [![Platform](https://img.shields.io/badge/Platform-Win%20%7C%20Mac%20%7C%20Linux-lightgrey?style=for-the-badge)](https://github.com/HakuSystems/EasyExtractUnitypackage)

</div>

<br/>

## 🚀 What is EasyExtract?

EasyExtract is the ultimate tool to unpack, inspect, and manage Unity packages (`.unitypackage`) **and Zip archives** (`.zip`) containing packages **without ever opening the Unity Editor**. 

Whether you are a developer checking assets or a power user managing a massive library, EasyExtract provides a high-performance, cross-platform solution (built with **Avalonia**) to streamline your workflow.

**Choose your weapon:**
* **Desktop Client (Recommended):** Full offline power, auto-updates, system-wide search, and deep integration.
* **Web App:** Instant, no-install extraction directly in your browser.

---

## 📥 Download & Quick Start

### 🖥️ Desktop App (Windows, macOS, Linux)
*The complete experience with Auto-Updates.*

1.  **Download** the latest release from [GitHub Releases](https://github.com/HakuSystems/EasyExtractUnitypackage/releases).
2.  **Install & Run:**
    * **Windows:** Download and run `EasyExtract-Setup.exe`. It will automatically install to your local app data and launch.
    * **Linux:** Download the `.AppImage`, make it executable (`chmod +x`), and run it.
    * **macOS:** Download the `.zip` or `.pkg` and install/run the App.
3.  **Enjoy:** Drag & drop files, or let the auto-search find packages for you.

*(Check [Installation Docs](https://github.com/HakuSystems/EasyExtractUnitypackage/blob/main/EasyExtractUnitypackageRework/EasyExtractCrossPlatform/docs/PlatformInstallation.md) for detailed steps)*

### 🌐 Web Version
*For quick, on-the-fly access.*

1.  Go to [https://easyextract.net/](https://easyextract.net/).
2.  Drag & drop a `.unitypackage`.
3.  Extract immediately.

---

## 🔥 Desktop Features (Power User)

The Desktop app is built for speed and safety using a modern .NET 9 stack.

### ⚡ Smart Extraction Workflow
* **Auto-Updates:** Integrated **Velopack** keeps your app always up to date with delta patches.
* **Zip Support:** Extract `.unitypackage` files directly from inside `.zip` archives seamlessly.
* **Batch & Queue:** Process hundreds of packages at once via drag-and-drop or CLI (`--extract`).
* **Live Dashboard:** Real-time velocity charts, progress tracking per asset, and "Time Remaining" estimates.

### 🔍 Discovery & Global Search
Stop digging through folders. EasyExtract finds your packages instantly.
* **Windows:** Integrated **Everything SDK** for instant, system-wide results.
* **macOS:** Native **Spotlight** integration.
* **Linux:** Uses `fd`, `plocate`, or `find` to locate assets.
* **Queue Management:** Search results can be queued, previewed, or opened directly.

### 🛡️ Preview & Security (Anti-Malware)
Don't import blind. Know what's inside.
* **Rich Previews:** Inspect images, play audio (WAV/MP3/AIFF with seeking), and render 3D OBJ models *before* extraction.
* **Malicious Code Detection:** Scans assets (up to 4MB) for Discord webhooks, unsafe links, and suspicious DLL patterns/registry hacks.
* **Safety Banners:** Visual warnings in the dashboard if a package contains threats.

### 🎨 UX & Integrations
* **Customizable:** Light/Dark themes, custom background images (local/URL), and opacity controls.
* **Discord Rich Presence:** Show off your extraction velocity and status to friends.
* **System Integration:** Optional Context Menu entry ("Extract with EasyExtract").
* **UwU Mode:** Don't ask, just toggle it in settings.

---

## 🔒 Web App Features

* **Zero Install:** Runs entirely in-browser using modern File System APIs.
* **Security:** Built-in scanner for webhooks and suspicious code patterns before you download.
* **Privacy:** Standard extraction is ephemeral. *EasyExtract Paid* (Gumroad) allows enabling "Local Mode" to keep files private on your machine without server uploads.

---

## ⚔️ Feature Comparison

| Feature | Desktop App (Recommended) | Web Version (Free) |
| :--- | :---: | :---: |
| **Platform** | **Windows, Linux, macOS** | Browser |
| **Setup** | **One-Click Installer (Auto-Update)** | Instant |
| **System Search** | **Yes (Everything / Spotlight)** | No |
| **Zip Extraction** | **Yes** | No |
| **Batch Processing** | **Unlimited Queue** | Limited |
| **Security Scanning** | **Advanced (Deep Scan)** | Basic |
| **Offline Access** | **Yes** | No |
| **Context Menu** | **Yes** | No |
| **Cost** | **Free** (Pro planned) | Free (Paid Add-on optional) |

---

## 📸 Gallery

### Desktop Dashboard
<div align="center">
  <img src="https://github.com/user-attachments/assets/4ad9e235-dedc-4ab0-b346-f22a66c6c304" width="100%" alt="Main Dashboard"/>
</div>

### Settings & Themes
<div align="center">
  <img src="https://github.com/user-attachments/assets/46f547a6-f36c-458c-bb3c-4dff8ef33ed6" width="100%" alt="Settings"/>
</div>

### Web Interface
<div align="center">
  <img src="https://github.com/user-attachments/assets/2790f520-76e0-4a14-8660-16cc11148efc" width="80%" alt="Web Interface" />
</div>

<br/>

### Demos

https://github.com/user-attachments/assets/1d2048e6-e4e3-43e8-b83d-1b1d7f65bd76

https://github.com/user-attachments/assets/7492bc68-1279-4599-b041-3588657e4378

---

## 🤝 Connect & Support

* **Discord:** [Join the Community](https://discord.gg/Wn7XfhPCyD)
* **Issues:** [Report a Bug](https://github.com/HakuSystems/EasyExtractUnitypackage/issues)

Maintained by [HakuSystems](https://github.com/HakuSystems). **Star the repo if it saved you time!** ⭐
