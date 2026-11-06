using System.Collections.ObjectModel;
using System.Windows.Input;
using PidSimulator.Config;
using PidSimulator.Core;
using PidSimulator.Engine;
using PidSimulator.Modbus;

namespace PidSimulator.ViewModels;

/// <summary>
/// 主界面 ViewModel：绑定参数、状态、趋势与日志，并封装命令。
/// 后台线程事件（扫描/日志）通过 <see cref="UiInvoker"/> 切回 UI 线程。
/// </summary>
public sealed class MainViewModel : ObservableObject
{
    private readonly ControlState _state;
    private readonly SimulationEngine _engine;
    private readonly IModbusServer _modbus;
    private readonly ConfigService _configService;
    private readonly string _configPath;
    private readonly int _trendCapacity;

    private float _pv;
    private float _op;
    private bool _isModbusListening;
    private string _modbusStatusText = "Modbus:502 未监听";

    public MainViewModel(
        ControlState state,
        SimulationEngine engine,
        IModbusServer modbus,
        ConfigService configService,
        string configPath,
        int trendCapacity = 600)
    {
        _state = state;
        _engine = engine;
        _modbus = modbus;
        _configService = configService;
        _configPath = configPath;
        _trendCapacity = Math.Max(1, trendCapacity);

        ToggleRunCommand = new RelayCommand(ToggleRun);
        ToggleModeCommand = new RelayCommand(ToggleMode);
        ResetParamsCommand = new RelayCommand(ResetParams);
        SaveConfigCommand = new RelayCommand(SaveConfig);
        ClearTrendCommand = new RelayCommand(() => OnUi(() => Trend.Clear()));
        ClearLogsCommand = new RelayCommand(() => OnUi(() => Logs.Clear()));

        _engine.Scanned += OnScanned;
        _modbus.Logged += OnLogged;
        _modbus.ListeningChanged += OnListeningChanged;
        _modbus.ValuesWritten += OnValuesWritten;
    }

    /// <summary>UI 线程调度器（由 App 注入 Dispatcher.Invoke）。默认就地执行。</summary>
    public Action<Action>? UiInvoker { get; set; }

    // —— 趋势与日志 ——
    public ObservableCollection<TrendPoint> Trend { get; } = new();
    public ObservableCollection<string> Logs { get; } = new();

    // —— 可设置参数（直接映射到 ControlState）——
    public float SP
    {
        get => _state.SP;
        set { lock (_state.Sync) _state.SP = Clamp(value, _state.SpMin, _state.SpMax); OnPropertyChanged(); }
    }

    public float P
    {
        get => _state.P;
        set { lock (_state.Sync) _state.P = MathF.Max(0f, value); OnPropertyChanged(); }
    }

    public float I
    {
        get => _state.I;
        set { lock (_state.Sync) _state.I = MathF.Max(0f, value); OnPropertyChanged(); }
    }

    public float D
    {
        get => _state.D;
        set { lock (_state.Sync) _state.D = MathF.Max(0f, value); OnPropertyChanged(); }
    }

    public float K
    {
        get => _state.K;
        set { lock (_state.Sync) _state.K = value; OnPropertyChanged(); }
    }

    public float Tau
    {
        get => _state.Tau;
        set { lock (_state.Sync) _state.Tau = MathF.Max(0.001f, value); OnPropertyChanged(); }
    }

    public float Theta
    {
        get => _state.Theta;
        set { lock (_state.Sync) _state.Theta = MathF.Max(0f, value); OnPropertyChanged(); }
    }

    public int ScanPeriodMs
    {
        get => _state.ScanPeriodMs;
        set { lock (_state.Sync) _state.ScanPeriodMs = Math.Clamp(value, 10, 60000); OnPropertyChanged(); }
    }

    // —— 只读显示 ——
    public float PV { get => _pv; private set => SetProperty(ref _pv, value); }
    public float OP { get => _op; private set => SetProperty(ref _op, value); }

    public bool IsAuto
    {
        get => _state.Mode == ControlMode.Auto;
        set
        {
            lock (_state.Sync)
                _state.Mode = value ? ControlMode.Auto : ControlMode.Manual;
            OnPropertyChanged();
            OnPropertyChanged(nameof(ModeText));
        }
    }

    public string ModeText => IsAuto ? "自动仿真" : "手动仿真";

