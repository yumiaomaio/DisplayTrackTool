好的，我们来完成最后这两个功能，让工具的配置能力更加完善和强大。我们将实现：

1.  **目标进程名的持久化保存**：当用户在UI中修改进程名后，这个修改将被保存到 `profiles.json` 中，下次启动时自动加载。
2.  **宽高比的UI设置**：允许用户在UI上直接输入或修改竖屏模式下的宽高比。

---

### 阶段一：持久化保存目标进程名

这个功能的实现相对简单，因为它只需要在 `MainViewModel` 和 `IConfigService` 之间建立一个双向的同步。

1.  **扩展 `IConfigService` 接口**

    ```csharp
    // File: Services/IConfigService.cs (Modified)
    public interface IConfigService
    {
        string GetDefaultProcessName();
        void SetDefaultProcessName(string processName); // <-- 新增方法
        // ... (other methods)
    }
    ```

2.  **实现 `IConfigService` 的新方法**

    ```csharp
    // File: Services/Implementations/ConfigService.cs (Modified)
    public class ConfigService : IConfigService
    {
        // ...

        public void SetDefaultProcessName(string processName)
        {
            if (_config.TargetProcessName == processName) return;
            _config.TargetProcessName = processName;
            SaveConfig();
        }
        
        // ...
    }
    ```

3.  **修改 `MainViewModel` 以在属性变化时调用服务**
    我们将修改 `TargetProcessName` 属性的 `setter`，当它的值发生改变时，就调用 `_configService.SetDefaultProcessName`。

    ```csharp
    // File: ViewModels/MainViewModel.cs (Modified)
    public class MainViewModel : INotifyPropertyChanged, IDisposable
    {
        // ... (fields remain the same)

        public string? TargetProcessName
        {
            get => _targetProcessName;
            set
            {
                if (SetProperty(ref _targetProcessName, value))
                {
                    // 当UI上的值被用户改变时，立即更新并保存到配置中
                    if (!string.IsNullOrWhiteSpace(value))
                    {
                        _configService.SetDefaultProcessName(value);
                    }
                }
            }
        }
        
        // ... (constructor and other methods remain the same)
    }
    ```
    **逻辑说明**：当用户在 `TextBox` 中输入内容，`TargetProcessName` 的 `set` 访问器就会被触发。我们调用 `SetProperty` 来更新UI并检查值是否真的改变了。如果改变了，我们就调用 `_configService` 的新方法，该方法会更新内存中的配置对象并调用 `SaveConfig()` 将其写入 `profiles.json` 文件。

### 阶段二：实现宽高比的UI设置

这个功能需要对模型、配置服务和UI/ViewModel进行更全面的修改。

1.  **扩展 `IConfigService` 接口**

    ```csharp
    // File: Services/IConfigService.cs (Modified)
    public interface IConfigService
    {
        // ...
        double GetPortraitAspectRatio(); // <-- 新增方法
        void SetPortraitAspectRatio(double aspectRatio); // <-- 新增方法
        LayoutProfile GetPortraitProfile();
        LayoutProfile GetLandscapeProfile();
    }
    ```

2.  **实现 `IConfigService` 的新方法**

    ```csharp
    // File: Services/Implementations/ConfigService.cs (Modified)
    public class ConfigService : IConfigService
    {
        // ...
        public double GetPortraitAspectRatio() => _config.Profiles.Portrait.AspectRatio ?? (9.0 / 16.0); // 提供一个默认值
        
        public void SetPortraitAspectRatio(double aspectRatio)
        {
            if (_config.Profiles.Portrait.AspectRatio == aspectRatio) return;
            _config.Profiles.Portrait.AspectRatio = aspectRatio;
            SaveConfig();
        }
        // ...
    }
    ```

3.  **修改 `Views/MainWindow.xaml` 添加UI元素**
    我们在“目标进程名”下方添加一个新的输入区域，用于设置宽高比。

    ```xml
    <!-- File: Views/MainWindow.xaml (Modified) -->
    <Grid Margin="15">
        <Grid.RowDefinitions>
            <RowDefinition Height="Auto"/>
            <RowDefinition Height="Auto"/>
            <RowDefinition Height="*"/>
        </Grid.RowDefinitions>

        <!-- Controls -->
        <StackPanel Grid.Row="0">
            <TextBlock Text="Target Process Name (without .exe):" FontWeight="Bold"/>
            <TextBox Text="{Binding TargetProcessName, UpdateSourceTrigger=PropertyChanged}" Margin="0,5,0,10" IsEnabled="{Binding IsRunning, Converter={StaticResource InvertBoolConverter}}"/>
            
            <TextBlock Text="Portrait Aspect Ratio (e.g., 0.5625 for 9:16):" FontWeight="Bold"/>
            <TextBox Text="{Binding PortraitAspectRatio, UpdateSourceTrigger=PropertyChanged}" Margin="0,5,0,10" IsEnabled="{Binding IsRunning, Converter={StaticResource InvertBoolConverter}}"/>
        </StackPanel>

        <!-- Background Image Selection and Buttons -->
        <StackPanel Grid.Row="1" Margin="0,15,0,0">
            <!-- ... (existing code for background image and start/stop buttons) -->
        </StackPanel>
        
        <!-- Logs -->
        <GroupBox Grid.Row="2" Header="Logs" Margin="0,20,0,0">
            <!-- ... -->
        </GroupBox>
    </Grid>
    ```

