namespace PidSimulator.Engine;

/// <summary>一次扫描的输出快照，供 UI / 趋势订阅。</summary>
public readonly record struct ScanResult(float SP, float PV, float OP, DateTime Time);
