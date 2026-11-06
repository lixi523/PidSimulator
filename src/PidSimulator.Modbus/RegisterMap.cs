namespace PidSimulator.Modbus;

/// <summary>
/// 保持寄存器地址映射常量与 float 读写辅助。
/// 已确认约定：4001 = FC03 寄存器地址 0，即 PDU 偏移 = 地址号 − 4001。
/// </summary>
public static class RegisterMap
{
    /// <summary>SP（4001）起始偏移。</summary>
    public const int SpOffset = 0;

    /// <summary>OP（4003）起始偏移。</summary>
    public const int OpOffset = 2;

    /// <summary>P（4005）起始偏移。</summary>
    public const int POffset = 4;

    /// <summary>I（4007）起始偏移。</summary>
    public const int IOffset = 6;

    /// <summary>D（4009）起始偏移。</summary>
    public const int DOffset = 8;

    /// <summary>PV（4011）起始偏移（只读）。</summary>
    public const int PvOffset = 10;

    /// <summary>寄存器区总长度（偏移 0–11）。</summary>
    public const int TotalRegisters = 12;

    /// <summary>从寄存器数组读取指定偏移处的 float。</summary>
    public static float ReadFloat(ushort[] regs, int offset, bool wordSwap = false)
        => FloatCodec.Decode(regs[offset], regs[offset + 1], wordSwap);

    /// <summary>向寄存器数组指定偏移处写入 float。</summary>
    public static void WriteFloat(ushort[] regs, int offset, float value, bool wordSwap = false)
    {
        var (first, second) = FloatCodec.EncodePair(value, wordSwap);
        regs[offset] = first;
        regs[offset + 1] = second;
    }
}
