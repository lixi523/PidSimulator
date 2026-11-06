namespace PidSimulator.Engine;

/// <summary>
/// 一阶惯性 + 纯滞后过程模型：G(s) = K·e^(-θs)/(τs+1)。
/// 纯滞后用队列实现（启动时按初始输入补零）；一阶惯性用精确离散
/// y = a·y_prev + (1−a)·K·u_delay，a = exp(−dt/τ)。详见设计文档 §5.2。
/// </summary>
public sealed class FopdtModel : IProcessModel
{
    public float K { get; set; } = 1f;
    public float Tau { get; set; } = 10f;
    public float Theta { get; set; } = 1f;

    /// <summary>测量噪声峰值（0 表示无噪声）。</summary>
    public float NoiseAmplitude { get; set; } = 0f;

    /// <summary>启动前的输入假设值（用于纯滞后补零）。</summary>
    public float InitialInput { get; set; } = 0f;

    private float _y;
    private readonly Queue<float> _delayLine = new();
    private int _delaySteps = -1;
    private readonly Random _rng = new();

    public float Step(float op, float dt)
    {
        if (dt <= 0f) dt = 1e-3f;

        // —— 纯滞后 ——
        int n = Theta > 0f ? (int)MathF.Round(Theta / dt) : 0;
        if (n != _delaySteps)
        {
            _delaySteps = n;
            _delayLine.Clear();
            for (int i = 0; i < n; i++)
                _delayLine.Enqueue(InitialInput);
        }

        float uDelay;
        if (n <= 0)
        {
            uDelay = op;
        }
        else
        {
            _delayLine.Enqueue(op);
            while (_delayLine.Count > n + 1)
                _delayLine.Dequeue();
            uDelay = _delayLine.Peek();
        }

        // —— 一阶惯性（精确离散）——
        float a = Tau > 0f ? MathF.Exp(-dt / Tau) : 0f;
        _y = a * _y + (1f - a) * K * uDelay;

        float pv = _y;
        if (NoiseAmplitude > 0f)
            pv += (float)((_rng.NextDouble() * 2.0 - 1.0) * NoiseAmplitude);
        return pv;
    }

    public void Reset(float initialPv)
    {
        _y = initialPv;
        _delayLine.Clear();
        _delaySteps = -1;
    }
}
