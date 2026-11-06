namespace PidSimulator.Engine;

/// <summary>被控过程模型接口。</summary>
public interface IProcessModel
{
    /// <summary>输入 OP，步进 dt 秒，返回新的 PV。</summary>
    float Step(float op, float dt);

    /// <summary>复位到指定初始 PV。</summary>
    void Reset(float initialPv);
}
