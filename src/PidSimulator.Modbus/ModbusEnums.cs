namespace PidSimulator.Modbus;

/// <summary>支持的 Modbus 功能码。</summary>
public enum ModbusFunctionCode : byte
{
    ReadHoldingRegisters = 0x03,
    WriteSingleRegister = 0x06,
    WriteMultipleRegisters = 0x10
}

/// <summary>Modbus 异常码。</summary>
public enum ModbusExceptionCode : byte
{
    IllegalFunction = 0x01,
    IllegalDataAddress = 0x02,
    IllegalDataValue = 0x03
}
