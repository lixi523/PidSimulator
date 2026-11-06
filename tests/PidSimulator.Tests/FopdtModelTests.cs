using PidSimulator.Engine;
using Xunit;

namespace PidSimulator.Tests;

public class FopdtModelTests
{
    [Fact]
    public void SteadyState_ApproachesGainTimesInput()
    {
        var m = new FopdtModel { K = 2f, Tau = 5f, Theta = 0f };
        m.Reset(0f);

        float y = 0f;
        for (int i = 0; i < 5000; i++)
            y = m.Step(op: 10f, dt: 0.1f);

        Assert.InRange(y, 19.8f, 20.2f); // K * op = 20
    }

    [Fact]
    public void DeadTime_SuppressesEarlyResponse()
    {
        var m = new FopdtModel { K = 1f, Tau = 1f, Theta = 1f };
        m.Reset(0f);

        float y = 0f;
        // θ=1s, dt=0.1s → 前 10 步延迟输入仍为 0
        for (int i = 0; i < 9; i++)
            y = m.Step(op: 100f, dt: 0.1f);

        Assert.InRange(y, 0f, 0.001f);
    }

    [Fact]
    public void DeadTime_RespondsAfterDelay()
    {
        var m = new FopdtModel { K = 1f, Tau = 1f, Theta = 1f };
        m.Reset(0f);

        float y = 0f;
        for (int i = 0; i < 25; i++)
            y = m.Step(op: 100f, dt: 0.1f);

        Assert.True(y > 1f, $"延迟后应开始响应，实际 y={y}");
    }
}
