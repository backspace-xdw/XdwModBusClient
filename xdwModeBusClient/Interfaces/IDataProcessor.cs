using xdwModeBusClient.Configuration;
using xdwModeBusClient.Models;

namespace xdwModeBusClient.Interfaces;

/// <summary>
/// 数据处理器接口 - 用于处理采集到的Modbus数据
/// </summary>
public interface IDataProcessor
{
    /// <summary>
    /// 处理采集到的数据
    /// </summary>
    /// <param name="result">采集结果</param>
    /// <param name="cancellationToken">取消令牌</param>
    Task ProcessDataAsync(PollingResult result, CancellationToken cancellationToken = default);
}

/// <summary>
/// 数据存储接口 - 用于持久化存储数据
/// </summary>
public interface IDataStorage
{
    /// <summary>
    /// 存储数据
    /// </summary>
    /// <param name="result">采集结果</param>
    /// <param name="cancellationToken">取消令牌</param>
    Task StoreAsync(PollingResult result, CancellationToken cancellationToken = default);

    /// <summary>
    /// 查询历史数据
    /// </summary>
    /// <param name="packetId">数据包ID</param>
    /// <param name="startTime">开始时间</param>
    /// <param name="endTime">结束时间</param>
    /// <param name="cancellationToken">取消令牌</param>
    Task<IEnumerable<PollingResult>> QueryAsync(string packetId, DateTime startTime, DateTime endTime, CancellationToken cancellationToken = default);
}

/// <summary>
/// 数据显示接口 - 用于展示数据
/// </summary>
public interface IDataDisplay
{
    /// <summary>
    /// 显示数据
    /// </summary>
    /// <param name="result">采集结果</param>
    void Display(PollingResult result);

    /// <summary>
    /// 显示错误
    /// </summary>
    /// <param name="packetId">数据包ID</param>
    /// <param name="errorMessage">错误消息</param>
    void DisplayError(string packetId, string errorMessage);

    /// <summary>
    /// 显示连接状态
    /// </summary>
    /// <param name="connectionId">连接ID</param>
    /// <param name="isConnected">是否已连接</param>
    void DisplayConnectionStatus(string connectionId, bool isConnected);
}

/// <summary>
/// 数据转换接口 - 用于数据格式转换
/// </summary>
public interface IDataConverter
{
    /// <summary>
    /// 将寄存器数据转换为指定类型
    /// </summary>
    /// <param name="registers">寄存器数据</param>
    /// <param name="offset">偏移量</param>
    /// <param name="dataType">数据类型</param>
    /// <param name="byteOrder">字节顺序</param>
    /// <returns>转换后的值</returns>
    object ConvertFromRegisters(ushort[] registers, int offset, ModbusDataType dataType, ByteOrder byteOrder);

    /// <summary>
    /// 将值转换为寄存器数据
    /// </summary>
    /// <param name="value">值</param>
    /// <param name="dataType">数据类型</param>
    /// <param name="byteOrder">字节顺序</param>
    /// <returns>寄存器数据</returns>
    ushort[] ConvertToRegisters(object value, ModbusDataType dataType, ByteOrder byteOrder);
}

/// <summary>
/// 轮询结果
/// </summary>
public class PollingResult
{
    /// <summary>
    /// 数据包ID
    /// </summary>
    public string PacketId { get; set; } = string.Empty;

    /// <summary>
    /// 数据包名称
    /// </summary>
    public string PacketName { get; set; } = string.Empty;

    /// <summary>
    /// 连接ID
    /// </summary>
    public string ConnectionId { get; set; } = string.Empty;

    /// <summary>
    /// 是否成功
    /// </summary>
    public bool Success { get; set; }

    /// <summary>
    /// 错误消息
    /// </summary>
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// 采集时间戳
    /// </summary>
    public DateTime Timestamp { get; set; } = DateTime.Now;

    /// <summary>
    /// 响应时间（毫秒）
    /// </summary>
    public long ResponseTimeMs { get; set; }

    /// <summary>
    /// 从站地址
    /// </summary>
    public byte SlaveId { get; set; }

    /// <summary>
    /// 功能码
    /// </summary>
    public ModbusFunctionCode FunctionCode { get; set; }

    /// <summary>
    /// 起始地址
    /// </summary>
    public ushort StartAddress { get; set; }

    /// <summary>
    /// 原始寄存器数据
    /// </summary>
    public ushort[]? RawRegisters { get; set; }

    /// <summary>
    /// 原始线圈数据
    /// </summary>
    public bool[]? RawCoils { get; set; }

    /// <summary>
    /// 解析后的数据点值
    /// </summary>
    public List<DataPointValue> DataPointValues { get; set; } = [];

    /// <summary>
    /// 原始请求数据
    /// </summary>
    public byte[]? RawRequest { get; set; }

    /// <summary>
    /// 原始响应数据
    /// </summary>
    public byte[]? RawResponse { get; set; }
}

/// <summary>
/// 数据点值
/// </summary>
public class DataPointValue
{
    /// <summary>
    /// 数据点ID
    /// </summary>
    public string PointId { get; set; } = string.Empty;

    /// <summary>
    /// 数据点名称
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// 原始值
    /// </summary>
    public object? RawValue { get; set; }

    /// <summary>
    /// 缩放后的值
    /// </summary>
    public object? ScaledValue { get; set; }

    /// <summary>
    /// 单位
    /// </summary>
    public string Unit { get; set; } = string.Empty;

    /// <summary>
    /// 数据类型
    /// </summary>
    public ModbusDataType DataType { get; set; }

    /// <summary>
    /// 质量（好/坏）
    /// </summary>
    public DataQuality Quality { get; set; } = DataQuality.Good;

    /// <summary>
    /// 时间戳
    /// </summary>
    public DateTime Timestamp { get; set; } = DateTime.Now;
}

/// <summary>
/// 数据质量枚举
/// </summary>
public enum DataQuality
{
    /// <summary>
    /// 好
    /// </summary>
    Good = 0,

    /// <summary>
    /// 坏
    /// </summary>
    Bad = 1,

    /// <summary>
    /// 未知
    /// </summary>
    Unknown = 2
}
