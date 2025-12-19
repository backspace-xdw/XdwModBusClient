using xdwModeBusClient.Models;

namespace xdwModeBusClient.Configuration;

/// <summary>
/// 主配置类，包含所有Modbus配置 (只读模式)
/// </summary>
public class ModbusConfiguration
{
    /// <summary>
    /// 应用程序设置
    /// </summary>
    public AppSettings AppSettings { get; set; } = new();

    /// <summary>
    /// 连接配置列表（支持多个连接）
    /// </summary>
    public List<ConnectionConfig> Connections { get; set; } = [];

    /// <summary>
    /// 数据包配置列表（支持多个数据包循环读取）
    /// </summary>
    public List<PacketConfig> Packets { get; set; } = [];
}

/// <summary>
/// 应用程序设置
/// </summary>
public class AppSettings
{
    /// <summary>
    /// 轮询间隔（毫秒）
    /// </summary>
    public int PollingIntervalMs { get; set; } = 1000;

    /// <summary>
    /// 请求超时时间（毫秒）
    /// </summary>
    public int RequestTimeoutMs { get; set; } = 3000;

    /// <summary>
    /// 重试次数
    /// </summary>
    public int RetryCount { get; set; } = 3;

    /// <summary>
    /// 重试间隔（毫秒）
    /// </summary>
    public int RetryIntervalMs { get; set; } = 500;

    /// <summary>
    /// 启用日志
    /// </summary>
    public bool EnableLogging { get; set; } = true;

    /// <summary>
    /// 日志文件路径
    /// </summary>
    public string LogFilePath { get; set; } = "logs/modbus.log";

    /// <summary>
    /// 启用数据存储
    /// </summary>
    public bool EnableDataStorage { get; set; } = true;

    /// <summary>
    /// 数据存储路径
    /// </summary>
    public string DataStoragePath { get; set; } = "data/";

    /// <summary>
    /// 请求间的延迟时间（毫秒），用于避免通讯过于频繁
    /// </summary>
    public int RequestDelayMs { get; set; } = 50;
}

/// <summary>
/// 连接配置
/// </summary>
public class ConnectionConfig
{
    /// <summary>
    /// 连接ID（唯一标识）
    /// </summary>
    public string ConnectionId { get; set; } = string.Empty;

    /// <summary>
    /// 连接名称（描述性名称）
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// 连接类型（TCP或RTU）
    /// </summary>
    public ConnectionType ConnectionType { get; set; } = ConnectionType.TCP;

    /// <summary>
    /// 启用状态
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// TCP配置（当ConnectionType为TCP时使用）
    /// </summary>
    public TcpConfig? TcpConfig { get; set; }

    /// <summary>
    /// RTU配置（当ConnectionType为RTU时使用）
    /// </summary>
    public RtuConfig? RtuConfig { get; set; }
}

/// <summary>
/// Modbus TCP配置
/// </summary>
public class TcpConfig
{
    /// <summary>
    /// 服务器IP地址
    /// </summary>
    public string IpAddress { get; set; } = "127.0.0.1";

    /// <summary>
    /// 服务器端口
    /// </summary>
    public int Port { get; set; } = 502;

    /// <summary>
    /// 连接超时时间（毫秒）
    /// </summary>
    public int ConnectTimeoutMs { get; set; } = 5000;

    /// <summary>
    /// 保持连接
    /// </summary>
    public bool KeepAlive { get; set; } = true;
}

/// <summary>
/// Modbus RTU配置
/// </summary>
public class RtuConfig
{
    /// <summary>
    /// 串口名称 (如 COM1, /dev/ttyUSB0)
    /// </summary>
    public string PortName { get; set; } = "COM1";

    /// <summary>
    /// 波特率
    /// </summary>
    public int BaudRate { get; set; } = 9600;

    /// <summary>
    /// 数据位 (5, 6, 7, 8)
    /// </summary>
    public int DataBits { get; set; } = 8;