    public bool IsRunning
    {
        get => _state.IsRunning;
        set
        {
            lock (_state.Sync) _state.IsRunning = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(RunButtonText));
        }
    }

    public string RunButtonText => IsRunning ? "暂停仿真" : "开始仿真";

    public bool IsModbusListening
    {
        get => _isModbusListening;
        private set => SetProperty(ref _isModbusListening, value);
    }

    public string ModbusStatusText
    {
        get => _modbusStatusText;
        private set => SetProperty(ref _modbusStatusText, value);
    }

    // —— 命令 ——
    public ICommand ToggleRunCommand { get; }
    public ICommand ToggleModeCommand { get; }
    public ICommand ResetParamsCommand { get; }
    public ICommand SaveConfigCommand { get; }
    public ICommand ClearTrendCommand { get; }
    public ICommand ClearLogsCommand { get; }

    private void ToggleRun()
    {
        lock (_state.Sync) _state.IsRunning = !_state.IsRunning;
        OnPropertyChanged(nameof(IsRunning));
        OnPropertyChanged(nameof(RunButtonText));
    }

    private void ToggleMode()
    {
        lock (_state.Sync)
            _state.Mode = _state.Mode == ControlMode.Auto ? ControlMode.Manual : ControlMode.Auto;
        OnPropertyChanged(nameof(IsAuto));
        OnPropertyChanged(nameof(ModeText));
    }

    private void ResetParams()
    {
        lock (_state.Sync)
        {
            _state.SP = 50f;
            _state.P = 2f;
            _state.I = 10f;
            _state.D = 0f;
            _state.K = 1f;
            _state.Tau = 10f;
            _state.Theta = 1f;
        }

        OnPropertyChanged(nameof(SP));
        OnPropertyChanged(nameof(P));
        OnPropertyChanged(nameof(I));
        OnPropertyChanged(nameof(D));
        OnPropertyChanged(nameof(K));
        OnPropertyChanged(nameof(Tau));
        OnPropertyChanged(nameof(Theta));
        AddLog("参数已恢复默认");
    }

    private void SaveConfig()
    {
        try
        {
            _configService.Save(BuildConfig(), _configPath);
            AddLog($"配置已保存：{_configPath}");
        }
        catch (Exception ex)
        {
            AddLog($"保存配置失败：{ex.Message}");
        }
    }

    private AppConfig BuildConfig()
    {
        lock (_state.Sync)
        {
            return new AppConfig
            {
                Modbus = new ModbusConfig
                {
                    ListenAddress = "127.0.0.1",
                    Port = 502,
                    UnitId = 1,
                    MaxConnections = 5,
                    WordSwap = false,
                    OpWriteInAuto = "SwitchToManual"
                },
                Pid = new PidConfig
                {
                    DirectAction = _state.DirectAction,
                    OpMin = _state.OpMin,
                    OpMax = _state.OpMax,
                    SpMin = _state.SpMin,
                    SpMax = _state.SpMax
                },
                Process = new ProcessConfig
                {
                    K = _state.K,
                    Tau = _state.Tau,
                    Theta = _state.Theta,
                    Noise = 0f,
                    InitialPv = _state.PV
                },
                Ui = new UiConfig
                {
                    TrendWindowSec = 60,
                    TrendBufferSize = _trendCapacity
                }
            };
        }
    }

    // —— 事件回调（后台线程）——
    private void OnScanned(ScanResult result)
    {
        OnUi(() =>
        {
            PV = result.PV;
            OP = result.OP;
            Trend.Add(new TrendPoint(result.Time, result.SP, result.PV, result.OP));
            while (Trend.Count > _trendCapacity)
                Trend.RemoveAt(0);
        });
    }

    private void OnLogged(string message) => OnUi(() => AddLog(message));

    /// <summary>外部（Modbus）写入后刷新界面绑定，实现“写入即回显”。</summary>
    private void OnValuesWritten(IReadOnlyList<ModbusWrite> changes)
    {
        OnUi(() =>
        {
            foreach (var change in changes)
            {
                switch (change.Name)
                {
                    case "SP": OnPropertyChanged(nameof(SP)); break;
                    case "OP": OP = change.Value; break;
                    case "P": OnPropertyChanged(nameof(P)); break;
                    case "I": OnPropertyChanged(nameof(I)); break;
                    case "D": OnPropertyChanged(nameof(D)); break;
                }
            }

            // 写 OP 可能触发自动→手动切换
            OnPropertyChanged(nameof(IsAuto));
            OnPropertyChanged(nameof(ModeText));
        });
    }

    private void OnListeningChanged(bool listening)
    {
        OnUi(() =>
        {
            IsModbusListening = listening;
            ModbusStatusText = listening ? "Modbus:502 已监听" : "Modbus:502 未监听";
        });
    }

    private void AddLog(string message)
    {
        // 时间倒序：最新一条置顶
        Logs.Insert(0, message);
        while (Logs.Count > 500)
            Logs.RemoveAt(Logs.Count - 1);
    }

    private void OnUi(Action action)
    {
        if (UiInvoker is null)
            action();
        else
            UiInvoker(action);
    }

    private static float Clamp(float value, float lo, float hi) => value < lo ? lo : (value > hi ? hi : value);
}
