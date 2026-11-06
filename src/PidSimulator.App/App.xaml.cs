using System.IO;
using System.Windows;
using PidSimulator.Config;
using PidSimulator.Core;
using PidSimulator.Engine;
using PidSimulator.Modbus;
using PidSimulator.ViewModels;

namespace PidSimulator.App;

/// <summary>应用入口：装配依赖（配置 → 状态 → 引擎 → Modbus → 窗口）。</summary>
public partial class App : Application
{
    private SimulationEngine? _engine;
    private ModbusTcpServer? _modbus;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        string configPath = Path.Combine(AppContext.BaseDirectory, "appsettings.json");
        var configService = new ConfigService();
        var config = configService.Load(configPath);

        // —— 共享状态 ——
        var state = new ControlState
        {
            SP = 50f,
            P = 2f,
            I = 10f,
            D = 0f,
            K = config.Process.K,
            Tau = config.Process.Tau,
            Theta = config.Process.Theta,
            DirectAction = config.Pid.DirectAction,
            OpMin = config.Pid.OpMin,
            OpMax = config.Pid.OpMax,
            SpMin = config.Pid.SpMin,
            SpMax = config.Pid.SpMax,
            ScanPeriodMs = 100,
            Mode = ControlMode.Auto,
            IsRunning = true
        };

        // —— 算法与仿真 ——
        var pid = new PidController();
        var process = new FopdtModel
        {
            K = state.K,
            Tau = state.Tau,
            Theta = state.Theta,
            NoiseAmplitude = config.Process.Noise
        };
        process.Reset(config.Process.InitialPv);

        var trend = new TrendBuffer(config.Ui.TrendBufferSize);
        _engine = new SimulationEngine(state, pid, process, trend);

        // —— Modbus 服务端 ——
        var options = new ModbusServerOptions
        {
            ListenAddress = config.Modbus.ListenAddress,
            Port = config.Modbus.Port,
            UnitId = config.Modbus.UnitId,
            MaxConnections = config.Modbus.MaxConnections,
            WordSwap = config.Modbus.WordSwap,
            OpWriteSwitchesToManual =
                !string.Equals(config.Modbus.OpWriteInAuto, "Ignore", StringComparison.OrdinalIgnoreCase)
        };
        _modbus = new ModbusTcpServer(state, options);

        // —— ViewModel 与窗口 ——
        var viewModel = new MainViewModel(state, _engine, _modbus, configService, configPath, config.Ui.TrendBufferSize)
        {
            UiInvoker = action => Dispatcher.Invoke(action)
        };

        var window = new MainWindow { DataContext = viewModel };
        window.Show();

        // —— 启动 ——
        _engine.Start();
        try
        {
            _modbus.Start();
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                $"Modbus 服务启动失败：{ex.Message}\n请确认端口 {config.Modbus.Port} 可用，必要时以管理员身份运行。",
                "PID 模拟器", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _engine?.Stop();
        _modbus?.Stop();
        base.OnExit(e);
    }
}
