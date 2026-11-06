namespace PidSimulator.Engine;

/// <summary>定长环形趋势缓冲，超出容量自动覆盖最旧数据。</summary>
public sealed class TrendBuffer
{
    private readonly TrendPoint[] _buffer;
    private int _start;
    private int _count;

    /// <summary>容量（默认 600 点）。</summary>
    public int Capacity { get; }

    public TrendBuffer(int capacity = 600)
    {
        if (capacity < 1) capacity = 1;
        Capacity = capacity;
        _buffer = new TrendPoint[capacity];
    }

    public int Count => _count;

    /// <summary>追加一个采样点（超出容量则覆盖最旧）。</summary>
    public void Add(in TrendPoint point)
    {
        int index = (_start + _count) % Capacity;
        _buffer[index] = point;
        if (_count < Capacity)
            _count++;
        else
            _start = (_start + 1) % Capacity;
    }

    /// <summary>返回按时间升序的只读快照。</summary>
    public IReadOnlyList<TrendPoint> Snapshot()
    {
        var result = new TrendPoint[_count];
        for (int i = 0; i < _count; i++)
            result[i] = _buffer[(_start + i) % Capacity];
        return result;
    }

    public void Clear()
    {
        _start = 0;
        _count = 0;
    }
}
