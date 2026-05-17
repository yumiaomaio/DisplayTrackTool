# 可靠的 GDI 到 CCD (Connecting and Configuring Displays) 映射方案

在 Windows 显示编程中，通常会遇到两套主要的 API 体系：
1. **GDI (Graphics Device Interface)**: 使用 `\\.\DISPLAY1` 这样的逻辑设备名称，常用于 `EnumDisplaySettings` 和处理窗口句柄 (HWND)。
2. **CCD (Connecting and Configuring Displays)**: 使用 `AdapterId` (LUID) 和 `SourceId` / `TargetId` 的物理路径，常用于多显示器拓扑管理、底层分辨率修改 (`SetDisplayConfig`) 和动态 DPI 设置。

在复杂的多显示器环境下，将这两套系统准确地联系起来（即知道一个 HWND 对应哪个物理显卡输出口）是一个痛点。`ResponsiveWindowTool` 项目提供了一个非常健壮的映射方案。

## 核心实现思路

该方案的核心思想是：**从窗口句柄出发，获取其所在的 GDI 监视器名称，然后遍历所有物理 CCD 路径，查询每个路径的源（Source）对应的 GDI 名称，最后进行字符串匹配。**

### 第一步：通过 HWND 获取目标监视器的 GDI 名称

这是最传统的获取窗口所在显示器的方法。

```csharp
// 1. 获取窗口所在的监视器句柄
IntPtr hMonitor = NativeMethods.MonitorFromWindow(hwnd, MonitorOptions.MONITOR_DEFAULTTONEAREST);

// 2. 获取监视器信息
var monitorInfoEx = new MONITORINFOEX();
monitorInfoEx.cbSize = Marshal.SizeOf(monitorInfoEx);
NativeMethods.GetMonitorInfo(hMonitor, ref monitorInfoEx);

// 3. 提取 GDI 设备名称 (例如: "\\.\DISPLAY1")
string targetDeviceName = monitorInfoEx.szDevice;
```

### 第二步：查询所有激活的 CCD 显示路径

我们需要获取当前系统正在使用的所有物理显示路径。

```csharp
// 1. 获取需要的缓冲区大小
NativeMethods.GetDisplayConfigBufferSizes(QueryDisplayConfigFlags.QDC_ONLY_ACTIVE_PATHS, out uint pathCount, out uint modeCount);

var paths = new DISPLAYCONFIG_PATH_INFO[pathCount];
var modes = new DISPLAYCONFIG_MODE_INFO[modeCount];

// 2. 填充路径信息
NativeMethods.QueryDisplayConfig(QueryDisplayConfigFlags.QDC_ONLY_ACTIVE_PATHS, ref pathCount, paths, ref modeCount, modes, IntPtr.Zero);
```

### 第三步：遍历 CCD 路径并匹配 GDI 名称

对于第二步获取的每一个路径，我们向系统查询其对应的 GDI 名称。

```csharp
foreach (var path in paths)
{
    var sourceInfo = path.sourceInfo;

    // 构造请求包，指定要查询的类型为 GET_SOURCE_NAME
    var sourceNameRequest = new DISPLAYCONFIG_SOURCE_DEVICE_NAME
    {
        header = new DISPLAYCONFIG_DEVICE_INFO_HEADER
        {
            type = DisplayConfigDeviceInfoType.DISPLAYCONFIG_DEVICE_INFO_GET_SOURCE_NAME,
            size = (uint)Marshal.SizeOf<DISPLAYCONFIG_SOURCE_DEVICE_NAME>(),
            adapterId = sourceInfo.adapterId, // LUID
            id = sourceInfo.id                // Source ID
        }
    };

    // 调用 API 获取该物理源的 GDI 名称
    if (NativeMethods.DisplayConfigGetDeviceInfo(ref sourceNameRequest) == 0)
    {
        string ccdGdiName = sourceNameRequest.viewGdiDeviceName;

        // 进行字符串比较 (忽略大小写)
        if (targetDeviceName.Equals(ccdGdiName, StringComparison.OrdinalIgnoreCase))
        {
            // 匹配成功！我们找到了对应的物理路径
            return new DisplayIdentifiers
            {
                DeviceName = targetDeviceName,
                AdapterId = sourceInfo.adapterId,
                SourceId = sourceInfo.id
            };
        }
    }
}
```

## 为什么这个方案比通过坐标 (Position) 匹配更好？

在主项目 `Borderless-Windows-Display` 的早期实现中（如 `GetSourceInfo_MapDeviceName` 方法），尝试过通过匹配 `EnumDisplaySettings` 获取的坐标（Position X/Y）和 `QueryDisplayConfig` 模式中获取的坐标来建立映射。

**坐标匹配方案的缺陷：**
1. **DPI 缩放干扰**：在多显示器且 DPI 缩放不同的情况下，Windows 有时候会进行虚拟化，导致 GDI 返回的坐标与 CCD 底层返回的物理坐标不完全一致。
2. **复制模式 (Clone Mode)**：在屏幕复制模式下，多个显示器共享相同的坐标和分辨率，坐标匹配会完全失效。

**当前方案的优势：**
使用 `DISPLAYCONFIG_DEVICE_INFO_GET_SOURCE_NAME` 直接询问操作系统底层，“这个物理显卡接口对应的逻辑设备名是什么？”，这是一种**直接的标识符映射**，它不受 DPI 缩放、分辨率或者屏幕排列位置的影响，即使在复制模式下也能准确区分源和目标。

## 相关 API 与结构体定义

为了实现上述逻辑，需要定义以下 P/Invoke 结构：

```csharp
public enum DisplayConfigDeviceInfoType : int
{
    DISPLAYCONFIG_DEVICE_INFO_GET_SOURCE_NAME = 1,
    // ... 其他定义
}

[StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
public struct DISPLAYCONFIG_SOURCE_DEVICE_NAME
{
    public DISPLAYCONFIG_DEVICE_INFO_HEADER header;
    [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
    public string viewGdiDeviceName;
}
```