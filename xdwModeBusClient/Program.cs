using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Serilog;
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

            // 构建主机
            var host = Host.CreateDefaultBuilder(args)
                .UseSerilog()
                .ConfigureAppConfiguration((context, config) =>
                {
                    config.SetBasePath(Directory.GetCurrentDirectory());
                    config.AddJsonFile("appsettings.json", optional: false, reloadOnChange: true);
                })
                .ConfigureServices((context, services) =>
                {
                    // 加载配置
                    var modbusConfig = context.Configuration.GetSection("ModbusConfiguration").Get<ModbusConfiguration>()
                        ?? throw new InvalidOperationException("无法加载Modbus配置");

                    services.AddSingleton(modbusConfig);

                    // 注册数据转换器
                    services.AddSingleton<IDataConverter, DataConverter>();

                    // 注册数据存储
                    services.AddSingleton<IDataStorage>(sp =>
                    {
                        var logger = sp.GetRequiredService<ILogger<JsonFileDataStorage>>();
                        return new JsonFileDataStorage(logger, modbusConfig.AppSettings.DataStoragePath);
                    });

                    // 注册数据显示
                    services.AddSingleton<IDataDisplay, ConsoleDataDisplay>();

                    // 注册数据处理器
                    services.AddSingleton<IDataProcessor, DataProcessor>();

                    // 注册Modbus客户端
                    services.AddSingleton<IEnumerable<IModbusClient>>(sp =>
                    {
                        var clients = new List<IModbusClient>();
                        var loggerFactory = sp.GetRequiredService<ILoggerFactory>();

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

                        return clients;
                    });

                    // 注册轮询管理器
                    services.AddSingleton<PollingManager>();

                    // 注册托管服务
                    services.AddHostedService<ModbusHostedService>();
                })
                .Build();

            await host.RunAsync();
        }
        catch (Exception ex)
        {
            Log.Fatal(ex, "程序启动失败");
            Console.WriteLine($"程序启动失败: {ex.Message}");
        }
        finally
        {
            Log.CloseAndFlush();
        }
    }
}

/// <summary>
/// Modbus托管服务
/// </summary>
public class ModbusHostedService : IHostedService
{
    private readonly ILogger<ModbusHostedService> _logger;
    private readonly PollingManager _pollingManager;
    private readonly IDataDisplay _dataDisplay;
    private readonly ModbusConfiguration _config;

    public ModbusHostedService(
        ILogger<ModbusHostedService> logger,
        PollingManager pollingManager,
        IDataDisplay dataDisplay,
        ModbusConfiguration config)
    {
        _logger = logger;
        _pollingManager = pollingManager;
        _dataDisplay = dataDisplay;
        _config = config;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Modbus服务启动");

        // 订阅事件
        _pollingManager.ConnectionStatusChanged += (sender, e) =>
        {
            _dataDisplay.DisplayConnectionStatus(e.ConnectionId, e.IsConnected);
        };

        // 显示配置信息
        PrintConfiguration();

        // 启动轮询
        await _pollingManager.StartAsync();

        _logger.LogInformation("按Ctrl+C停止服务");
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Modbus服务停止");
        await _pollingManager.StopAsync();
    }

    private void PrintConfiguration()
    {
        Console.WriteLine("\n配置信息:");
        Console.WriteLine($"  轮询间隔: {_config.AppSettings.PollingIntervalMs}ms");
        Console.WriteLine($"  请求超时: {_config.AppSettings.RequestTimeoutMs}ms");
        Console.WriteLine($"  重试次数: {_config.AppSettings.RetryCount}");

        Console.WriteLine($"\n连接配置 ({_config.Connections.Count(c => c.Enabled)} 个启用):");
        foreach (var conn in _config.Connections.Where(c => c.Enabled))
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

        Console.WriteLine($"\n数据包配置 ({_config.Packets.Count(p => p.Enabled)} 个启用):");
        foreach (var packet in _config.Packets.Where(p => p.Enabled))
        {
            Console.WriteLine($"  [{packet.PacketId}] {packet.Name}");
            Console.WriteLine($"    从站: {packet.SlaveId}, 功能码: {packet.FunctionCode}, 地址: {packet.StartAddress}, 数量: {packet.RegisterCount}");
            Console.WriteLine($"    数据点: {packet.DataPoints.Count} 个");
        }

        Console.WriteLine();
    }
}
