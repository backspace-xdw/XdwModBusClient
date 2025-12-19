using System.Diagnostics;
using System.Net.Sockets;
using Microsoft.Extensions.Logging;
using xdwModeBusClient.Configuration;
using xdwModeBusClient.Interfaces;
using xdwModeBusClient.Utils;

namespace xdwModeBusClient.Services.Modbus;

/// <summary>
/// Modbus TCP客户端实现 (只读模式)
/// </summary>
public class ModbusTcpClient : IModbusClient
{
    private readonly ConnectionConfig _config;
    private readonly ILogger<ModbusTcpClient> _logger;
    private readonly int _timeout;
    private TcpClient? _tcpClient;
    private NetworkStream? _stream;
    private ushort _transactionId;
    private readonly SemaphoreSlim _semaphore = new(1, 1);

    public string ConnectionId => _config.ConnectionId;
    public bool IsConnected => _tcpClient?.Connected ?? false;

    public ModbusTcpClient(ConnectionConfig config, ILogger<ModbusTcpClient> logger, int timeout = 3000)
    {
        _config = config ?? throw new ArgumentNullException(nameof(config));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _timeout = timeout;

        if (config.TcpConfig == null)
            throw new ArgumentException("TCP配置不能为空");
    }

    public async Task<bool> ConnectAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            await _semaphore.WaitAsync(cancellationToken);

            if (IsConnected)
            {
                _logger.LogDebug("已经连接到 {Ip}:{Port}", _config.TcpConfig!.IpAddress, _config.TcpConfig.Port);
                return true;
            }

            _tcpClient = new TcpClient();
            _tcpClient.ReceiveTimeout = _timeout;
            _tcpClient.SendTimeout = _timeout;

            var connectTask = _tcpClient.ConnectAsync(_config.TcpConfig!.IpAddress, _config.TcpConfig.Port);
            var timeoutTask = Task.Delay(_config.TcpConfig.ConnectTimeoutMs, cancellationToken);

            if (await Task.WhenAny(connectTask, timeoutTask) == timeoutTask)
            {
                throw new TimeoutException($"连接超时: {_config.TcpConfig.IpAddress}:{_config.TcpConfig.Port}");
            }

            await connectTask;

            _stream = _tcpClient.GetStream();
            _transactionId = 0;

