using System.Diagnostics;
using System.IO.Ports;
using Microsoft.Extensions.Logging;
using xdwModeBusClient.Configuration;
using xdwModeBusClient.Interfaces;
using xdwModeBusClient.Utils;

namespace xdwModeBusClient.Services.Modbus;

/// <summary>
/// Modbus RTU客户端实现 (只读模式)
/// </summary>
public class ModbusRtuClient : IModbusClient
{
    private readonly ConnectionConfig _config;
    private readonly ILogger<ModbusRtuClient> _logger;
    private readonly int _timeout;
    private SerialPort? _serialPort;
    private readonly SemaphoreSlim _semaphore = new(1, 1);

    public string ConnectionId => _config.ConnectionId;
    public bool IsConnected => _serialPort?.IsOpen ?? false;

    public ModbusRtuClient(ConnectionConfig config, ILogger<ModbusRtuClient> logger, int timeout = 3000)
    {
        _config = config ?? throw new ArgumentNullException(nameof(config));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _timeout = timeout;

        if (config.RtuConfig == null)
            throw new ArgumentException("RTU配置不能为空");
    }

    public async Task<bool> ConnectAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            await _semaphore.WaitAsync(cancellationToken);

            if (IsConnected)
            {
                _logger.LogDebug("已经连接到 {PortName}", _config.RtuConfig!.PortName);
                return true;
            }

            var rtuConfig = _config.RtuConfig!;

            _serialPort = new SerialPort
            {
                PortName = rtuConfig.PortName,
                BaudRate = rtuConfig.BaudRate,
                DataBits = rtuConfig.DataBits,
                StopBits = ConvertStopBits(rtuConfig.StopBits),
                Parity = ConvertParity(rtuConfig.Parity),
                ReadTimeout = rtuConfig.ReadTimeoutMs,
                WriteTimeout = rtuConfig.WriteTimeoutMs,
                Handshake = Handshake.None
            };

            _serialPort.Open();
            _serialPort.DiscardInBuffer();
            _serialPort.DiscardOutBuffer();

