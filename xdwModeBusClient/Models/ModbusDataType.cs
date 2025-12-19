namespace xdwModeBusClient.Models;

/// <summary>
/// Modbus数据类型枚举
/// </summary>
public enum ModbusDataType
{
    /// <summary>
    /// 16位无符号整数
    /// </summary>
    UInt16 = 0,

    /// <summary>
    /// 16位有符号整数
    /// </summary>
    Int16 = 1,

    /// <summary>
    /// 32位无符号整数
    /// </summary>
    UInt32 = 2,

    /// <summary>
    /// 32位有符号整数
    /// </summary>
    Int32 = 3,

    /// <summary>
    /// 32位浮点数
    /// </summary>
    Float32 = 4,

    /// <summary>
    /// 64位无符号整数
    /// </summary>
    UInt64 = 5,

    /// <summary>
    /// 64位有符号整数
    /// </summary>
    Int64 = 6,

    /// <summary>
    /// 64位浮点数
    /// </summary>
    Float64 = 7,

    /// <summary>
    /// 布尔值(线圈/离散输入)
    /// </summary>
    Boolean = 8,

    /// <summary>
    /// ASCII字符串
    /// </summary>
    String = 9
}

/// <summary>
/// 字节顺序枚举
/// </summary>
public enum ByteOrder
{
    /// <summary>
    /// 大端模式 (高字节在前) - ABCD
    /// </summary>
    BigEndian = 0,

    /// <summary>
    /// 小端模式 (低字节在前) - DCBA
    /// </summary>
    LittleEndian = 1,

    /// <summary>
    /// 大端字节交换 - BADC
    /// </summary>
    BigEndianByteSwap = 2,

    /// <summary>
    /// 小端字节交换 - CDAB
    /// </summary>
    LittleEndianByteSwap = 3
}

/// <summary>
/// Modbus功能码枚举
/// </summary>
public enum ModbusFunctionCode
{
    /// <summary>
    /// 读取线圈状态 (0x01)
    /// </summary>
    ReadCoils = 0x01,

    /// <summary>
    /// 读取离散输入 (0x02)
    /// </summary>
    ReadDiscreteInputs = 0x02,

    /// <summary>
    /// 读取保持寄存器 (0x03)
    /// </summary>
    ReadHoldingRegisters = 0x03,

    /// <summary>
    /// 读取输入寄存器 (0x04)
    /// </summary>
    ReadInputRegisters = 0x04,

    /// <summary>
    /// 写单个线圈 (0x05)
    /// </summary>
    WriteSingleCoil = 0x05,

    /// <summary>
    /// 写单个寄存器 (0x06)
    /// </summary>
    WriteSingleRegister = 0x06,

    /// <summary>
    /// 写多个线圈 (0x0F)
    /// </summary>
    WriteMultipleCoils = 0x0F,

    /// <summary>
    /// 写多个寄存器 (0x10)
    /// </summary>
    WriteMultipleRegisters = 0x10
}

/// <summary>
/// 连接类型枚举
/// </summary>
public enum ConnectionType
{
    /// <summary>
    /// Modbus TCP
    /// </summary>
    TCP = 0,

    /// <summary>
    /// Modbus RTU
    /// </summary>
    RTU = 1
}
