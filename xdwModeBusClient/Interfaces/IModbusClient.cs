using xdwModeBusClient.Configuration;
using xdwModeBusClient.Models;

namespace xdwModeBusClient.Interfaces;

/// <summary>
/// Modbus客户端接口 (只读模式)
/// </summary>
public interface IModbusClient : IDisposable
{
    /// <summary>
    /// 连接ID
    /// </summary>
    string ConnectionId { get; }

    /// <summary>
    /// 是否已连接
    /// </summary>
    bool IsConnected { get; }

    /// <summary>
    /// 连接到Modbus设备
    /// </summary>
    Task<bool> ConnectAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// 断开连接
    /// </summary>
    Task DisconnectAsync();

    /// <summary>
    /// 读取线圈状态 (FC01)
    /// </summary>
    /// <param name="slaveId">从站地址</param>
    /// <param name="startAddress">起始地址</param>
    /// <param name="count">数量</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>线圈状态数组</returns>
    Task<ModbusResponse<bool[]>> ReadCoilsAsync(byte slaveId, ushort startAddress, ushort count, CancellationToken cancellationToken = default);

    /// <summary>
    /// 读取离散输入 (FC02)
    /// </summary>
    /// <param name="slaveId">从站地址</param>
    /// <param name="startAddress">起始地址</param>
    /// <param name="count">数量</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>离散输入状态数组</returns>
    Task<ModbusResponse<bool[]>> ReadDiscreteInputsAsync(byte slaveId, ushort startAddress, ushort count, CancellationToken cancellationToken = default);

    /// <summary>
    /// 读取保持寄存器 (FC03)
    /// </summary>
    /// <param name="slaveId">从站地址</param>
    /// <param name="startAddress">起始地址</param>
    /// <param name="count">数量</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>寄存器值数组</returns>
    Task<ModbusResponse<ushort[]>> ReadHoldingRegistersAsync(byte slaveId, ushort startAddress, ushort count, CancellationToken cancellationToken = default);

    /// <summary>
    /// 读取输入寄存器 (FC04)
    /// </summary>
    /// <param name="slaveId">从站地址</param>
    /// <param name="startAddress">起始地址</param>
    /// <param name="count">数量</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>寄存器值数组</returns>
    Task<ModbusResponse<ushort[]>> ReadInputRegistersAsync(byte slaveId, ushort startAddress, ushort count, CancellationToken cancellationToken = default);
}

/// <summary>
/// Modbus响应
/// </summary>
/// <typeparam name="T">数据类型</typeparam>
public class ModbusResponse<T>
{
    /// <summary>
    /// 是否成功
    /// </summary>
    public bool Success { get; set; }

    /// <summary>
    /// 数据
    /// </summary>
    public T? Data { get; set; }

    /// <summary>
    /// 错误消息
    /// </summary>
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// 异常代码（Modbus异常）
    /// </summary>
    public byte? ExceptionCode { get; set; }

    /// <summary>
    /// 请求时间戳
    /// </summary>
    public DateTime Timestamp { get; set; } = DateTime.Now;

    /// <summary>
    /// 响应时间（毫秒）
    /// </summary>
    public long ResponseTimeMs { get; set; }

    /// <summary>
    /// 原始请求数据
    /// </summary>
    public byte[]? RawRequest { get; set; }

    /// <summary>
    /// 原始响应数据
    /// </summary>
    public byte[]? RawResponse { get; set; }

    /// <summary>
    /// 创建成功响应
    /// </summary>
    public static ModbusResponse<T> CreateSuccess(T data, long responseTimeMs = 0, byte[]? rawRequest = null, byte[]? rawResponse = null)
    {
        return new ModbusResponse<T>
        {
            Success = true,
            Data = data,
            ResponseTimeMs = responseTimeMs,
            RawRequest = rawRequest,
            RawResponse = rawResponse
        };
    }

    /// <summary>
    /// 创建失败响应
    /// </summary>
    public static ModbusResponse<T> CreateFailure(string errorMessage, byte? exceptionCode = null)
    {
        return new ModbusResponse<T>
        {
            Success = false,
            ErrorMessage = errorMessage,
            ExceptionCode = exceptionCode
        };
    }
}
