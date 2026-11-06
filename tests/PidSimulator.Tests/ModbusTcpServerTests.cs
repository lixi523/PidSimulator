using System.IO;
using System.Net.Sockets;
using PidSimulator.Core;
using PidSimulator.Modbus;
using Xunit;

namespace PidSimulator.Tests;

/// <summary>Modbus TCP 服务端端到端测试（使用非特权端口，避免与 502 冲突）。</summary>
public class ModbusTcpServerTests
{
    [Fact]
    public void Fc03_ReadAll_ReturnsExpectedFloats()
    {
        var state = new ControlState { SP = 50f, OP = 12.5f, P = 2f, I = 10f, D = 0f };
        using var server = new ModbusTcpServer(state, new ModbusServerOptions { Port = 15021 });
        server.Start();
        try
        {
            using var client = new TcpClient();
            client.Connect("127.0.0.1", 15021);
            using var ns = client.GetStream();
            ns.ReadTimeout = 3000;

            var req = BuildRead(1, 1, start: 0, quantity: 10);
            ns.Write(req, 0, req.Length);
            var pdu = ReadPdu(ns);

            Assert.Equal(0x03, pdu[0]);
            Assert.Equal(20, pdu[1]); // 字节数

            Assert.Equal(50f, DecodeFloat(pdu, 2));
            Assert.Equal(12.5f, DecodeFloat(pdu, 6));
            Assert.Equal(2f, DecodeFloat(pdu, 10));
            Assert.Equal(10f, DecodeFloat(pdu, 14));
            Assert.Equal(0f, DecodeFloat(pdu, 18));
        }
        finally
        {
            server.Stop();
        }
    }

    [Fact]
    public void Fc16_WriteSp_UpdatesState()
    {
        var state = new ControlState { SP = 0f };
        using var server = new ModbusTcpServer(state, new ModbusServerOptions { Port = 15022 });
        server.Start();
        try
        {
            using var client = new TcpClient();
            client.Connect("127.0.0.1", 15022);
            using var ns = client.GetStream();
            ns.ReadTimeout = 3000;

            var words = FloatCodec.Encode(80f);
            var req = BuildWriteMultiple(1, 1, start: RegisterMap.SpOffset, words);
            ns.Write(req, 0, req.Length);
            var pdu = ReadPdu(ns);

            Assert.Equal(0x10, pdu[0]);
            Assert.Equal(80f, state.SP);
        }
        finally
        {
            server.Stop();
        }
    }

    [Fact]
    public void Fc16_WriteOp_InAutoMode_SwitchesToManual()
    {
        var state = new ControlState { Mode = ControlMode.Auto, OP = 0f };
        using var server = new ModbusTcpServer(state, new ModbusServerOptions { Port = 15023 });
        server.Start();
        try
        {
            using var client = new TcpClient();
            client.Connect("127.0.0.1", 15023);
            using var ns = client.GetStream();
            ns.ReadTimeout = 3000;

            var words = FloatCodec.Encode(33f);
            var req = BuildWriteMultiple(1, 1, start: RegisterMap.OpOffset, words);
            ns.Write(req, 0, req.Length);
            ReadPdu(ns);

            Assert.Equal(ControlMode.Manual, state.Mode);
            Assert.Equal(33f, state.OP);
        }
        finally
        {
            server.Stop();
        }
    }

    [Fact]
    public void Fc16_WriteSp_RaisesValuesWrittenAndLogs()
    {
        var state = new ControlState { SP = 0f };
        using var server = new ModbusTcpServer(state, new ModbusServerOptions { Port = 15024 });
        var writes = new List<ModbusWrite>();
        var logs = new List<string>();
        using var signal = new ManualResetEventSlim(false);

        server.ValuesWritten += w => { writes.AddRange(w); signal.Set(); };
        server.Logged += s => logs.Add(s);

        server.Start();
        try
        {
            using var client = new TcpClient();
            client.Connect("127.0.0.1", 15024);
            using var ns = client.GetStream();
            ns.ReadTimeout = 3000;

            var req = BuildWriteMultiple(1, 1, RegisterMap.SpOffset, FloatCodec.Encode(80f));
            ns.Write(req, 0, req.Length);
            ReadPdu(ns);

            Assert.True(signal.Wait(2000), "应触发 ValuesWritten 事件");
            Assert.Contains(writes, w => w.Name == "SP" && MathF.Abs(w.Value - 80f) < 1e-4f);
            Assert.Contains(logs, s => s.Contains("Modbus 写入") && s.Contains("SP"));
        }
        finally
        {
            server.Stop();
        }
    }

