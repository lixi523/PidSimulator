namespace PidSimulator.Engine;

/// <summary>趋势采样点。</summary>
public readonly record struct TrendPoint(DateTime Time, float SP, float PV, float OP);
