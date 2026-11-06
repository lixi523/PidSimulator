namespace PidSimulator.Core;

/// <summary>控制模式：手动 / 自动。</summary>
public enum ControlMode
{
    /// <summary>手动：OP 由界面或 Modbus 给定。</summary>
    Manual,

    /// <summary>自动：OP 由 PID 计算。</summary>
    Auto
}
