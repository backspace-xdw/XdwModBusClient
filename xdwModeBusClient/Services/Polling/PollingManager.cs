using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using xdwModeBusClient.Configuration;
using xdwModeBusClient.Interfaces;
using xdwModeBusClient.Models;
using xdwModeBusClient.Utils;

namespace xdwModeBusClient.Services.Polling;

/// <summary>
/// 轮询管理器 - 管理多个数据包的循环采集 (只读模式)
/// </summary>
public class PollingManager : IDisposable
{
    private readonly ILogger<PollingManager> _logger;
    private readonly ModbusConfiguration _config;
    private readonly IDataProcessor _dataProcessor;
    private readonly IDataConverter _dataConverter;
    private readonly ConcurrentDictionary<string, IModbusClient> _clients = new();
    private readonly ConcurrentDictionary<string, PacketPollingState> _packetStates = new();
    private readonly CancellationTokenSource _cts = new();
    private readonly List<Task> _pollingTasks = [];
    private bool _isRunning;

    /// <summary>
    /// 连接状态变更事件
    /// </summary>
    public event EventHandler<ConnectionStatusEventArgs>? ConnectionStatusChanged;

    /// <summary>
    /// 数据采集完成事件
    /// </summary>
    public event EventHandler<PollingResult>? DataPolled;

    public PollingManager(
        ILogger<PollingManager> logger,
        ModbusConfiguration config,
        IDataProcessor dataProcessor,
        IDataConverter dataConverter,
        IEnumerable<IModbusClient> clients)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _config = config ?? throw new ArgumentNullException(nameof(config));
        _dataProcessor = dataProcessor ?? throw new ArgumentNullException(nameof(dataProcessor));
        _dataConverter = dataConverter ?? throw new ArgumentNullException(nameof(dataConverter));

        // 注册客户端
        foreach (var client in clients)
        {
            _clients[client.ConnectionId] = client;
        }

