using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Serilog;
using Serilog.Extensions.Logging;
using xdwModeBusClient.Configuration;
using xdwModeBusClient.Interfaces;
using xdwModeBusClient.Models;
using xdwModeBusClient.Services.DataProcessing;
using xdwModeBusClient.Services.Modbus;
using xdwModeBusClient.Services.Polling;
using xdwModeBusClient.Utils;

namespace xdwModeBusClient;

class Program
{
    static async Task Main(string[] args)
    {
        Console.OutputEncoding = System.Text.Encoding.UTF8;
        Console.WriteLine("╔════════════════════════════════════════════════════════════╗");
        Console.WriteLine("║           xdwModeBusClient - Modbus客户端工具              ║");
        Console.WriteLine("║                  版本: 1.0.0                               ║");
        Console.WriteLine("╚════════════════════════════════════════════════════════════╝");
        Console.WriteLine();

        // 检查是否为测试模式
        if (args.Length > 0 && args[0].Equals("--test", StringComparison.OrdinalIgnoreCase))
        {
            await TestRunner.RunTestsAsync();
            return;
        }

        PollingManager? pollingManager = null;
        var cts = new CancellationTokenSource();

        try
        {
            // 配置Serilog
            Log.Logger = new LoggerConfiguration()
                .MinimumLevel.Debug()
                .WriteTo.Console(outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}{Exception}")
                .WriteTo.File("logs/modbus_.log",
                    rollingInterval: RollingInterval.Day,
                    outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff} [{Level:u3}] {Message:lj}{NewLine}{Exception}")
                .CreateLogger();

            // 创建日志工厂
            var loggerFactory = new SerilogLoggerFactory(Log.Logger);

            // 加载配置
            var configuration = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
                .Build();

            var modbusConfig = configuration.GetSection("ModbusConfiguration").Get<ModbusConfiguration>()
                ?? throw new InvalidOperationException("无法加载Modbus配置");

            // 创建数据转换器
            var dataConverter = new DataConverter();

            // 创建数据存储
            var dataStorage = new JsonFileDataStorage(
                loggerFactory.CreateLogger<JsonFileDataStorage>(),
                modbusConfig.AppSettings.DataStoragePath);

            // 创建数据显示
            var dataDisplay = new ConsoleDataDisplay(loggerFactory.CreateLogger<ConsoleDataDisplay>());

            // 创建数据处理器
            var dataProcessor = new DataProcessor(
                loggerFactory.CreateLogger<DataProcessor>(),
                dataConverter,
                dataStorage,
                dataDisplay);

            // 创建Modbus客户端
            var clients = new List<IModbusClient>();
            foreach (var connConfig in modbusConfig.Connections.Where(c => c.Enabled))
            {
                IModbusClient client = connConfig.ConnectionType switch
                {
                    ConnectionType.TCP => new ModbusTcpClient(
                        connConfig,
                        loggerFactory.CreateLogger<ModbusTcpClient>(),
                        modbusConfig.AppSettings.RequestTimeoutMs),
                    ConnectionType.RTU => new ModbusRtuClient(
                        connConfig,
                        loggerFactory.CreateLogger<ModbusRtuClient>(),
                        modbusConfig.AppSettings.RequestTimeoutMs),
                    _ => throw new NotSupportedException($"不支持的连接类型: {connConfig.ConnectionType}")
                };
                clients.Add(client);
            }

            // 创建轮询管理器
            pollingManager = new PollingManager(
                loggerFactory.CreateLogger<PollingManager>(),
                modbusConfig,
                dataProcessor,
                dataConverter,
                clients);

            // 订阅连接状态事件
            pollingManager.ConnectionStatusChanged += (sender, e) =>
            {
                dataDisplay.DisplayConnectionStatus(e.ConnectionId, e.IsConnected);
            };

            // 显示配置信息
            PrintConfiguration(modbusConfig);

            // 设置Ctrl+C处理
            Console.CancelKeyPress += (sender, e) =>
            {
                e.Cancel = true;
                Console.WriteLine("\n正在停止服务...");
                cts.Cancel();
            };

            Log.Information("Modbus服务启动");

            // 启动轮询
            await pollingManager.StartAsync();

            Log.Information("按Ctrl+C停止服务");

            // 等待取消信号
            try
            {
                await Task.Delay(Timeout.Infinite, cts.Token);
            }
            catch (OperationCanceledException)
            {
                // 正常取消
            }

            // 停止轮询
            Log.Information("Modbus服务停止");
            await pollingManager.StopAsync();
        }
        catch (Exception ex)
        {
            Log.Fatal(ex, "程序启动失败");
            Console.WriteLine($"程序启动失败: {ex.Message}");
        }
        finally
        {
            pollingManager?.Dispose();
            Log.CloseAndFlush();
        }
    }

    private static void PrintConfiguration(ModbusConfiguration config)
    {
        Console.WriteLine("\n配置信息:");
        Console.WriteLine($"  轮询间隔: {config.AppSettings.PollingIntervalMs}ms");
        Console.WriteLine($"  请求超时: {config.AppSettings.RequestTimeoutMs}ms");
        Console.WriteLine($"  重试次数: {config.AppSettings.RetryCount}");

        Console.WriteLine($"\n连接配置 ({config.Connections.Count(c => c.Enabled)} 个启用):");
        foreach (var conn in config.Connections.Where(c => c.Enabled))
        {
            if (conn.ConnectionType == ConnectionType.TCP && conn.TcpConfig != null)
            {
                Console.WriteLine($"  [{conn.ConnectionId}] TCP - {conn.TcpConfig.IpAddress}:{conn.TcpConfig.Port}");
            }
            else if (conn.ConnectionType == ConnectionType.RTU && conn.RtuConfig != null)
            {
                Console.WriteLine($"  [{conn.ConnectionId}] RTU - {conn.RtuConfig.PortName} @ {conn.RtuConfig.BaudRate}bps");
            }
        }

        Console.WriteLine($"\n数据包配置 ({config.Packets.Count(p => p.Enabled)} 个启用):");
        foreach (var packet in config.Packets.Where(p => p.Enabled))
        {
            Console.WriteLine($"  [{packet.PacketId}] {packet.Name}");
            Console.WriteLine($"    从站: {packet.SlaveId}, 功能码: {packet.FunctionCode}, 地址: {packet.StartAddress}, 数量: {packet.RegisterCount}");
            Console.WriteLine($"    数据点: {packet.DataPoints.Count} 个");
        }

        Console.WriteLine();
    }
}
