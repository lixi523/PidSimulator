namespace PidSimulator.Modbus;

/// <summary>一次 Modbus 写入造成的单个字段变更（用于回显界面与日志）。</summary>
/// <param name="Name">字段名：SP / OP / P / I / D。</param>
/// <param name="Offset">该字段在寄存器区的起始偏移。</param>
/// <param name="Value">写入后的值。</param>
public readonly record struct ModbusWrite(string Name, int Offset, float Value)
{
    /// <summary>对应的 4xxxx 参考号（4001 基）。</summary>
    public int Reference => 4001 + Offset;
}