        // 初始化数据包状态
        foreach (var packet in _config.Packets.Where(p => p.Enabled))
        {
            _packetStates[packet.PacketId] = new PacketPollingState
            {
                PacketConfig = packet,
                LastPollingTime = DateTime.MinValue,
                PollingInterval = packet.PollingIntervalMs > 0
                    ? packet.PollingIntervalMs
                    : _config.AppSettings.PollingIntervalMs
            };
        }
    }

    /// <summary>
    /// 启动轮询
    /// </summary>
    public async Task StartAsync()
    {
        if (_isRunning)
        {
            _logger.LogWarning("轮询管理器已经在运行中");
            return;
        }

        _isRunning = true;
        _logger.LogInformation("启动轮询管理器, 共 {Count} 个数据包", _packetStates.Count);

        // 连接所有客户端
        foreach (var client in _clients.Values)
        {
            var connected = await client.ConnectAsync(_cts.Token);
            OnConnectionStatusChanged(client.ConnectionId, connected);
        }

        // 启动轮询任务
        var pollingTask = Task.Run(() => PollingLoopAsync(_cts.Token), _cts.Token);
        _pollingTasks.Add(pollingTask);
    }

    /// <summary>
    /// 停止轮询
    /// </summary>
    public async Task StopAsync()
    {
        if (!_isRunning)
        {
            return;
        }

        _logger.LogInformation("停止轮询管理器");
        _cts.Cancel();

        try
        {
            await Task.WhenAll(_pollingTasks);
        }
        catch (OperationCanceledException)
        {
            // 正常取消
        }

        // 断开所有连接
        foreach (var client in _clients.Values)
        {
            await client.DisconnectAsync();
            OnConnectionStatusChanged(client.ConnectionId, false);
        }

        _isRunning = false;
    }

    /// <summary>
    /// 手动触发单次采集
    /// </summary>
    /// <param name="packetId">数据包ID</param>
    public async Task<PollingResult?> PollOnceAsync(string packetId)
    {
        if (!_packetStates.TryGetValue(packetId, out var state))
        {
            _logger.LogWarning("未找到数据包: {PacketId}", packetId);
            return null;
        }

        return await PollPacketAsync(state.PacketConfig, _cts.Token);
    }

    /// <summary>
    /// 获取所有数据包的最新状态
    /// </summary>
    public IReadOnlyDictionary<string, PacketPollingState> GetPacketStates()
    {
        return _packetStates;
    }

    #region 私有方法

    private async Task PollingLoopAsync(CancellationToken cancellationToken)
    {
        _logger.LogDebug("轮询循环开始");

        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                var now = DateTime.Now;

                // 遍历所有数据包，检查是否需要采集
                foreach (var state in _packetStates.Values)
                {
                    if (cancellationToken.IsCancellationRequested) break;

                    var timeSinceLastPoll = (now - state.LastPollingTime).TotalMilliseconds;
                    if (timeSinceLastPoll >= state.PollingInterval)
                    {
                        var result = await PollPacketAsync(state.PacketConfig, cancellationToken);
                        if (result != null)
                        {
                            state.LastPollingTime = now;
                            state.LastResult = result;
                            state.SuccessCount += result.Success ? 1 : 0;
                            state.FailureCount += result.Success ? 0 : 1;
                        }

                        // 请求间延迟
                        if (_config.AppSettings.RequestDelayMs > 0)
                        {
                            await Task.Delay(_config.AppSettings.RequestDelayMs, cancellationToken);
                        }
                    }
                }

                // 短暂休眠，避免CPU过高占用
                await Task.Delay(10, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "轮询循环异常");
                await Task.Delay(1000, cancellationToken);
            }
        }

        _logger.LogDebug("轮询循环结束");
    }

    private async Task<PollingResult?> PollPacketAsync(PacketConfig packet, CancellationToken cancellationToken)
    {
        if (!_clients.TryGetValue(packet.ConnectionId, out var client))
        {
            _logger.LogWarning("未找到连接: {ConnectionId}", packet.ConnectionId);
            return null;
        }

        var result = new PollingResult
        {
            PacketId = packet.PacketId,
            PacketName = packet.Name,
            ConnectionId = packet.ConnectionId,
            SlaveId = packet.SlaveId,
            FunctionCode = packet.FunctionCode,
            StartAddress = packet.StartAddress
        };

        try
        {
            // 确保连接
            if (!client.IsConnected)
            {
                var connected = await client.ConnectAsync(cancellationToken);
                OnConnectionStatusChanged(client.ConnectionId, connected);
                if (!connected)
                {
                    result.Success = false;
                    result.ErrorMessage = "无法连接到设备";
                    await _dataProcessor.ProcessDataAsync(result, cancellationToken);
                    return result;
                }
            }

            // 执行读取操作
            await ExecuteReadAsync(client, packet, result, cancellationToken);

            // 如果读取成功，解析数据点
            if (result.Success)
            {
                ParseDataPoints(packet, result);
            }

            // 处理数据
            await _dataProcessor.ProcessDataAsync(result, cancellationToken);

            // 触发事件
            OnDataPolled(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "采集数据包失败: {PacketId}", packet.PacketId);
            result.Success = false;
            result.ErrorMessage = ex.Message;
            await _dataProcessor.ProcessDataAsync(result, cancellationToken);
        }

        return result;
    }

    private async Task ExecuteReadAsync(IModbusClient client, PacketConfig packet, PollingResult result, CancellationToken cancellationToken)
    {
        // 重试逻辑
        var retryCount = _config.AppSettings.RetryCount;
        var retryInterval = _config.AppSettings.RetryIntervalMs;

        for (int attempt = 0; attempt <= retryCount; attempt++)
        {
            if (attempt > 0)
            {
                _logger.LogDebug("重试第 {Attempt} 次: {PacketId}", attempt, packet.PacketId);
                await Task.Delay(retryInterval, cancellationToken);
            }

            switch (packet.FunctionCode)
            {
                case ModbusFunctionCode.ReadCoils:
                    var coilsResponse = await client.ReadCoilsAsync(packet.SlaveId, packet.StartAddress, packet.RegisterCount, cancellationToken);
                    if (coilsResponse.Success)
                    {
                        result.Success = true;
                        result.RawCoils = coilsResponse.Data;
                        result.ResponseTimeMs = coilsResponse.ResponseTimeMs;
                        result.RawRequest = coilsResponse.RawRequest;
                        result.RawResponse = coilsResponse.RawResponse;
                        return;
                    }
                    result.ErrorMessage = coilsResponse.ErrorMessage;
                    break;

                case ModbusFunctionCode.ReadDiscreteInputs:
                    var discreteResponse = await client.ReadDiscreteInputsAsync(packet.SlaveId, packet.StartAddress, packet.RegisterCount, cancellationToken);
                    if (discreteResponse.Success)
                    {
                        result.Success = true;
                        result.RawCoils = discreteResponse.Data;
                        result.ResponseTimeMs = discreteResponse.ResponseTimeMs;
                        result.RawRequest = discreteResponse.RawRequest;
                        result.RawResponse = discreteResponse.RawResponse;
                        return;
                    }
                    result.ErrorMessage = discreteResponse.ErrorMessage;
                    break;

                case ModbusFunctionCode.ReadHoldingRegisters:
                    var holdingResponse = await client.ReadHoldingRegistersAsync(packet.SlaveId, packet.StartAddress, packet.RegisterCount, cancellationToken);
                    if (holdingResponse.Success)
                    {
                        result.Success = true;
                        result.RawRegisters = holdingResponse.Data;
                        result.ResponseTimeMs = holdingResponse.ResponseTimeMs;
                        result.RawRequest = holdingResponse.RawRequest;
                        result.RawResponse = holdingResponse.RawResponse;
                        return;
                    }
                    result.ErrorMessage = holdingResponse.ErrorMessage;
                    break;

                case ModbusFunctionCode.ReadInputRegisters:
                    var inputResponse = await client.ReadInputRegistersAsync(packet.SlaveId, packet.StartAddress, packet.RegisterCount, cancellationToken);
                    if (inputResponse.Success)
                    {
                        result.Success = true;
                        result.RawRegisters = inputResponse.Data;
                        result.ResponseTimeMs = inputResponse.ResponseTimeMs;
                        result.RawRequest = inputResponse.RawRequest;
                        result.RawResponse = inputResponse.RawResponse;
                        return;
                    }
                    result.ErrorMessage = inputResponse.ErrorMessage;
                    break;

                default:
                    result.ErrorMessage = $"不支持的功能码: {packet.FunctionCode}";
                    return;
            }
        }

        result.Success = false;
    }

    private void ParseDataPoints(PacketConfig packet, PollingResult result)
    {
        foreach (var pointConfig in packet.DataPoints)
        {
            var pointValue = new DataPointValue
            {
                PointId = pointConfig.PointId,
                Name = pointConfig.Name,
                DataType = pointConfig.DataType,
                Unit = pointConfig.Unit,
                Timestamp = result.Timestamp
            };

            try
            {
                // 根据功能码类型解析数据
                if (result.RawRegisters != null)
                {
                    // 寄存器数据
                    if (pointConfig.BitIndex >= 0)
                    {
                        // 位操作
                        var registerValue = result.RawRegisters[pointConfig.Offset];
                        pointValue.RawValue = DataConverter.GetBit(registerValue, pointConfig.BitIndex);
                        pointValue.ScaledValue = pointValue.RawValue;
                    }
                    else
                    {
                        // 完整数据类型
                        pointValue.RawValue = _dataConverter.ConvertFromRegisters(
                            result.RawRegisters,
                            pointConfig.Offset,
                            pointConfig.DataType,
                            pointConfig.ByteOrder);

                        // 应用缩放和偏移
                        if (pointValue.RawValue is IConvertible convertible)
                        {
                            var rawDouble = convertible.ToDouble(null);
                            var scaled = rawDouble * pointConfig.Scale + pointConfig.OffsetValue;
                            pointValue.ScaledValue = scaled;
                        }
                        else
                        {
                            pointValue.ScaledValue = pointValue.RawValue;
                        }
                    }
                }
                else if (result.RawCoils != null)
                {
                    // 线圈数据
                    if (pointConfig.Offset < result.RawCoils.Length)
                    {
                        pointValue.RawValue = result.RawCoils[pointConfig.Offset];
                        pointValue.ScaledValue = pointValue.RawValue;
                    }
                }

                pointValue.Quality = DataQuality.Good;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "解析数据点失败: {PointId}", pointConfig.PointId);
                pointValue.Quality = DataQuality.Bad;
            }

            result.DataPointValues.Add(pointValue);
        }
    }

    private void OnConnectionStatusChanged(string connectionId, bool isConnected)
    {
        ConnectionStatusChanged?.Invoke(this, new ConnectionStatusEventArgs(connectionId, isConnected));
    }

    private void OnDataPolled(PollingResult result)
    {
        DataPolled?.Invoke(this, result);
    }

    #endregion

    public void Dispose()
    {
        _cts.Cancel();
        _cts.Dispose();

        foreach (var client in _clients.Values)
        {
            client.Dispose();
        }

        GC.SuppressFinalize(this);
    }
}

/// <summary>
/// 数据包轮询状态
/// </summary>
public class PacketPollingState
{
    public required PacketConfig PacketConfig { get; set; }
    public DateTime LastPollingTime { get; set; }
    public int PollingInterval { get; set; }
    public PollingResult? LastResult { get; set; }
    public long SuccessCount { get; set; }
    public long FailureCount { get; set; }
}

/// <summary>
/// 连接状态变更事件参数
/// </summary>
public class ConnectionStatusEventArgs : EventArgs
{
    public string ConnectionId { get; }
    public bool IsConnected { get; }

    public ConnectionStatusEventArgs(string connectionId, bool isConnected)
    {
        ConnectionId = connectionId;
        IsConnected = isConnected;
    }
}
