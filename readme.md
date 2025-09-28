## Internet Speed Monitor

A lightweight desktop app built with C# (.NET) and WPF that displays your current internet upload and download speeds. You can choose your preferred unit of measurement. It also offers a click-through, always-on-top overlay that you can position anywhere on the screen, customize, and have persist across launches.

### Features

- Always-on-top overlay with click-through behavior
- Live upload/download speeds (from system network statistics)
- Position presets: Top Left, Top Center, Top Right, Bottom Left, Bottom Center, Bottom Right
- Text customization: color, font family, size, style (Regular, Bold, Italic, Bold Italic, Sharp)
- Background options: No Background (transparent) or colored translucent background
- System tray support (minimize to tray)
- Settings persistence in AppData

### Download
You can download the installer here: [Internet Speed Monitor Releases](https://github.com/iamxeeshankhan/InternetSpeedMonitor/releases)  
No additional software is required - simply install and run the application via the desktop shortcut.  
This app is available for Windows (x86 and x64) only.

### Tech Stack

- Language: C# (.NET)
- UI: WPF
- Windows-only: Uses `System.Net.NetworkInformation` (with optional WMI)

**Notes:** 
- This application displays the total internet speed of your entire PC — not just the speed of a specific browser, downloader, torrent client, or application. It measures the overall upload and download traffic coming in and out of your system. As a result, the reported speed may differ from what you see in individual applications such as your browser, download manager, or torrent client.

- When you start this application, the internet speed shown may be inaccurate at first. Please allow 1–2 minutes for it to stabilize. After that, it will display the accurate incoming and outgoing traffic of your PC or laptop.