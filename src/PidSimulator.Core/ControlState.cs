namespace PidSimulator.Core;

/// <summary>
/// 全局共享状态。仿真线程、Modbus 线程与 UI 线程均通过 <see cref="Sync"/> 保护读写，
/// 保证一组相关字段的一致性快照。
/// </summary>
public sealed class ControlState
{
    // —— 控制量（PID）——
    /// <summary>设定值 SP。</summary>
    public float SP { get; set; } = 50f;

    /// <summary>过程值 PV。</summary>
    public float PV { get; set; } = 0f;

    /// <summary>输出值 OP。</summary>
    public float OP { get; set; } = 0f;

    /// <summary>比例增益 Kp（对应 Modbus 的 P，4005）。</summary>
    public float P { get; set; } = 2f;

    /// <summary>积分时间 Ti（秒）（对应 Modbus 的 I，4007）。</summary>
    public float I { get; set; } = 10f;

    /// <summary>微分时间 Td（秒）（对应 Modbus 的 D，4009）。</summary>
    public float D { get; set; } = 0f;

    // —— 过程模型参数（FOPDT）——
    public float K { get; set; } = 1f;
    public float Tau { get; set; } = 10f;
    public float Theta { get; set; } = 1f;

    // —— 运行与模式 ——
    /// <summary>作用方向：false=反作用（默认，e=SP−PV）；true=正作用（e=PV−SP）。</summary>
    public bool DirectAction { get; set; } = false;

    public ControlMode Mode { get; set; } = ControlMode.Auto;

    /// <summary>是否运行扫描（false=暂停，参数仍可改）。</summary>
    public bool IsRunning { get; set; } = true;

    public int ScanPeriodMs { get; set; } = 100;

    // —— 限幅 ——
    public float OpMin { get; set; } = 0f;
    public float OpMax { get; set; } = 100f;
    public float SpMin { get; set; } = 0f;
    public float SpMax { get; set; } = 100f;

    /// <summary>唯一同步对象。所有关联字段的读改写请在此锁内完成。</summary>
    public object Sync { get; } = new();
}