    [Fact]
    public void Fc16_WriteOp_InAuto_RaisesEventWithOpAndLogsSwitch()
    {
        var state = new ControlState { Mode = ControlMode.Auto, OP = 0f };
        using var server = new ModbusTcpServer(state, new ModbusServerOptions { Port = 15025 });
        var writes = new List<ModbusWrite>();
        var logs = new List<string>();
        using var signal = new ManualResetEventSlim(false);

        server.ValuesWritten += w => { writes.AddRange(w); signal.Set(); };
        server.Logged += s => logs.Add(s);

        server.Start();
        try
        {
            using var client = new TcpClient();
            client.Connect("127.0.0.1", 15025);
            using var ns = client.GetStream();
            ns.ReadTimeout = 3000;

            var req = BuildWriteMultiple(1, 1, RegisterMap.OpOffset, FloatCodec.Encode(33f));
            ns.Write(req, 0, req.Length);
            ReadPdu(ns);

            Assert.True(signal.Wait(2000), "应触发 ValuesWritten 事件");
            Assert.Contains(writes, w => w.Name == "OP" && MathF.Abs(w.Value - 33f) < 1e-4f);
            Assert.Contains(logs, s => s.Contains("切换到手动"));
        }
        finally
        {
            server.Stop();
        }
    }

    [Fact]
    public void Fc03_ReadPv_ReturnsProcessValue()
    {
        var state = new ControlState { PV = 42.5f };
        using var server = new ModbusTcpServer(state, new ModbusServerOptions { Port = 15026 });
        server.Start();
        try
        {
            using var client = new TcpClient();
            client.Connect("127.0.0.1", 15026);
            using var ns = client.GetStream();
            ns.ReadTimeout = 3000;

            var req = BuildRead(1, 1, start: RegisterMap.PvOffset, quantity: 2);
            ns.Write(req, 0, req.Length);
            var pdu = ReadPdu(ns);

            Assert.Equal(0x03, pdu[0]);
            Assert.Equal(42.5f, DecodeFloat(pdu, 2));
        }
        finally
        {
            server.Stop();
        }
    }

    [Fact]
    public void Fc16_WritePv_IsRejected_AndValueUnchanged()
    {
        var state = new ControlState { PV = 10f };
        using var server = new ModbusTcpServer(state, new ModbusServerOptions { Port = 15027 });
        server.Start();
        try
        {
            using var client = new TcpClient();
            client.Connect("127.0.0.1", 15027);
            using var ns = client.GetStream();
            ns.ReadTimeout = 3000;

            var req = BuildWriteMultiple(1, 1, RegisterMap.PvOffset, FloatCodec.Encode(99f));
            ns.Write(req, 0, req.Length);
            var pdu = ReadPdu(ns);

            Assert.Equal(0x90, pdu[0]); // 0x10 | 0x80 异常
            Assert.Equal(0x02, pdu[1]); // IllegalDataAddress
            Assert.Equal(10f, state.PV);
        }
        finally
        {
            server.Stop();
        }
    }

    // —— 辅助 ——
    private static float DecodeFloat(byte[] pdu, int pduOffset)
    {
        ushort hi = (ushort)((pdu[pduOffset] << 8) | pdu[pduOffset + 1]);
        ushort lo = (ushort)((pdu[pduOffset + 2] << 8) | pdu[pduOffset + 3]);
        return FloatCodec.Decode(hi, lo);
    }

    private static byte[] BuildRead(ushort txId, byte unitId, int start, int quantity)
    {
        var pdu = new byte[]
        {
            0x03,
            (byte)(start >> 8), (byte)start,
            (byte)(quantity >> 8), (byte)quantity
        };
        return Frame(txId, unitId, pdu);
    }

    private static byte[] BuildWriteMultiple(ushort txId, byte unitId, int start, ushort[] values)
    {
        int quantity = values.Length;
        var pdu = new byte[6 + quantity * 2];
        pdu[0] = 0x10;
        pdu[1] = (byte)(start >> 8);
        pdu[2] = (byte)start;
        pdu[3] = (byte)(quantity >> 8);
        pdu[4] = (byte)quantity;
        pdu[5] = (byte)(quantity * 2);
        for (int i = 0; i < quantity; i++)
        {
            pdu[6 + i * 2] = (byte)(values[i] >> 8);
            pdu[7 + i * 2] = (byte)(values[i] & 0xFF);
        }
        return Frame(txId, unitId, pdu);
    }

    private static byte[] Frame(ushort txId, byte unitId, byte[] pdu)
    {
        var buffer = new byte[7 + pdu.Length];
        buffer[0] = (byte)(txId >> 8);
        buffer[1] = (byte)(txId & 0xFF);
        buffer[2] = 0;
        buffer[3] = 0;
        int len = pdu.Length + 1;
        buffer[4] = (byte)(len >> 8);
        buffer[5] = (byte)(len & 0xFF);
        buffer[6] = unitId;
        Array.Copy(pdu, 0, buffer, 7, pdu.Length);
        return buffer;
    }

    private static byte[] ReadPdu(NetworkStream ns)
    {
        var header = ReadExact(ns, 7);
        int len = (header[4] << 8) | header[5];
        return ReadExact(ns, len - 1);
    }

    private static byte[] ReadExact(NetworkStream ns, int count)
    {
        var buffer = new byte[count];
        int offset = 0;
        while (offset < count)
        {
            int read = ns.Read(buffer, offset, count - offset);
            if (read <= 0) throw new IOException("连接已关闭");
            offset += read;
        }
        return buffer;
    }
}