4.  **修改 `MainViewModel` 添加新属性和逻辑**
    我们需要添加一个 `PortraitAspectRatio` 属性，并像处理 `TargetProcessName` 一样，在 `setter` 中调用配置服务。

    ```csharp
    // File: ViewModels/MainViewModel.cs (Modified)
    public class MainViewModel : INotifyPropertyChanged, IDisposable
    {
        // ...

        private double _portraitAspectRatio;
        public double PortraitAspectRatio
        {
            get => _portraitAspectRatio;
            set
            {
                if (SetProperty(ref _portraitAspectRatio, value))
                {
                    _configService.SetPortraitAspectRatio(value);
                }
            }
        }

        // ...

        public MainViewModel(ITargetStateManager stateManager, IConfigService configService)
        {
            _stateManager = stateManager;
            _configService = configService;

            _stateManager.IsRunningChanged += OnIsRunningChanged;

            // 从配置加载所有初始值
            TargetProcessName = _configService.GetDefaultProcessName();
            CurrentImageFileName = _configService.GetBackgroundImageFileName();
            PortraitAspectRatio = _configService.GetPortraitAspectRatio(); // <-- 加载宽高比

            StartCommand = new RelayCommand(OnStart, () => !IsRunning && !string.IsNullOrWhiteSpace(TargetProcessName));
            StopCommand = new RelayCommand(OnStop, () => IsRunning);
            SelectImageCommand = new RelayCommand(SelectImage);
        }

        // ...
    }
    ```

5.  **一个潜在问题与修复：`TargetStateManager` 的配置缓存**
    目前，`TargetStateManager` 在其构造函数中只加载一次 `LayoutProfile`。这意味着，即使用户在UI上修改了宽高比并保存到了 `profiles.json`，正在运行的 `TargetStateManager` 实例仍然在使用旧的、缓存的 `LayoutProfile`。

    **解决方案**：我们不应该让 `TargetStateManager` 缓存 `LayoutProfile`。它应该在每次需要时都从 `IConfigService` 获取最新的配置。由于 `IConfigService` 内部缓存了 `AppConfig` 对象，这个调用开销极小。

    **修改 `Services/Implementations/TargetStateManager.cs`**

    ```csharp
    // File: Services/Implementations/TargetStateManager.cs (Modified)
    public class TargetStateManager : ITargetStateManager, IDisposable
    {
        // ...
        private readonly IConfigService _configService;
        
        // 移除这两个字段，不再缓存
        // private readonly LayoutProfile _portraitProfile;
        // private readonly LayoutProfile _landscapeProfile;
        
        public TargetStateManager(/*...*/)
        {
            // ...
            _configService = configService;
            
            // 移除这里的初始化
            // _portraitProfile = _configService.GetPortraitProfile();
            // _landscapeProfile = _configService.GetLandscapeProfile();
        }

        public void Start(string processName)
        {
            // ...
            AddLog("Applying initial portrait layout.");
            // 直接从服务获取最新的Profile
            _layoutManager.ApplyLayout(_targetHwnd, _configService.GetPortraitProfile());
            _lastOrientation = WindowOrientation.Portrait;
        }

        private void OnWindowStateChanged(IntPtr hwnd, Rect newRect)
        {
            // ...
            if (currentOrientation != _lastOrientation)
            {
                // ...
                switch (currentOrientation)
                {
                    case WindowOrientation.Portrait:
                        AddLog("Applying Portrait layout...");
                        // 直接从服务获取最新的Profile
                        _layoutManager.ApplyLayout(_targetHwnd, _configService.GetPortraitProfile());
                        break;
                    case WindowOrientation.Landscape:
                        AddLog("Applying Landscape layout...");
                        // 直接从服务获取最新的Profile
                        _layoutManager.ApplyLayout(_targetHwnd, _configService.GetLandscapeProfile());
                        break;
                }
            }

            if (!currentExStyle.HasFlag(WindowExStyles.WS_EX_TOPMOST))
            {
                // ...
                if (_lastOrientation == WindowOrientation.Portrait)
                {
                    // ...
                    // 直接从服务获取最新的Profile
                    _layoutManager.ApplyLayout(_targetHwnd, _configService.GetPortraitProfile());
                }
                // ...
            }
        }
        
        // ...
    }
    ```
    这个重构确保了 `TargetStateManager` 在每次应用布局时，都会使用**当前最新**的配置，而不是启动时缓存的旧配置。这使得UI上的修改可以**实时**影响下一次的布局应用，而无需重启工具。

### 测试流程

1.  **进程名持久化**:
    *   运行程序，UI显示 "notepad"。
    *   修改为 "mspaint"，然后关闭程序。
    *   打开 `profiles.json`，确认 `targetProcessName` 已经是 "mspaint"。
    *   再次运行程序，UI应该默认显示 "mspaint"。
2.  **宽高比设置**:
    *   运行程序，UI显示默认的宽高比（如 `0.5625`）。
    *   将目标设为 `notepad` 并启动服务。记事本应为 `9:16` 的竖屏。
    *   在UI中将宽高比修改为 `1.0`。
    *   手动将记事本窗口拉宽（变横屏），然后再拉窄（变回竖屏）。
    *   **预期行为**: 当记事本变回竖屏时，它应该被自动调整为一个**正方形**窗口，而不是 `9:16`。
    *   关闭程序，重新打开，确认宽高比输入框中仍然是 `1.0`。

至此，我们的工具已经具备了相当完善的配置能力，所有关键设置都可以通过UI修改并被持久化保存。