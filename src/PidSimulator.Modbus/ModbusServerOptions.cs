namespace PidSimulator.Modbus;

/// <summary>Modbus TCP 服务端配置项。</summary>
public sealed record ModbusServerOptions
{
    /// <summary>监听地址（默认仅本机）。</summary>
    public string ListenAddress { get; init; } = "127.0.0.1";

    /// <summary>监听端口（默认 502）。</summary>
    public int Port { get; init; } = 502;

    /// <summary>从站号（回显用）。</summary>
    public byte UnitId { get; init; } = 1;

    /// <summary>最大并发连接数（默认 5）。</summary>
    public int MaxConnections { get; init; } = 5;

    /// <summary>字序交换：false=大端 ABCD（默认）。</summary>
    public bool WordSwap { get; init; } = false;

    /// <summary>自动模式下外部写 OP 是否自动切换到手动（默认 true）。</summary>
    public bool OpWriteSwitchesToManual { get; init; } = true;
}
