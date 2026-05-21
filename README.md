# Immersive Display Track Tool

一个高性能、模块化的 Windows 窗口沉浸式管理工具。

## 🌟 核心特性
- **响应式布局**：根据目标窗口的方向自动切换显示器设置和布局。
- **任务栏自动隐藏**：在控制期间自动隐藏 Windows 任务栏，实现全屏沉浸。
- **显示器同步**：支持自动旋转物理显示器并调整分辨率。
- **背景垫衬**：在非全屏比例下提供纯色或图像背景，消除视觉空隙。
- **高 DPI 感知**：在 4K 等高分屏上保持 UI 绝对清晰，不模糊。
- **跨语言架构**：使用 C++ 托管 UI，C# 驱动逻辑，Vue 提供现代交互界面。

## 🏗️ 项目架构
本项目采用了现代化的三层架构，以实现极致的性能和 AOT 兼容性：

1.  **Frontend (Vue 3 + Vite)**: 位于 `/vite-project`。负责所有 UI 交互，通过 JSON 指令与后端通信。
2.  **Core Logic (C# .NET 10)**: 位于 `/ImmersiveDisplay`。核心业务代码，通过 **Native AOT** 编译为原生的 `ImmersiveDisplay.dll`。彻底去除了 COM 依赖，实现高性能、零运行时的逻辑驱动。
3.  **Native Host (C++ Win32)**: 位于 `/immersive-cpp-wrapper`。极简的 C++ 宿主程序，负责创建 Win32 窗口、托管 WebView2 控件并作为前端与 C# DLL 之间的透明网关。

## 🛠️ 编译环境要求
- **Visual Studio 2022/2025/2026**: 包含 C++ 桌面开发组件及 MSVC 编译器。
- **.NET 10 SDK**: 用于编译 C# AOT 核心库。
- **Node.js (LTS)**: 用于编译 Vue 前端界面。
- **CMake (3.10+)**: 负责协调整个项目的自动化构建。

## 🚀 快速开始

### 1. 克隆项目
```bash
git clone <repository-url>
cd DisplayTrackTool
```

### 2. 一键编译 (使用 CMake)
我们已经将整个流水线（NPM -> .NET AOT -> C++）集成到了 CMake 中：

```bash
cd immersive-cpp-wrapper
mkdir build
cd build
cmake ..
cmake --build . --config Release
```

### 3. 运行程序
编译产物位于 `immersive-cpp-wrapper/build/Release/` 目录下：
```bash
cd Release
./immersive-cpp.exe
```

## 📂 目录结构
- `/ImmersiveDisplay`: C# 核心逻辑库，包含窗口监控、显示器控制等 Service。
- `/immersive-cpp-wrapper`: C++ 宿主项目，包含 Win32 窗口管理和 WebView2 集成。
- `/vite-project`: Vue 前端项目源码。
- `/DOC`: 项目相关文档及说明。

## 📝 配置文件
程序启动后会在同级目录下自动生成 `profiles.json`。您可以直接在 WebUI 中修改设置，更改将实时保存至该文件。

---
**Note**: 本项目通过 C++ 宿主成功避开了传统 WebView2 注入导致的 AOT 不兼容问题，是跨语言高性能桌面开发的最佳实践。
