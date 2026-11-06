using PidSimulator.Modbus;
using Xunit;

namespace PidSimulator.Tests;

public class RegisterMapTests
{
    [Fact]
    public void Offsets_MatchConfirmedAddresses()
    {
        Assert.Equal(0, RegisterMap.SpOffset);   // 4001
        Assert.Equal(2, RegisterMap.OpOffset);   // 4003
        Assert.Equal(4, RegisterMap.POffset);    // 4005
        Assert.Equal(6, RegisterMap.IOffset);    // 4007
        Assert.Equal(8, RegisterMap.DOffset);    // 4009
        Assert.Equal(10, RegisterMap.PvOffset);  // 4011（只读）
        Assert.Equal(12, RegisterMap.TotalRegisters);
    }

    [Fact]
    public void WriteReadFloat_RoundTrips()
    {
        var regs = new ushort[RegisterMap.TotalRegisters];
        RegisterMap.WriteFloat(regs, RegisterMap.SpOffset, 80.5f);
        Assert.Equal(80.5f, RegisterMap.ReadFloat(regs, RegisterMap.SpOffset));
    }

    [Fact]
    public void WriteReadFloat_WordSwap_RoundTrips()
    {
        var regs = new ushort[RegisterMap.TotalRegisters];
        RegisterMap.WriteFloat(regs, RegisterMap.POffset, 2.25f, wordSwap: true);
        Assert.Equal(2.25f, RegisterMap.ReadFloat(regs, RegisterMap.POffset, wordSwap: true));
    }
}
