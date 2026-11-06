namespace PidSimulator.Modbus;

/// <summary>Modbus TCP 服务端接口。</summary>
public interface IModbusServer
{
    bool IsRunning { get; }

    /// <summary>通信日志。</summary>
    event Action<string>? Logged;

    /// <summary>监听状态变化。</summary>
    event Action<bool>? ListeningChanged;

    /// <summary>外部（Modbus）写入造成的字段变更，用于界面回显。</summary>
    event Action<IReadOnlyList<ModbusWrite>>? ValuesWritten;

    void Start();

    void Stop();
}