    /// <summary>
    /// 停止位 (1, 1.5, 2)
    /// </summary>
    public StopBitsConfig StopBits { get; set; } = StopBitsConfig.One;

    /// <summary>
    /// 奇偶校验
    /// </summary>
    public ParityConfig Parity { get; set; } = ParityConfig.None;

    /// <summary>
    /// 读取超时（毫秒）
    /// </summary>
    public int ReadTimeoutMs { get; set; } = 1000;

    /// <summary>
    /// 写入超时（毫秒）
    /// </summary>
    public int WriteTimeoutMs { get; set; } = 1000;

    /// <summary>
    /// 帧间隔时间（毫秒），RTU模式下帧之间的最小间隔
    /// </summary>
    public int FrameIntervalMs { get; set; } = 50;
}

/// <summary>
/// 停止位配置枚举
/// </summary>
public enum StopBitsConfig
{
    None = 0,
    One = 1,
    Two = 2,
    OnePointFive = 3
}

/// <summary>
/// 奇偶校验配置枚举
/// </summary>
public enum ParityConfig
{
    None = 0,
    Odd = 1,
    Even = 2,
    Mark = 3,
    Space = 4
}

/// <summary>
/// 数据包配置 (只读)
/// </summary>
public class PacketConfig
{
    /// <summary>
    /// 数据包ID（唯一标识）
    /// </summary>
    public string PacketId { get; set; } = string.Empty;

    /// <summary>
    /// 数据包名称（描述性名称）
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// 关联的连接ID
    /// </summary>
    public string ConnectionId { get; set; } = string.Empty;

    /// <summary>
    /// 启用状态
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// 从站地址 (1-247)
    /// </summary>
    public byte SlaveId { get; set; } = 1;

    /// <summary>
    /// 功能码 (仅支持读取: FC01/02/03/04)
    /// </summary>
    public ModbusFunctionCode FunctionCode { get; set; } = ModbusFunctionCode.ReadHoldingRegisters;

    /// <summary>
    /// 起始寄存器地址
    /// </summary>
    public ushort StartAddress { get; set; } = 0;

    /// <summary>
    /// 寄存器数量 (单次请求最大126个寄存器)
    /// </summary>
    public ushort RegisterCount { get; set; } = 1;

    /// <summary>
    /// 轮询间隔（毫秒），0表示使用全局设置
    /// </summary>
    public int PollingIntervalMs { get; set; } = 0;

    /// <summary>
    /// 数据点配置列表
    /// </summary>
    public List<DataPointConfig> DataPoints { get; set; } = [];
}

/// <summary>
/// 数据点配置
/// </summary>
public class DataPointConfig
{
    /// <summary>
    /// 数据点ID（唯一标识）
    /// </summary>
    public string PointId { get; set; } = string.Empty;

    /// <summary>
    /// 数据点名称
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// 相对于包起始地址的偏移量（寄存器偏移）
    /// </summary>
    public ushort Offset { get; set; } = 0;

    /// <summary>
    /// 数据类型
    /// </summary>
    public ModbusDataType DataType { get; set; } = ModbusDataType.UInt16;

    /// <summary>
    /// 字节顺序
    /// </summary>
    public ByteOrder ByteOrder { get; set; } = ByteOrder.BigEndian;

    /// <summary>
    /// 缩放系数（原始值 * Scale = 实际值）
    /// </summary>
    public double Scale { get; set; } = 1.0;

    /// <summary>
    /// 偏移量（原始值 * Scale + Offset = 实际值）
    /// </summary>
    public double OffsetValue { get; set; } = 0.0;

    /// <summary>
    /// 单位
    /// </summary>
    public string Unit { get; set; } = string.Empty;

    /// <summary>
    /// 描述
    /// </summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// 位索引（用于位操作，-1表示不使用位操作）
    /// </summary>
    public int BitIndex { get; set; } = -1;

    /// <summary>
    /// 字符串长度（当DataType为String时使用）
    /// </summary>
    public int StringLength { get; set; } = 0;
}
