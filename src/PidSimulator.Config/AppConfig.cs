namespace PidSimulator.Config;

/// <summary>应用配置根对象（对应 appsettings.json）。</summary>
public sealed class AppConfig
{
    public ModbusConfig Modbus { get; set; } = new();
    public PidConfig Pid { get; set; } = new();
    public ProcessConfig Process { get; set; } = new();
    public UiConfig Ui { get; set; } = new();
}

/// <summary>Modbus 服务端配置。</summary>
public sealed class ModbusConfig
{
    public string ListenAddress { get; set; } = "127.0.0.1";
    public int Port { get; set; } = 502;
    public byte UnitId { get; set; } = 1;

    /// <summary>最大并发连接数（默认 5）。</summary>
    public int MaxConnections { get; set; } = 5;

    /// <summary>false=大端 ABCD（默认）。</summary>
    public bool WordSwap { get; set; } = false;

    /// <summary>自动模式下外部写 OP 的策略：SwitchToManual（默认）或 Ignore。</summary>
    public string OpWriteInAuto { get; set; } = "SwitchToManual";
}

/// <summary>PID 配置。</summary>
public sealed class PidConfig
{
    /// <summary>false=反作用（默认）；true=正作用。</summary>
    public bool DirectAction { get; set; } = false;

    public float OpMin { get; set; } = 0f;
    public float OpMax { get; set; } = 100f;
    public float SpMin { get; set; } = 0f;
    public float SpMax { get; set; } = 100f;
}

/// <summary>过程模型配置。</summary>
public sealed class ProcessConfig
{
    public float K { get; set; } = 1f;
    public float Tau { get; set; } = 10f;
    public float Theta { get; set; } = 1f;
    public float Noise { get; set; } = 0f;
    public float InitialPv { get; set; } = 0f;
}

/// <summary>界面配置。</summary>
public sealed class UiConfig
{
    public int TrendWindowSec { get; set; } = 60;

    /// <summary>趋势缓冲点数（默认 600）。</summary>
    public int TrendBufferSize { get; set; } = 600;
}
