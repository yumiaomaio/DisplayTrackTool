# Immersive Window Control

A precision window management tool designed to force specific aspect ratios and immersive display settings for target applications. Built with a WPF backend and a modern Vue 3 frontend.

## Features

- **Aspect Ratio Override**: Force target windows into specific ratios (e.g., 9:16 Portrait) with automatic centering.
- **Background Overlay**: Fill screen gaps with solid colors or custom images placed behind the target window.
- **Taskbar Integration**: Automatically hides the Windows Taskbar when monitoring starts and restores it on exit.

## Build Instructions

This project consists of a Vue 3 frontend and a .NET WPF backend. The frontend must be built first as the backend bundles the output.

### 1. Prerequisites
- **Node.js** (v18+)
- **.NET 10 SDK**
- **Visual Studio 2022** or **JetBrains Rider**

### 2. Build the Frontend
Navigate to the `vite-project` directory to install dependencies and build the Web UI:
```bash
cd vite-project
npm install
npm run build
```
The build process will automatically deploy the single-file bundle to `ImmersiveWindow/WebUI/index.html`.

### 3. Build the Backend
Open the solution file `ImmersiveWindow.sln` in your IDE or use the .NET CLI:
```bash
# From the root directory
dotnet build -c Release
```

### 4. Run
After building, you can run the executable found in `ImmersiveWindow/bin/Release/net10.0-windows/ImmersiveWindow.exe`.
