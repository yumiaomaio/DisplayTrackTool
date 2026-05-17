# DisplayTrackTool (ImmersiveWindow)

A sophisticated Windows display and window management utility designed for immersive setups and multi-monitor control. It leverages low-level Win32 APIs for reliable display mapping and window manipulation.

## Project Overview

DisplayTrackTool provides a modern interface to control Windows desktop environment features that are typically hard to manage, such as:
- Taskbar visibility and positioning.
- Advanced window layout management across multiple monitors.
- Reliable GDI to CCD (Connecting and Configuring Displays) mapping.
- Global keyboard hooks for hotkey control.
- "Immersive" mode for applications.

### Architecture
- **Backend**: C# / WPF application targeting .NET 10.0.
- **Frontend**: Vue 3 application built with Vite, embedded via **WebView2**.
- **Communication**: A bi-directional bridge (`AppBridge.cs`) connects the JavaScript frontend with the C# backend.
- **Interop**: Extensive use of P/Invoke to access `User32.dll`, `Shell32.dll`, `Gdi32.dll`, and other Windows system APIs.

## Building and Running

### Prerequisites
- .NET 10.0 SDK
- Node.js & npm (for frontend development)

### Frontend Development
The UI is located in the `vite-project` directory.
```powershell
cd vite-project
npm install
# For development (with HMR, requires backend to point to dev server)
npm run dev
# For production build (outputs to ImmersiveWindow/WebUI)
npm run build
```
*Note: `vite-plugin-singlefile` is used to bundle the entire UI into a single `index.html` for easy embedding.*

### Backend Development
The main solution is `ImmersiveWindow.sln`.
```powershell
# Restore dependencies
dotnet restore
# Build the project
dotnet build
# Run the application
dotnet run --project ImmersiveWindow/ImmersiveWindow.csproj
```

## Project Structure

- `ImmersiveWindow/`: Main WPF application.
  - `Bridge/`: Contains `AppBridge.cs`, the JS-C# bridge.
  - `Interop/`: Win32 API declarations (P/Invoke) and related structs/enums.
  - `Services/`: Core business logic (Taskbar, Display, Window management).
  - `ViewModels/`: MVVM pattern implementation.
  - `WebUI/`: Target directory for the built frontend.
- `vite-project/`: Vue 3 source code for the user interface.
- `DOC/`: Technical documentation and research notes (e.g., GDI to CCD mapping strategies).

## Development Conventions

- **Service-Oriented**: Business logic should be encapsulated in services with interfaces defined in `ImmersiveWindow/Services`.
- **Interop Safety**: Keep Win32 API signatures organized in `ImmersiveWindow/Interop` using partial classes (e.g., `NativeMethods.Window.cs`).
- **UI Bridge**: New backend features intended for the UI must be exposed through `AppBridge.cs`.
- **Async Priority**: Use asynchronous patterns for UI-bound operations to keep the interface responsive, especially during WebView2 initialization.
- **DPI Awareness**: Always consider per-monitor DPI scaling when performing window or display calculations.
