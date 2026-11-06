using System.Net;
using System.Net.Sockets;
using PidSimulator.Core;

namespace PidSimulator.Modbus;

/// <summary>
/// 轻量 Modbus TCP 服务端（从站）。支持 FC03 读保持寄存器、FC06 写单寄存器、FC16 写多寄存器。
/// 读写直接作用于 <see cref="ControlState"/>（在 Sync 锁内），float 按大端 ABCD 编解码。
/// 详见设计文档 §6 与 §14.6。
/// </summary>
public sealed class ModbusTcpServer : IModbusServer, IDisposable
{
    private readonly ControlState _state;
    private readonly ModbusServerOptions _opt;

    private readonly object _lifecycle = new();
    private TcpListener? _listener;
    private CancellationTokenSource? _cts;
    private int _connections;

    public bool IsRunning { get; private set; }

    public event Action<string>? Logged;
    public event Action<bool>? ListeningChanged;
    public event Action<IReadOnlyList<ModbusWrite>>? ValuesWritten;

    public ModbusTcpServer(ControlState state, ModbusServerOptions options)
    {
        _state = state;
        _opt = options;
    }

    public void Start()
    {
        lock (_lifecycle)
        {
            if (IsRunning) return;

            try
            {
                var address = IPAddress.Parse(_opt.ListenAddress);
                _listener = new TcpListener(address, _opt.Port);
                _listener.Start();
                _cts = new CancellationTokenSource();
                IsRunning = true;

                _ = AcceptLoopAsync(_listener, _cts.Token);

                Log($"Modbus TCP 已监听 {_opt.ListenAddress}:{_opt.Port} (UnitId={_opt.UnitId}, 最大连接={_opt.MaxConnections})");
                ListeningChanged?.Invoke(true);
            }
            catch (Exception ex)
            {
                IsRunning = false;
                _listener = null;
                Log($"Modbus 启动失败：{ex.Message}");
                ListeningChanged?.Invoke(false);
                throw;
            }
        }
    }

    public void Stop()
    {
        lock (_lifecycle)
        {
            if (!IsRunning) return;
            try { _cts?.Cancel(); } catch { /* ignore */ }
            try { _listener?.Stop(); } catch { /* ignore */ }
            _cts?.Dispose();
            _cts = null;
            _listener = null;
            IsRunning = false;
            Log("Modbus TCP 已停止");
            ListeningChanged?.Invoke(false);
        }
    }

    private async Task AcceptLoopAsync(TcpListener listener, CancellationToken token)
    {
        while (!token.IsCancellationRequested)
        {
            TcpClient client;
            try
            {
                client = await listener.AcceptTcpClientAsync(token).ConfigureAwait(false);
            }
            catch (OperationCanceledException) { break; }
            catch (ObjectDisposedException) { break; }
            catch (Exception ex)
            {
                Log($"接受连接异常：{ex.Message}");
                continue;
            }

            if (Volatile.Read(ref _connections) >= _opt.MaxConnections)
            {
                Log("已达最大连接数，拒绝新连接");
                client.Close();
                continue;
            }

            Interlocked.Increment(ref _connections);
            _ = HandleClientAsync(client, token);
        }
    }

    private async Task HandleClientAsync(TcpClient client, CancellationToken token)
    {
        string remote = client.Client.RemoteEndPoint?.ToString() ?? "?";
        Log($"客户端已连接：{remote}");
        try
        {
            using (client)
            using (var stream = client.GetStream())
            {
                var header = new byte[7];
                while (!token.IsCancellationRequested)
                {
                    if (!await ReadExactAsync(stream, header, 7, token).ConfigureAwait(false))
                        break;

                    ushort txId = (ushort)((header[0] << 8) | header[1]);
                    ushort protoId = (ushort)((header[2] << 8) | header[3]);
                    ushort length = (ushort)((header[4] << 8) | header[5]);
                    byte unitId = header[6];

                    int pduLen = length - 1;
                    if (protoId != 0 || pduLen <= 0 || pduLen > 260)
                        break;

                    var pdu = new byte[pduLen];
                    if (!await ReadExactAsync(stream, pdu, pduLen, token).ConfigureAwait(false))
                        break;

                    var (respPdu, isException) = ProcessPdu(pdu);
                    if (isException)
                        Log($"请求异常：FC=0x{pdu[0]:X2} 异常码=0x{respPdu[1]:X2}");

                    var response = BuildResponse(txId, unitId, respPdu);
                    await stream.WriteAsync(response, token).ConfigureAwait(false);
                }
            }
        }
        catch (Exception ex)
        {
            Log($"客户端处理异常：{ex.Message}");
        }
        finally
        {
            Interlocked.Decrement(ref _connections);
            Log($"客户端已断开：{remote}");
        }
    }

    private (byte[] pdu, bool isException) ProcessPdu(byte[] pdu)
    {
        if (pdu.Length < 1)
            return (Exception(pdu.Length > 0 ? pdu[0] : (byte)0, ModbusExceptionCode.IllegalFunction), true);

        return pdu[0] switch
        {
            (byte)ModbusFunctionCode.ReadHoldingRegisters => HandleReadHolding(pdu),
            (byte)ModbusFunctionCode.WriteSingleRegister => HandleWriteSingle(pdu),
            (byte)ModbusFunctionCode.WriteMultipleRegisters => HandleWriteMultiple(pdu),
            _ => (Exception(pdu[0], ModbusExceptionCode.IllegalFunction), true)
        };
    }

