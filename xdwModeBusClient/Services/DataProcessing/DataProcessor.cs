using Microsoft.Extensions.Logging;
using xdwModeBusClient.Configuration;
using xdwModeBusClient.Interfaces;
using xdwModeBusClient.Models;
using xdwModeBusClient.Utils;

namespace xdwModeBusClient.Services.DataProcessing;

/// <summary>
/// 数据处理器实现
/// </summary>
public class DataProcessor : IDataProcessor
{
    private readonly ILogger<DataProcessor> _logger;
    private readonly IDataConverter _dataConverter;
    private readonly IDataStorage? _dataStorage;
    private readonly IDataDisplay? _dataDisplay;
    private readonly List<Action<PollingResult>> _dataHandlers = [];

    public DataProcessor(
        ILogger<DataProcessor> logger,
        IDataConverter dataConverter,
        IDataStorage? dataStorage = null,
        IDataDisplay? dataDisplay = null)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _dataConverter = dataConverter ?? throw new ArgumentNullException(nameof(dataConverter));
        _dataStorage = dataStorage;
        _dataDisplay = dataDisplay;
    }

    /// <summary>
    /// 注册数据处理回调
    /// </summary>
    /// <param name="handler">处理回调</param>
    public void RegisterDataHandler(Action<PollingResult> handler)
    {
        _dataHandlers.Add(handler);
    }

    /// <summary>
    /// 处理采集到的数据
    /// </summary>
    public async Task ProcessDataAsync(PollingResult result, CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogDebug("处理数据包: {PacketId}, 成功: {Success}", result.PacketId, result.Success);

            // 如果采集成功，解析数据点
            if (result.Success)
            {
                // 数据点值已在轮询管理器中解析
                _logger.LogDebug("数据包 {PacketId} 包含 {Count} 个数据点", result.PacketId, result.DataPointValues.Count);
            }

            // 存储数据
            if (_dataStorage != null)
            {
                await _dataStorage.StoreAsync(result, cancellationToken);
            }

            // 显示数据
            if (_dataDisplay != null)
            {
                if (result.Success)
                {
                    _dataDisplay.Display(result);
                }
                else
                {
                    _dataDisplay.DisplayError(result.PacketId, result.ErrorMessage ?? "未知错误");
                }
            }

            // 调用注册的处理回调
            foreach (var handler in _dataHandlers)
            {
                try
                {
                    handler(result);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "执行数据处理回调时发生错误");
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "处理数据时发生错误: {PacketId}", result.PacketId);
        }
    }
}

/// <summary>
/// 默认数据存储实现 - 存储到JSON文件
/// </summary>
public class JsonFileDataStorage : IDataStorage
{
    private readonly ILogger<JsonFileDataStorage> _logger;
    private readonly string _storagePath;
    private readonly SemaphoreSlim _semaphore = new(1, 1);

    public JsonFileDataStorage(ILogger<JsonFileDataStorage> logger, string storagePath)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _storagePath = storagePath;

