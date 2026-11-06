namespace PidSimulator.Engine;

/// <summary>PID 控制器接口。</summary>
public interface IPidController
{
    /// <summary>自动模式：根据 SP/PV 计算输出 OP（已限幅）。</summary>
    float Compute(float sp, float pv, float dt);

    /// <summary>手动模式：给定手动输出，令积分项跟踪，保证切回自动时无扰。</summary>
    void TrackManual(float sp, float pv, float dt, float manualOp);

    /// <summary>清零内部状态（积分、上次 PV）。</summary>
    void Reset();
}
