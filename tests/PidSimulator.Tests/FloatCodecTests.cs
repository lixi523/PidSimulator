using PidSimulator.Modbus;
using Xunit;

namespace PidSimulator.Tests;

public class FloatCodecTests
{
    [Fact]
    public void Decode_KnownValue_Returns50()
    {
        // 50.0f = 0x42480000 → 高字 0x4248, 低字 0x0000
        float v = FloatCodec.Decode(0x4248, 0x0000);
        Assert.Equal(50.0f, v);
    }

    [Fact]
    public void Encode_KnownValue_ReturnsExpectedWords()
    {
        var words = FloatCodec.Encode(50.0f);
        Assert.Equal(0x4248, words[0]);
        Assert.Equal(0x0000, words[1]);
    }

    [Theory]
    [InlineData(0f)]
    [InlineData(-12.34f)]
    [InlineData(100f)]
    [InlineData(1e-6f)]
    [InlineData(123456.78f)]
    public void RoundTrip_IsExact(float value)
    {
        var words = FloatCodec.Encode(value);
        float back = FloatCodec.Decode(words[0], words[1]);
        Assert.Equal(value, back);
    }

    [Fact]
    public void WordSwap_SwapsOrder()
    {
        var normal = FloatCodec.Encode(50.0f, wordSwap: false);
        var swapped = FloatCodec.Encode(50.0f, wordSwap: true);
        Assert.Equal(normal[0], swapped[1]);
        Assert.Equal(normal[1], swapped[0]);
        Assert.Equal(50.0f, FloatCodec.Decode(swapped[0], swapped[1], wordSwap: true));
    }
}