        if (!Directory.Exists(_storagePath))
        {
            Directory.CreateDirectory(_storagePath);
        }
    }

    public async Task StoreAsync(PollingResult result, CancellationToken cancellationToken = default)
    {
        await _semaphore.WaitAsync(cancellationToken);
        try
        {
            var fileName = $"{result.PacketId}_{result.Timestamp:yyyyMMdd}.json";
            var filePath = Path.Combine(_storagePath, fileName);

            var json = System.Text.Json.JsonSerializer.Serialize(result, new System.Text.Json.JsonSerializerOptions
            {
                WriteIndented = true
            });

            await File.AppendAllTextAsync(filePath, json + Environment.NewLine, cancellationToken);
            _logger.LogDebug("数据已存储到: {FilePath}", filePath);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "存储数据失败: {PacketId}", result.PacketId);
        }
        finally
        {
            _semaphore.Release();
        }
    }

    public async Task<IEnumerable<PollingResult>> QueryAsync(string packetId, DateTime startTime, DateTime endTime, CancellationToken cancellationToken = default)
    {
        var results = new List<PollingResult>();

        try
        {
            var files = Directory.GetFiles(_storagePath, $"{packetId}_*.json");
            foreach (var file in files)
            {
                var lines = await File.ReadAllLinesAsync(file, cancellationToken);
                foreach (var line in lines)
                {
                    if (string.IsNullOrWhiteSpace(line)) continue;

                    try
                    {
                        var result = System.Text.Json.JsonSerializer.Deserialize<PollingResult>(line);
                        if (result != null && result.Timestamp >= startTime && result.Timestamp <= endTime)
                        {
                            results.Add(result);
                        }
                    }
                    catch
                    {
                        // 忽略解析错误的行
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "查询数据失败: {PacketId}", packetId);
        }

        return results.OrderBy(r => r.Timestamp);
    }

    public void Dispose()
    {
        _semaphore.Dispose();
        GC.SuppressFinalize(this);
    }
}

/// <summary>
/// 控制台数据显示实现
/// </summary>
public class ConsoleDataDisplay : IDataDisplay
{
    private readonly ILogger<ConsoleDataDisplay> _logger;
    private readonly object _consoleLock = new();

    public ConsoleDataDisplay(ILogger<ConsoleDataDisplay> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public void Display(PollingResult result)
    {
        lock (_consoleLock)
        {
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine($"\n[{result.Timestamp:HH:mm:ss.fff}] 数据包: {result.PacketName} ({result.PacketId})");
            Console.ResetColor();

            Console.WriteLine($"  从站: {result.SlaveId}, 功能码: {result.FunctionCode}, 起始地址: {result.StartAddress}");
            Console.WriteLine($"  响应时间: {result.ResponseTimeMs}ms");

            if (result.RawRegisters != null && result.RawRegisters.Length > 0)
            {
                Console.WriteLine($"  原始寄存器: [{string.Join(", ", result.RawRegisters.Take(20).Select(r => $"0x{r:X4}"))}]" +
                    (result.RawRegisters.Length > 20 ? "..." : ""));
            }

            if (result.RawCoils != null && result.RawCoils.Length > 0)
            {
                Console.WriteLine($"  原始线圈: [{string.Join(", ", result.RawCoils.Take(32).Select(c => c ? "1" : "0"))}]" +
                    (result.RawCoils.Length > 32 ? "..." : ""));
            }

            if (result.DataPointValues.Count > 0)
            {
                Console.WriteLine("  数据点值:");
                foreach (var point in result.DataPointValues)
                {
                    var quality = point.Quality == DataQuality.Good ? "√" : "×";
                    Console.WriteLine($"    [{quality}] {point.Name}: {point.ScaledValue} {point.Unit}");
                }
            }
        }
    }

    public void DisplayError(string packetId, string errorMessage)
    {
        lock (_consoleLock)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"\n[{DateTime.Now:HH:mm:ss.fff}] 错误 - 数据包: {packetId}");
            Console.WriteLine($"  {errorMessage}");
            Console.ResetColor();
        }
    }

    public void DisplayConnectionStatus(string connectionId, bool isConnected)
    {
        lock (_consoleLock)
        {
            Console.ForegroundColor = isConnected ? ConsoleColor.Green : ConsoleColor.Yellow;
            Console.WriteLine($"\n[{DateTime.Now:HH:mm:ss.fff}] 连接状态 - {connectionId}: {(isConnected ? "已连接" : "已断开")}");
            Console.ResetColor();
        }
    }
}

/// <summary>
/// 预留的数据处理扩展点
/// 用户可以实现此接口来自定义数据处理逻辑
/// </summary>
public interface ICustomDataHandler
{
    /// <summary>
    /// 处理原始数据
    /// </summary>
    /// <param name="result">轮询结果</param>
    Task HandleDataAsync(PollingResult result);

    /// <summary>
    /// 数据处理器优先级（数值越小优先级越高）
    /// </summary>
    int Priority { get; }
}

/// <summary>
/// 示例自定义数据处理器 - 阈值告警
/// </summary>
public class ThresholdAlarmHandler : ICustomDataHandler
{
    private readonly ILogger<ThresholdAlarmHandler> _logger;
    private readonly Dictionary<string, (double Min, double Max)> _thresholds = new();

    public int Priority => 100;

    public ThresholdAlarmHandler(ILogger<ThresholdAlarmHandler> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// 设置数据点阈值
    /// </summary>
    public void SetThreshold(string pointId, double min, double max)
    {
        _thresholds[pointId] = (min, max);
    }

    public Task HandleDataAsync(PollingResult result)
    {
        foreach (var point in result.DataPointValues)
        {
            if (_thresholds.TryGetValue(point.PointId, out var threshold))
            {
                if (point.ScaledValue is double value)
                {
                    if (value < threshold.Min)
                    {
                        _logger.LogWarning("低于下限告警: {PointName} = {Value} < {Min}",
                            point.Name, value, threshold.Min);
                    }
                    else if (value > threshold.Max)
                    {
                        _logger.LogWarning("超过上限告警: {PointName} = {Value} > {Max}",
                            point.Name, value, threshold.Max);
                    }
                }
            }
        }

        return Task.CompletedTask;
    }
}
