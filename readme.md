## Internet Speed Monitor

A lightweight desktop app built with C# (.NET) and WPF that shows your current internet upload/download speeds. It also provides a click-through, always-on-top overlay that you can position on screen, customize, and persist settings across launches.

### Features

- Always-on-top overlay with click-through behavior
- Live upload/download speeds (from Windows performance counters)
- Position presets: Top Left, Top Center, Top Right, Bottom Left, Bottom Center, Bottom Right
- Text customization: color, font family, size, style (Regular, Bold, Italic, Bold Italic, Sharp)
- Background options: No Background (transparent) or colored translucent background
- System tray support (minimize to tray)
- Settings persistence in AppData

### Tech Stack

- Language: C# (.NET)
- UI: WPF
- Windows-only: Uses `System.Management` and WMI performance counters
