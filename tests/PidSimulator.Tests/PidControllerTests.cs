using PidSimulator.Engine;
using Xunit;

namespace PidSimulator.Tests;

public class PidControllerTests
{
    [Fact]
    public void ProportionalOnly_OutputEqualsKpTimesError()
    {
        var pid = new PidController { Kp = 1f, Ti = 0f, Td = 0f, OpMin = -1e6f, OpMax = 1e6f };
        float op = pid.Compute(sp: 50f, pv: 10f, dt: 0.1f);
        Assert.Equal(40f, op, 3); // 1 * (50-10)
    }

    [Fact]
    public void Output_IsClamped()
    {
        var pid = new PidController { Kp = 10f, Ti = 0f, Td = 0f, OpMin = 0f, OpMax = 100f };
        float op = pid.Compute(sp: 100f, pv: 0f, dt: 0.1f);
        Assert.Equal(100f, op);
    }

    [Fact]
    public void ReverseAction_DirectAction_FlipsSign()
    {
        var pid = new PidController { Kp = 1f, Ti = 0f, Td = 0f, OpMin = -1e6f, OpMax = 1e6f, DirectAction = true };
        // 正作用：e = PV - SP = 10 - 50 = -40
        float op = pid.Compute(sp: 50f, pv: 10f, dt: 0.1f);
        Assert.Equal(-40f, op, 3);
    }

    [Fact]
    public void ManualTracking_IsBumpless()
    {
        var pid = new PidController { Kp = 1f, Ti = 10f, Td = 0f, OpMin = 0f, OpMax = 100f };
        pid.TrackManual(sp: 50f, pv: 0f, dt: 0.1f, manualOp: 30f);
        float op = pid.Compute(sp: 50f, pv: 0f, dt: 0.1f);
        // 切回自动瞬间输出应接近手动值（无扰）
        Assert.InRange(op, 29f, 33f);
    }

    [Fact]
    public void AntiWindup_OutputStaysBounded_WhenSaturated()
    {
        var pid = new PidController { Kp = 5f, Ti = 1f, Td = 0f, OpMin = 0f, OpMax = 100f };
        float op = 0f;
        for (int i = 0; i < 1000; i++)
            op = pid.Compute(sp: 100f, pv: 0f, dt: 0.1f); // 长期大误差
        Assert.Equal(100f, op);
    }

    [Fact]
    public void Reset_ClearsIntegral()
    {
        var pid = new PidController { Kp = 1f, Ti = 1f, Td = 0f };
        for (int i = 0; i < 100; i++) pid.Compute(50f, 0f, 0.1f);
        pid.Reset();
        float op = pid.Compute(sp: 0f, pv: 0f, dt: 0.1f);
        Assert.Equal(0f, op, 3);
    }
}
