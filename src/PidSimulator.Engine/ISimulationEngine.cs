namespace PidSimulator.Engine;

/// <summary>仿真引擎接口。</summary>
public interface ISimulationEngine
{
    bool IsRunning { get; }
    event Action<ScanResult>? Scanned;
    void Start();
    void Stop();
    void Tick();
}
