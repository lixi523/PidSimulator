namespace PidSimulator.Modbus;

/// <summary>
/// IEEE-754 单精度 float ↔ 保持寄存器对（2×16bit）编解码。
/// 默认大端 ABCD：寄存器 N 存高 16 位，N+1 存低 16 位。
/// wordSwap=true 时字序交换（CDAB），以适配按低字在前的设备。
/// </summary>
public static class FloatCodec
{
    /// <summary>解码：hi=第一个寄存器、lo=第二个寄存器。</summary>
    public static float Decode(ushort hi, ushort lo, bool wordSwap = false)
    {
        ushort h = wordSwap ? lo : hi;
        ushort l = wordSwap ? hi : lo;
        uint bits = ((uint)h << 16) | l;
        return BitConverter.UInt32BitsToSingle(bits);
    }

    /// <summary>编码为寄存器对 (第一个寄存器, 第二个寄存器)。</summary>
    public static (ushort First, ushort Second) EncodePair(float value, bool wordSwap = false)
    {
        uint bits = BitConverter.SingleToUInt32Bits(value);
        ushort hi = (ushort)(bits >> 16);
        ushort lo = (ushort)(bits & 0xFFFF);
        return wordSwap ? (lo, hi) : (hi, lo);
    }

    /// <summary>编码为长度 2 的寄存器数组。</summary>
    public static ushort[] Encode(float value, bool wordSwap = false)
    {
        var (first, second) = EncodePair(value, wordSwap);
        return new[] { first, second };
    }
}
