using PidSimulator.Core;

namespace PidSimulator.Engine;

/// <summary>
/// 仿真引擎：以 <see cref="ControlState.ScanPeriodMs"/> 为节拍执行
/// “读参数 → PID → FOPDT → 写回状态”，并对外抛出 <see cref="Scanned"/>。
/// 详见设计文档 §2.4 / §14.9。
/// </summary>
public sealed class SimulationEngine : ISimulationEngine, IDisposable
{
    private readonly ControlState _state;
    private readonly IPidController _pid;
    private readonly IProcessModel _process;
    private readonly TrendBuffer _trend;

    private readonly object _tickGuard = new();
    private Timer? _timer;

    public event Action<ScanResult>? Scanned;

    public bool IsRunning { get; private set; }

    public SimulationEngine(ControlState state, IPidController pid, IProcessModel process, TrendBuffer trend)
    {
        _state = state;
        _pid = pid;
        _process = process;
        _trend = trend;
    }

    public void Start()
    {
        if (IsRunning) return;
        int period = Math.Clamp(_state.ScanPeriodMs, 10, 60_000);
        _timer = new Timer(_ => Tick(), null, 0, period);
        IsRunning = true;
    }

    public void Stop()
    {
        _timer?.Dispose();
        _timer = null;
        IsRunning = false;
    }

    /// <summary>单次扫描。可由定时器或单元测试调用。</summary>
    public void Tick()
    {
        lock (_tickGuard)
        {
            ScanResult result;

            lock (_state.Sync)
            {
                if (!_state.IsRunning) return;

                float dt = Math.Max(_state.ScanPeriodMs, 1) / 1000f;

                if (_pid is PidController pc)
                {
                    pc.Kp = _state.P;
                    pc.Ti = _state.I;
                    pc.Td = _state.D;
                    pc.OpMin = _state.OpMin;
                    pc.OpMax = _state.OpMax;
                    pc.DirectAction = _state.DirectAction;
                }

                float sp = _state.SP;
                float pv = _state.PV;

                float op;
                if (_state.Mode == ControlMode.Auto)
                {
                    op = _pid.Compute(sp, pv, dt);
                }
                else
                {
                    op = _state.OP;
                    _pid.TrackManual(sp, pv, dt, op);
                }

                if (_process is FopdtModel fm)
                {
                    fm.K = _state.K;
                    fm.Tau = _state.Tau;
                    fm.Theta = _state.Theta;
                }

                float newPv = _process.Step(op, dt);

                _state.OP = op;
                _state.PV = newPv;

                result = new ScanResult(sp, newPv, op, DateTime.Now);
            }

            _trend.Add(new TrendPoint(result.Time, result.SP, result.PV, result.OP));
            Scanned?.Invoke(result);
        }
    }

    public void Dispose() => Stop();
}
