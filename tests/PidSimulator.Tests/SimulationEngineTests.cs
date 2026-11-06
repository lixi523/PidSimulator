using PidSimulator.Core;
using PidSimulator.Engine;
using Xunit;

namespace PidSimulator.Tests;

public class SimulationEngineTests
{
    private static (SimulationEngine engine, ControlState state) CreateEngine()
    {
        var state = new ControlState
        {
            SP = 50f,
            P = 2f,
            I = 10f,
            D = 0f,
            K = 1f,
            Tau = 10f,
            Theta = 1f,
            ScanPeriodMs = 100,
            Mode = ControlMode.Auto,
            IsRunning = true,
            OpMin = 0f,
            OpMax = 100f
        };

        var pid = new PidController();
        var process = new FopdtModel { K = state.K, Tau = state.Tau, Theta = state.Theta };
        process.Reset(0f);
        var trend = new TrendBuffer(600);
        var engine = new SimulationEngine(state, pid, process, trend);
        return (engine, state);
    }

    [Fact]
    public void AutoMode_ConvergesToSetpoint()
    {
        var (engine, state) = CreateEngine();

        for (int i = 0; i < 5000; i++)
            engine.Tick();

        Assert.InRange(state.PV, state.SP - 2f, state.SP + 2f);
    }

    [Fact]
    public void Scanned_EventFires()
    {
        var (engine, _) = CreateEngine();
        int count = 0;
        engine.Scanned += _ => count++;
        for (int i = 0; i < 10; i++) engine.Tick();
        Assert.Equal(10, count);
    }

    [Fact]
    public void ManualMode_HoldsOutput()
    {
        var (engine, state) = CreateEngine();
        state.Mode = ControlMode.Manual;
        state.OP = 42f;

        engine.Tick();

        Assert.Equal(42f, state.OP, 3);
    }

    [Fact]
    public void Paused_DoesNotAdvance()
    {
        var (engine, state) = CreateEngine();
        state.IsRunning = false;
        var before = state.PV;
        engine.Tick();
        Assert.Equal(before, state.PV);
    }
}