            _logger.LogInformation("成功连接到 {Ip}:{Port}", _config.TcpConfig.IpAddress, _config.TcpConfig.Port);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "连接失败: {Ip}:{Port}", _config.TcpConfig?.IpAddress, _config.TcpConfig?.Port);
            await DisconnectAsync();
            return false;
        }
        finally
        {
            _semaphore.Release();
        }
    }

    public async Task DisconnectAsync()
    {
        try
        {
            _stream?.Close();
            _tcpClient?.Close();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "断开连接时发生错误");
        }
        finally
        {
            _stream = null;
            _tcpClient = null;
        }

        await Task.CompletedTask;
        _logger.LogInformation("已断开连接: {ConnectionId}", ConnectionId);
    }

    public async Task<ModbusResponse<bool[]>> ReadCoilsAsync(byte slaveId, ushort startAddress, ushort count, CancellationToken cancellationToken = default)
    {
        return await ExecuteReadBooleanAsync(slaveId, 0x01, startAddress, count, cancellationToken);
    }

    public async Task<ModbusResponse<bool[]>> ReadDiscreteInputsAsync(byte slaveId, ushort startAddress, ushort count, CancellationToken cancellationToken = default)
    {
        return await ExecuteReadBooleanAsync(slaveId, 0x02, startAddress, count, cancellationToken);
    }

    public async Task<ModbusResponse<ushort[]>> ReadHoldingRegistersAsync(byte slaveId, ushort startAddress, ushort count, CancellationToken cancellationToken = default)
    {
        return await ExecuteReadRegistersAsync(slaveId, 0x03, startAddress, count, cancellationToken);
    }

    public async Task<ModbusResponse<ushort[]>> ReadInputRegistersAsync(byte slaveId, ushort startAddress, ushort count, CancellationToken cancellationToken = default)
    {
        return await ExecuteReadRegistersAsync(slaveId, 0x04, startAddress, count, cancellationToken);
    }

    #region 私有方法

    private async Task<ModbusResponse<bool[]>> ExecuteReadBooleanAsync(byte slaveId, byte functionCode, ushort startAddress, ushort count, CancellationToken cancellationToken)
    {
        var request = BuildReadRequest(slaveId, functionCode, startAddress, count);
        var response = await SendAndReceiveAsync(request, cancellationToken);

        if (!response.Success || response.Data == null)
        {
            return ModbusResponse<bool[]>.CreateFailure(response.ErrorMessage ?? "读取失败", response.ExceptionCode);
        }

        var data = response.Data;
        if (data.Length < 9)
        {
            return ModbusResponse<bool[]>.CreateFailure("响应数据长度不足");
        }

        if ((data[7] & 0x80) != 0)
        {
            var exceptionCode = data[8];
            return ModbusResponse<bool[]>.CreateFailure(ModbusUtils.GetExceptionDescription(exceptionCode), exceptionCode);
        }

        var byteCount = data[8];
        var values = new bool[count];
        for (int i = 0; i < count; i++)
        {
            var byteIndex = i / 8;
            var bitIndex = i % 8;
            if (9 + byteIndex < data.Length)
            {
                values[i] = ((data[9 + byteIndex] >> bitIndex) & 1) == 1;
            }
        }

        return ModbusResponse<bool[]>.CreateSuccess(values, response.ResponseTimeMs, response.RawRequest, response.RawResponse);
    }

    private async Task<ModbusResponse<ushort[]>> ExecuteReadRegistersAsync(byte slaveId, byte functionCode, ushort startAddress, ushort count, CancellationToken cancellationToken)
    {
        var request = BuildReadRequest(slaveId, functionCode, startAddress, count);
        var response = await SendAndReceiveAsync(request, cancellationToken);

        if (!response.Success || response.Data == null)
        {
            return ModbusResponse<ushort[]>.CreateFailure(response.ErrorMessage ?? "读取失败", response.ExceptionCode);
        }

        var data = response.Data;
        if (data.Length < 9)
        {
            return ModbusResponse<ushort[]>.CreateFailure("响应数据长度不足");
        }

        if ((data[7] & 0x80) != 0)
        {
            var exceptionCode = data[8];
            return ModbusResponse<ushort[]>.CreateFailure(ModbusUtils.GetExceptionDescription(exceptionCode), exceptionCode);
        }

        var byteCount = data[8];
        var expectedByteCount = count * 2;
        if (byteCount != expectedByteCount)
        {
            _logger.LogWarning("响应字节数不匹配: 期望 {Expected}, 实际 {Actual}", expectedByteCount, byteCount);
        }

        var values = new ushort[count];
        for (int i = 0; i < count && (9 + i * 2 + 1) < data.Length; i++)
        {
            values[i] = (ushort)((data[9 + i * 2] << 8) | data[9 + i * 2 + 1]);
        }

        return ModbusResponse<ushort[]>.CreateSuccess(values, response.ResponseTimeMs, response.RawRequest, response.RawResponse);
    }

    private byte[] BuildReadRequest(byte slaveId, byte functionCode, ushort startAddress, ushort count)
    {
        var transactionId = GetNextTransactionId();
        return
        [
            (byte)(transactionId >> 8),
            (byte)(transactionId & 0xFF),
            0x00, 0x00,
            0x00, 0x06,
            slaveId,
            functionCode,
            (byte)(startAddress >> 8),
            (byte)(startAddress & 0xFF),
            (byte)(count >> 8),
            (byte)(count & 0xFF)
        ];
    }

    private async Task<ModbusResponse<byte[]>> SendAndReceiveAsync(byte[] request, CancellationToken cancellationToken)
    {
        await _semaphore.WaitAsync(cancellationToken);
        var stopwatch = Stopwatch.StartNew();

        try
        {
            if (!IsConnected)
            {
                var connected = await ConnectAsync(cancellationToken);
                if (!connected)
                {
                    return ModbusResponse<byte[]>.CreateFailure("无法连接到服务器");
                }
            }

            _logger.LogDebug("发送请求: {Request}", ModbusUtils.ToHexString(request));

            await _stream!.WriteAsync(request, cancellationToken);
            await _stream.FlushAsync(cancellationToken);

            var buffer = new byte[1024];
            var bytesRead = 0;

            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(_timeout);

            try
            {
                bytesRead = await _stream.ReadAsync(buffer, cts.Token);
            }
            catch (OperationCanceledException)
            {
                return ModbusResponse<byte[]>.CreateFailure("读取响应超时");
            }

            if (bytesRead == 0)
            {
                return ModbusResponse<byte[]>.CreateFailure("未收到响应");
            }

            var response = new byte[bytesRead];
            Array.Copy(buffer, response, bytesRead);

            stopwatch.Stop();
            _logger.LogDebug("收到响应: {Response}, 耗时: {Time}ms", ModbusUtils.ToHexString(response), stopwatch.ElapsedMilliseconds);

            return ModbusResponse<byte[]>.CreateSuccess(response, stopwatch.ElapsedMilliseconds, request, response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "通信错误");
            return ModbusResponse<byte[]>.CreateFailure($"通信错误: {ex.Message}");
        }
        finally
        {
            _semaphore.Release();
        }
    }

    private ushort GetNextTransactionId()
    {
        return ++_transactionId;
    }

    #endregion

    public void Dispose()
    {
        _stream?.Dispose();
        _tcpClient?.Dispose();
        _semaphore.Dispose();
        GC.SuppressFinalize(this);
    }
}