            _logger.LogInformation("成功连接到 {PortName}, 波特率: {BaudRate}, 数据位: {DataBits}, 停止位: {StopBits}, 校验: {Parity}",
                rtuConfig.PortName, rtuConfig.BaudRate, rtuConfig.DataBits, rtuConfig.StopBits, rtuConfig.Parity);

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "连接失败: {PortName}", _config.RtuConfig?.PortName);
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
            if (_serialPort?.IsOpen == true)
            {
                _serialPort.Close();
            }
            _serialPort?.Dispose();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "断开连接时发生错误");
        }
        finally
        {
            _serialPort = null;
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
        var expectedLength = 5 + (count + 7) / 8;
        var response = await SendAndReceiveAsync(request, expectedLength, cancellationToken);

        if (!response.Success || response.Data == null)
        {
            return ModbusResponse<bool[]>.CreateFailure(response.ErrorMessage ?? "读取失败", response.ExceptionCode);
        }

        var data = response.Data;

        if (!ModbusUtils.VerifyCrc16(data))
        {
            return ModbusResponse<bool[]>.CreateFailure("CRC校验失败");
        }

        if ((data[1] & 0x80) != 0)
        {
            var exceptionCode = data[2];
            return ModbusResponse<bool[]>.CreateFailure(ModbusUtils.GetExceptionDescription(exceptionCode), exceptionCode);
        }

        var byteCount = data[2];
        var values = new bool[count];
        for (int i = 0; i < count; i++)
        {
            var byteIndex = i / 8;
            var bitIndex = i % 8;
            if (3 + byteIndex < data.Length - 2)
            {
                values[i] = ((data[3 + byteIndex] >> bitIndex) & 1) == 1;
            }
        }

        return ModbusResponse<bool[]>.CreateSuccess(values, response.ResponseTimeMs, response.RawRequest, response.RawResponse);
    }

    private async Task<ModbusResponse<ushort[]>> ExecuteReadRegistersAsync(byte slaveId, byte functionCode, ushort startAddress, ushort count, CancellationToken cancellationToken)
    {
        var request = BuildReadRequest(slaveId, functionCode, startAddress, count);
        var expectedLength = 5 + count * 2;
        var response = await SendAndReceiveAsync(request, expectedLength, cancellationToken);

        if (!response.Success || response.Data == null)
        {
            return ModbusResponse<ushort[]>.CreateFailure(response.ErrorMessage ?? "读取失败", response.ExceptionCode);
        }

        var data = response.Data;

        if (!ModbusUtils.VerifyCrc16(data))
        {
            return ModbusResponse<ushort[]>.CreateFailure("CRC校验失败");
        }

        if ((data[1] & 0x80) != 0)
        {
            var exceptionCode = data[2];
            return ModbusResponse<ushort[]>.CreateFailure(ModbusUtils.GetExceptionDescription(exceptionCode), exceptionCode);
        }

        var byteCount = data[2];
        var values = new ushort[count];
        for (int i = 0; i < count && (3 + i * 2 + 1) < data.Length - 2; i++)
        {
            values[i] = (ushort)((data[3 + i * 2] << 8) | data[3 + i * 2 + 1]);
        }

        return ModbusResponse<ushort[]>.CreateSuccess(values, response.ResponseTimeMs, response.RawRequest, response.RawResponse);
    }

    private byte[] BuildReadRequest(byte slaveId, byte functionCode, ushort startAddress, ushort count)
    {
        var request = new byte[]
        {
            slaveId,
            functionCode,
            (byte)(startAddress >> 8),
            (byte)(startAddress & 0xFF),
            (byte)(count >> 8),
            (byte)(count & 0xFF)
        };

        return ModbusUtils.AppendCrc16(request);
    }

    private async Task<ModbusResponse<byte[]>> SendAndReceiveAsync(byte[] request, int expectedLength, CancellationToken cancellationToken)
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
                    return ModbusResponse<byte[]>.CreateFailure("无法连接到串口");
                }
            }

            _serialPort!.DiscardInBuffer();
            _serialPort.DiscardOutBuffer();

            _logger.LogDebug("发送请求: {Request}", ModbusUtils.ToHexString(request));

            await _serialPort.BaseStream.WriteAsync(request, cancellationToken);
            await _serialPort.BaseStream.FlushAsync(cancellationToken);

            var frameInterval = _config.RtuConfig?.FrameIntervalMs ?? 50;
            await Task.Delay(frameInterval, cancellationToken);

            var buffer = new byte[Math.Max(expectedLength, 256)];
            var totalBytesRead = 0;
            var readTimeout = _timeout;

            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(readTimeout);

            try
            {
                while (totalBytesRead < 3 && !cts.Token.IsCancellationRequested)
                {
                    if (_serialPort.BytesToRead > 0)
                    {
                        var bytesRead = await _serialPort.BaseStream.ReadAsync(
                            buffer.AsMemory(totalBytesRead, buffer.Length - totalBytesRead),
                            cts.Token);
                        totalBytesRead += bytesRead;
                    }
                    else
                    {
                        await Task.Delay(10, cts.Token);
                    }
                }

                if (totalBytesRead >= 2 && (buffer[1] & 0x80) != 0)
                {
                    expectedLength = 5;
                }

                while (totalBytesRead < expectedLength && !cts.Token.IsCancellationRequested)
                {
                    if (_serialPort.BytesToRead > 0)
                    {
                        var bytesRead = await _serialPort.BaseStream.ReadAsync(
                            buffer.AsMemory(totalBytesRead, buffer.Length - totalBytesRead),
                            cts.Token);
                        totalBytesRead += bytesRead;
                    }
                    else
                    {
                        await Task.Delay(10, cts.Token);
                    }
                }
            }
            catch (OperationCanceledException)
            {
                if (totalBytesRead == 0)
                {
                    return ModbusResponse<byte[]>.CreateFailure("读取响应超时");
                }
            }

            if (totalBytesRead == 0)
            {
                return ModbusResponse<byte[]>.CreateFailure("未收到响应");
            }

            var response = new byte[totalBytesRead];
            Array.Copy(buffer, response, totalBytesRead);

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

    private static StopBits ConvertStopBits(StopBitsConfig config)
    {
        return config switch
        {
            StopBitsConfig.None => StopBits.None,
            StopBitsConfig.One => StopBits.One,
            StopBitsConfig.Two => StopBits.Two,
            StopBitsConfig.OnePointFive => StopBits.OnePointFive,
            _ => StopBits.One
        };
    }

    private static Parity ConvertParity(ParityConfig config)
    {
        return config switch
        {
            ParityConfig.None => Parity.None,
            ParityConfig.Odd => Parity.Odd,
            ParityConfig.Even => Parity.Even,
            ParityConfig.Mark => Parity.Mark,
            ParityConfig.Space => Parity.Space,
            _ => Parity.None
        };
    }

    #endregion

    public void Dispose()
    {
        _serialPort?.Dispose();
        _semaphore.Dispose();
        GC.SuppressFinalize(this);
    }
}
