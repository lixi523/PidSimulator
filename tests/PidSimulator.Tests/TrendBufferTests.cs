using PidSimulator.Engine;
using Xunit;

namespace PidSimulator.Tests;

public class TrendBufferTests
{
    [Fact]
    public void Add_WithinCapacity_KeepsAll()
    {
        var buf = new TrendBuffer(5);
        for (int i = 0; i < 3; i++)
            buf.Add(new TrendPoint(DateTime.Now, i, i, i));
        Assert.Equal(3, buf.Count);
    }

    [Fact]
    public void Add_OverCapacity_KeepsNewestAndOrder()
    {
        var buf = new TrendBuffer(5);
        for (int i = 0; i < 8; i++)
            buf.Add(new TrendPoint(DateTime.Now, i, i, i));

        var snap = buf.Snapshot();
        Assert.Equal(5, snap.Count);
        Assert.Equal(3f, snap[0].SP); // 最旧保留的是第 3 个
        Assert.Equal(7f, snap[^1].SP);
    }

    [Fact]
    public void Clear_EmptiesBuffer()
    {
        var buf = new TrendBuffer(3);
        buf.Add(new TrendPoint(DateTime.Now, 1, 1, 1));
        buf.Clear();
        Assert.Equal(0, buf.Count);
    }
}