    private (byte[], bool) HandleReadHolding(byte[] pdu)
    {
        if (pdu.Length < 5)
            return (Exception(pdu[0], ModbusExceptionCode.IllegalDataValue), true);

        int start = (pdu[1] << 8) | pdu[2];
        int quantity = (pdu[3] << 8) | pdu[4];

        if (quantity < 1 || quantity > 125)
            return (Exception(pdu[0], ModbusExceptionCode.IllegalDataValue), true);

        if (start < 0 || start + quantity > RegisterMap.TotalRegisters)
            return (Exception(pdu[0], ModbusExceptionCode.IllegalDataAddress), true);

        ushort[] regs;
        lock (_state.Sync)
            regs = BuildRegistersLocked();

        var resp = new byte[2 + quantity * 2];
        resp[0] = pdu[0];
        resp[1] = (byte)(quantity * 2);
        for (int i = 0; i < quantity; i++)
        {
            ushort v = regs[start + i];
            resp[2 + i * 2] = (byte)(v >> 8);
            resp[3 + i * 2] = (byte)(v & 0xFF);
        }
        return (resp, false);
    }

    private (byte[], bool) HandleWriteSingle(byte[] pdu)
    {
        if (pdu.Length < 5)
            return (Exception(pdu[0], ModbusExceptionCode.IllegalDataValue), true);

        int addr = (pdu[1] << 8) | pdu[2];
        ushort value = (ushort)((pdu[3] << 8) | pdu[4]);

        if (addr < 0 || addr >= RegisterMap.TotalRegisters)
            return (Exception(pdu[0], ModbusExceptionCode.IllegalDataAddress), true);

        if (IsReadOnlyRegister(addr))
            return (Exception(pdu[0], ModbusExceptionCode.IllegalDataAddress), true);

        List<ModbusWrite> changes;
        bool switchedToManual;
        lock (_state.Sync)
        {
            var regs = BuildRegistersLocked();
            regs[addr] = value;
            (changes, switchedToManual) = ApplyRegistersLocked(regs);
        }

        ReportWrites(changes, switchedToManual);

        // 回显请求（地址 + 值）
        return (new[] { pdu[0], pdu[1], pdu[2], pdu[3], pdu[4] }, false);
    }

    private (byte[], bool) HandleWriteMultiple(byte[] pdu)
    {
        if (pdu.Length < 6)
            return (Exception(pdu[0], ModbusExceptionCode.IllegalDataValue), true);

        int start = (pdu[1] << 8) | pdu[2];
        int quantity = (pdu[3] << 8) | pdu[4];
        int byteCount = pdu[5];

        if (quantity < 1 || quantity > 123 || byteCount != quantity * 2 || pdu.Length < 6 + byteCount)
            return (Exception(pdu[0], ModbusExceptionCode.IllegalDataValue), true);

        if (start < 0 || start + quantity > RegisterMap.TotalRegisters)
            return (Exception(pdu[0], ModbusExceptionCode.IllegalDataAddress), true);

        for (int i = 0; i < quantity; i++)
        {
            if (IsReadOnlyRegister(start + i))
                return (Exception(pdu[0], ModbusExceptionCode.IllegalDataAddress), true);
        }

        List<ModbusWrite> changes;
        bool switchedToManual;
        lock (_state.Sync)
        {
            var regs = BuildRegistersLocked();
            for (int i = 0; i < quantity; i++)
            {
                ushort v = (ushort)((pdu[6 + i * 2] << 8) | pdu[7 + i * 2]);
                regs[start + i] = v;
            }
            (changes, switchedToManual) = ApplyRegistersLocked(regs);
        }

        ReportWrites(changes, switchedToManual);

        // 回显功能码 + 起始地址 + 数量
        return (new[] { pdu[0], pdu[1], pdu[2], pdu[3], pdu[4] }, false);
    }

    /// <summary>按当前状态构建寄存器区快照（调用方需持有 Sync 锁）。</summary>
    private ushort[] BuildRegistersLocked()
    {
        var regs = new ushort[RegisterMap.TotalRegisters];
        RegisterMap.WriteFloat(regs, RegisterMap.SpOffset, _state.SP, _opt.WordSwap);
        RegisterMap.WriteFloat(regs, RegisterMap.OpOffset, _state.OP, _opt.WordSwap);
        RegisterMap.WriteFloat(regs, RegisterMap.POffset, _state.P, _opt.WordSwap);
        RegisterMap.WriteFloat(regs, RegisterMap.IOffset, _state.I, _opt.WordSwap);
        RegisterMap.WriteFloat(regs, RegisterMap.DOffset, _state.D, _opt.WordSwap);
        RegisterMap.WriteFloat(regs, RegisterMap.PvOffset, _state.PV, _opt.WordSwap);
        return regs;
    }

