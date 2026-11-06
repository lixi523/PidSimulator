namespace PidSimulator.Engine;

/// <summary>
/// 离散位置式 PID 控制器：含条件积分抗饱和、积分钳位、微分先行（作用在 PV）。
/// 详见设计文档 §4.2 / §4.3 / §4.4。
/// </summary>
public sealed class PidController : IPidController
{
    /// <summary>比例增益 Kp。</summary>
    public float Kp { get; set; } = 2f;

    /// <summary>积分时间 Ti（秒）；0 表示无积分。</summary>
    public float Ti { get; set; } = 10f;

    /// <summary>微分时间 Td（秒）；0 表示无微分。</summary>
    public float Td { get; set; } = 0f;

    public float OpMin { get; set; } = 0f;
    public float OpMax { get; set; } = 100f;

    /// <summary>false=反作用 e=SP−PV（默认）；true=正作用 e=PV−SP。</summary>
    public bool DirectAction { get; set; } = false;

    private float _integral;
    private float _prevPv;
    private bool _hasPrev;

    /// <summary>上一次的比例项（供手动跟踪/调试）。</summary>
    public float LastPTerm { get; private set; }

    /// <summary>上一次的微分项（供手动跟踪/调试）。</summary>
    public float LastDTerm { get; private set; }

    public float Compute(float sp, float pv, float dt)
    {
        if (dt <= 0f) dt = 1e-3f;

        float e = DirectAction ? (pv - sp) : (sp - pv);
        float pTerm = Kp * e;
        float dTerm = _hasPrev ? Kp * Td * (_prevPv - pv) / dt : 0f; // 微分先行

        // 条件积分（抗饱和）：饱和且误差仍推动其更饱和时，暂停积分
        float uUnclamped = pTerm + _integral + dTerm;
        bool satHigh = uUnclamped >= OpMax && e > 0f;
        bool satLow = uUnclamped <= OpMin && e < 0f;
        if (Ti > 0f && !satHigh && !satLow)
            _integral += Kp / Ti * e * dt;

        // 积分项钳位，使 u = P + I + D 不越界
        _integral = Clamp(_integral, OpMin - (pTerm + dTerm), OpMax - (pTerm + dTerm));

        float op = Clamp(pTerm + _integral + dTerm, OpMin, OpMax);

        LastPTerm = pTerm;
        LastDTerm = dTerm;
        _prevPv = pv;
        _hasPrev = true;
        return op;
    }

    public void TrackManual(float sp, float pv, float dt, float manualOp)
    {
        if (dt <= 0f) dt = 1e-3f;

        float e = DirectAction ? (pv - sp) : (sp - pv);
        float pTerm = Kp * e;
        float dTerm = _hasPrev ? Kp * Td * (_prevPv - pv) / dt : 0f;

        // 令积分项使输出恰好等于手动值 → 切回自动时连续（无扰）
        _integral = manualOp - pTerm - dTerm;

        LastPTerm = pTerm;
        LastDTerm = dTerm;
        _prevPv = pv;
        _hasPrev = true;
    }

    public void Reset()
    {
        _integral = 0f;
        _prevPv = 0f;
        _hasPrev = false;
        LastPTerm = 0f;
        LastDTerm = 0f;
    }

    private static float Clamp(float v, float lo, float hi) => v < lo ? lo : (v > hi ? hi : v);
}