    /// <summary>
    /// 将寄存器区写回状态，含校验、限幅与 OP 联动（调用方需持有 Sync 锁）。
    /// 返回实际发生变化的字段列表与是否触发了自动→手动切换，供调用方在锁外记日志/通知。
    /// </summary>
    private (List<ModbusWrite> changes, bool switchedToManual) ApplyRegistersLocked(ushort[] regs)
    {
        var changes = new List<ModbusWrite>();

        float sp = RegisterMap.ReadFloat(regs, RegisterMap.SpOffset, _opt.WordSwap);
        float op = RegisterMap.ReadFloat(regs, RegisterMap.OpOffset, _opt.WordSwap);
        float p = RegisterMap.ReadFloat(regs, RegisterMap.POffset, _opt.WordSwap);
        float i = RegisterMap.ReadFloat(regs, RegisterMap.IOffset, _opt.WordSwap);
        float d = RegisterMap.ReadFloat(regs, RegisterMap.DOffset, _opt.WordSwap);

        float newSp = Clamp(sp, _state.SpMin, _state.SpMax);
        if (!NearlyEqual(newSp, _state.SP))
        {
            _state.SP = newSp;
            changes.Add(new ModbusWrite("SP", RegisterMap.SpOffset, newSp));
        }

        float newP = MathF.Max(0f, p);
        if (!NearlyEqual(newP, _state.P))
        {
            _state.P = newP;
            changes.Add(new ModbusWrite("P", RegisterMap.POffset, newP));
        }

        float newI = MathF.Max(0f, i);
        if (!NearlyEqual(newI, _state.I))
        {
            _state.I = newI;
            changes.Add(new ModbusWrite("I", RegisterMap.IOffset, newI));
        }

        float newD = MathF.Max(0f, d);
        if (!NearlyEqual(newD, _state.D))
        {
            _state.D = newD;
            changes.Add(new ModbusWrite("D", RegisterMap.DOffset, newD));
        }

        bool switchedToManual = false;
        if (_state.Mode == ControlMode.Manual)
        {
            float newOp = Clamp(op, _state.OpMin, _state.OpMax);
            if (!NearlyEqual(newOp, _state.OP))
            {
                _state.OP = newOp;
                changes.Add(new ModbusWrite("OP", RegisterMap.OpOffset, newOp));
            }
        }
        else if (_opt.OpWriteSwitchesToManual && !NearlyEqual(op, _state.OP))
        {
            // 自动模式外部写 OP → 切手动（无扰）
            _state.Mode = ControlMode.Manual;
            float newOp = Clamp(op, _state.OpMin, _state.OpMax);
            _state.OP = newOp;
            changes.Add(new ModbusWrite("OP", RegisterMap.OpOffset, newOp));
            switchedToManual = true;
        }

        return (changes, switchedToManual);
    }

    /// <summary>在锁外记日志并广播写入变更（避免持锁回调 UI 造成死锁）。</summary>
    private void ReportWrites(List<ModbusWrite> changes, bool switchedToManual)
    {
        foreach (var change in changes)
            Log($"Modbus 写入：{change.Name} = {change.Value:F2}（地址 {change.Reference}）");

        if (switchedToManual)
            Log("外部写入 OP → 已切换到手动模式");

        if (changes.Count > 0)
            ValuesWritten?.Invoke(changes);
    }

    /// <summary>该寄存器偏移是否属于只读区（PV，4011–4012）。</summary>
    private static bool IsReadOnlyRegister(int offset)
        => offset >= RegisterMap.PvOffset && offset < RegisterMap.PvOffset + 2;

    private static byte[] BuildResponse(ushort txId, byte unitId, byte[] pdu)    {
        var resp = new byte[7 + pdu.Length];
        resp[0] = (byte)(txId >> 8);
        resp[1] = (byte)(txId & 0xFF);
        resp[2] = 0;
        resp[3] = 0;
        int len = pdu.Length + 1; // UnitId + PDU
        resp[4] = (byte)(len >> 8);
        resp[5] = (byte)(len & 0xFF);
        resp[6] = unitId;
        Array.Copy(pdu, 0, resp, 7, pdu.Length);
        return resp;
    }

    private static byte[] Exception(byte fc, ModbusExceptionCode code)
        => new[] { (byte)(fc | 0x80), (byte)code };

    private static async Task<bool> ReadExactAsync(NetworkStream stream, byte[] buffer, int count, CancellationToken token)
    {
        int offset = 0;
        while (offset < count)
        {
            int read;
            try
            {
                read = await stream.ReadAsync(buffer.AsMemory(offset, count - offset), token).ConfigureAwait(false);
            }
            catch (OperationCanceledException) { return false; }
            if (read == 0) return false;
            offset += read;
        }
        return true;
    }

    private static float Clamp(float v, float lo, float hi) => v < lo ? lo : (v > hi ? hi : v);

    private static bool NearlyEqual(float a, float b) => MathF.Abs(a - b) <= 1e-4f;

    private void Log(string message) => Logged?.Invoke($"{DateTime.Now:HH:mm:ss.fff} {message}");

    public void Dispose() => Stop();
}
