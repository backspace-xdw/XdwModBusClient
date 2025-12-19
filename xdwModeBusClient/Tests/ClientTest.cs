using Microsoft.Extensions.Logging;
using Serilog;
using Serilog.Extensions.Logging;
using xdwModeBusClient.Configuration;
using xdwModeBusClient.Interfaces;
using xdwModeBusClient.Models;
using xdwModeBusClient.Services.Modbus;
using xdwModeBusClient.Utils;

namespace xdwModeBusClient.Tests;

/// <summary>
/// Modbus客户端测试类
/// </summary>
public class ClientTest
{
    private readonly ILoggerFactory _loggerFactory;

    public ClientTest()
    {
        var serilogLogger = new LoggerConfiguration()
            .MinimumLevel.Debug()
            .WriteTo.Console()
            .CreateLogger();
        _loggerFactory = new SerilogLoggerFactory(serilogLogger);
    }

    /// <summary>
    /// 运行所有测试
    /// </summary>
    public async Task RunAllTestsAsync()
    {
        Console.WriteLine("\n" + new string('=', 60));
        Console.WriteLine("          xdwModeBusClient 功能测试");
        Console.WriteLine(new string('=', 60));

        // 启动模拟器
        using var simulator = new ModbusTcpSimulator(5020);
        simulator.Start();
        await Task.Delay(500); // 等待模拟器启动

        try
        {
            // 测试TCP客户端
            await TestTcpClientAsync();

            // 测试数据转换器
            TestDataConverter();

            // 测试CRC计算
            TestCrcCalculation();

            Console.WriteLine("\n" + new string('=', 60));
            Console.WriteLine("          所有测试完成！");
            Console.WriteLine(new string('=', 60));
        }
        finally
        {
            simulator.Stop();
        }
    }

    /// <summary>
    /// 测试TCP客户端
    /// </summary>
    public async Task TestTcpClientAsync()
    {
        Console.WriteLine("\n" + new string('-', 50));
        Console.WriteLine("【测试1】Modbus TCP 客户端测试");
        Console.WriteLine(new string('-', 50));

        var config = new ConnectionConfig
        {
            ConnectionId = "test-tcp",
            Name = "测试TCP连接",
            ConnectionType = ConnectionType.TCP,
            TcpConfig = new TcpConfig
            {
                IpAddress = "127.0.0.1",
                Port = 5020,
                ConnectTimeoutMs = 5000
            }
        };

        using var client = new ModbusTcpClient(config, _loggerFactory.CreateLogger<ModbusTcpClient>(), 3000);

        // 测试连接
        Console.WriteLine("\n[测试] 连接到服务器...");
        var connected = await client.ConnectAsync();
        Console.WriteLine($"  连接状态: {(connected ? "成功 ✓" : "失败 ✗")}");
        if (!connected) return;

        // 测试读取保持寄存器 (FC03)
        Console.WriteLine("\n[测试] 读取保持寄存器 (FC03) - 地址0, 数量10");
        var holdingResult = await client.ReadHoldingRegistersAsync(1, 0, 10);
        PrintResult("读取保持寄存器", holdingResult);
        if (holdingResult.Success && holdingResult.Data != null)
        {
            Console.WriteLine("  寄存器值:");
            for (int i = 0; i < holdingResult.Data.Length; i++)
            {
                Console.WriteLine($"    [地址 {i}] = {holdingResult.Data[i]} (0x{holdingResult.Data[i]:X4})");
            }
        }

        // 测试读取输入寄存器 (FC04)
        Console.WriteLine("\n[测试] 读取输入寄存器 (FC04) - 地址0, 数量4");
        var inputResult = await client.ReadInputRegistersAsync(1, 0, 4);
        PrintResult("读取输入寄存器", inputResult);
        if (inputResult.Success && inputResult.Data != null)
        {
            Console.WriteLine("  寄存器值:");
            for (int i = 0; i < inputResult.Data.Length; i++)
            {
                Console.WriteLine($"    [地址 {i}] = {inputResult.Data[i]} (0x{inputResult.Data[i]:X4})");
            }
        }

        // 测试读取线圈 (FC01)
        Console.WriteLine("\n[测试] 读取线圈 (FC01) - 地址0, 数量8");
        var coilResult = await client.ReadCoilsAsync(1, 0, 8);
        PrintResult("读取线圈", coilResult);
        if (coilResult.Success && coilResult.Data != null)
        {
            Console.WriteLine($"  线圈值: [{string.Join(", ", coilResult.Data.Select(b => b ? "ON" : "OFF"))}]");
        }

        // 测试读取离散输入 (FC02)
        Console.WriteLine("\n[测试] 读取离散输入 (FC02) - 地址0, 数量4");
        var discreteResult = await client.ReadDiscreteInputsAsync(1, 0, 4);
        PrintResult("读取离散输入", discreteResult);
        if (discreteResult.Success && discreteResult.Data != null)
        {
            Console.WriteLine($"  离散输入值: [{string.Join(", ", discreteResult.Data.Select(b => b ? "ON" : "OFF"))}]");
        }

        // 断开连接
        await client.DisconnectAsync();
        Console.WriteLine("\n[测试] 已断开连接");
    }

    /// <summary>
    /// 测试RTU客户端（需要实际串口设备）
    /// </summary>
    public async Task TestRtuClientAsync(string portName)
    {
        Console.WriteLine("\n" + new string('-', 50));
        Console.WriteLine("【测试2】Modbus RTU 客户端测试");
        Console.WriteLine(new string('-', 50));

        var config = new ConnectionConfig
        {
            ConnectionId = "test-rtu",
            Name = "测试RTU连接",
            ConnectionType = ConnectionType.RTU,
            RtuConfig = new RtuConfig
            {
                PortName = portName,
                BaudRate = 9600,
                DataBits = 8,
                StopBits = StopBitsConfig.One,
                Parity = ParityConfig.None,
                ReadTimeoutMs = 1000,
                WriteTimeoutMs = 1000
            }
        };

        using var client = new ModbusRtuClient(config, _loggerFactory.CreateLogger<ModbusRtuClient>(), 3000);

        Console.WriteLine($"\n[测试] 连接到串口 {portName}...");
        var connected = await client.ConnectAsync();
        Console.WriteLine($"  连接状态: {(connected ? "成功 ✓" : "失败 ✗")}");

        if (connected)
        {
            Console.WriteLine("\n[测试] 读取保持寄存器 (FC03) - 从站1, 地址0, 数量10");
            var result = await client.ReadHoldingRegistersAsync(1, 0, 10);
            PrintResult("读取保持寄存器", result);

            if (result.Success && result.Data != null)
            {
                Console.WriteLine("  寄存器值:");
                for (int i = 0; i < result.Data.Length; i++)
                {
                    Console.WriteLine($"    [地址 {i}] = {result.Data[i]} (0x{result.Data[i]:X4})");
                }
            }

            await client.DisconnectAsync();
        }
    }

    /// <summary>
    /// 测试数据转换器
    /// </summary>
    public void TestDataConverter()
    {
        Console.WriteLine("\n" + new string('-', 50));
        Console.WriteLine("【测试2】数据转换器测试");
        Console.WriteLine(new string('-', 50));

        var converter = new DataConverter();

        // 测试Float32转换 (BigEndian)
        Console.WriteLine("\n[测试] Float32转换 (BigEndian)");
        ushort[] floatRegisters = [0x42F6, 0xE979]; // 123.456 in BigEndian
        var floatValue = converter.ConvertFromRegisters(floatRegisters, 0, ModbusDataType.Float32, ByteOrder.BigEndian);
        Console.WriteLine($"  输入寄存器: [0x{floatRegisters[0]:X4}, 0x{floatRegisters[1]:X4}]");
        Console.WriteLine($"  转换结果: {floatValue} (期望: ~123.456)");

        // 测试UInt32转换
        Console.WriteLine("\n[测试] UInt32转换 (BigEndian)");
        ushort[] uint32Registers = [0x0001, 0x86A0]; // 100000 in BigEndian
        var uint32Value = converter.ConvertFromRegisters(uint32Registers, 0, ModbusDataType.UInt32, ByteOrder.BigEndian);
        Console.WriteLine($"  输入寄存器: [0x{uint32Registers[0]:X4}, 0x{uint32Registers[1]:X4}]");
        Console.WriteLine($"  转换结果: {uint32Value} (期望: 100000)");

        // 测试Int16转换
        Console.WriteLine("\n[测试] Int16转换");
        ushort[] int16Registers = [0xFFFF]; // -1
        var int16Value = converter.ConvertFromRegisters(int16Registers, 0, ModbusDataType.Int16, ByteOrder.BigEndian);
        Console.WriteLine($"  输入寄存器: [0x{int16Registers[0]:X4}]");
        Console.WriteLine($"  转换结果: {int16Value} (期望: -1)");

        // 测试位操作
        Console.WriteLine("\n[测试] 位操作");
        ushort statusWord = 0b1010_0101_1100_0011;
        Console.WriteLine($"  状态字: 0x{statusWord:X4} (二进制: {Convert.ToString(statusWord, 2).PadLeft(16, '0')})");
        for (int i = 0; i < 8; i++)
        {
            var bitValue = DataConverter.GetBit(statusWord, i);
            Console.WriteLine($"    Bit {i}: {(bitValue ? "1" : "0")}");
        }

        // 测试值到寄存器转换
        Console.WriteLine("\n[测试] 值到寄存器转换");
        var registers = converter.ConvertToRegisters(123.456f, ModbusDataType.Float32, ByteOrder.BigEndian);
        Console.WriteLine($"  输入值: 123.456f");
        Console.WriteLine($"  转换结果: [{string.Join(", ", registers.Select(r => $"0x{r:X4}"))}]");
    }

    /// <summary>
    /// 测试CRC计算
    /// </summary>
    public void TestCrcCalculation()
    {
        Console.WriteLine("\n" + new string('-', 50));
        Console.WriteLine("【测试3】CRC16校验测试");
        Console.WriteLine(new string('-', 50));

        // 标准测试向量: 从站1, 功能码3, 地址0, 数量10
        byte[] testData = [0x01, 0x03, 0x00, 0x00, 0x00, 0x0A];
        var crc = ModbusUtils.CalculateCrc16(testData);
        var dataWithCrc = ModbusUtils.AppendCrc16(testData);

        Console.WriteLine($"\n[测试] CRC16计算");
        Console.WriteLine($"  输入数据: {ModbusUtils.ToHexString(testData)}");
        Console.WriteLine($"  CRC结果: 0x{crc:X4}");
        Console.WriteLine($"  完整帧: {ModbusUtils.ToHexString(dataWithCrc)}");

        // 验证CRC
        var isValid = ModbusUtils.VerifyCrc16(dataWithCrc);
        Console.WriteLine($"  CRC验证: {(isValid ? "通过 ✓" : "失败 ✗")}");

        // 测试错误CRC
        dataWithCrc[^1] = 0x00; // 破坏CRC
        var isInvalid = !ModbusUtils.VerifyCrc16(dataWithCrc);
        Console.WriteLine($"  错误CRC检测: {(isInvalid ? "通过 ✓" : "失败 ✗")}");
    }

    private static void PrintResult<T>(string operation, ModbusResponse<T> response)
    {
        if (response.Success)
        {
            Console.WriteLine($"  {operation}: 成功 ✓ (耗时: {response.ResponseTimeMs}ms)");
        }
        else
        {
            Console.WriteLine($"  {operation}: 失败 ✗");
            Console.WriteLine($"    错误: {response.ErrorMessage}");
            if (response.ExceptionCode.HasValue)
            {
                Console.WriteLine($"    异常代码: 0x{response.ExceptionCode:X2}");
            }
        }

        if (response.RawRequest != null)
        {
            Console.WriteLine($"  请求: {ModbusUtils.ToHexString(response.RawRequest)}");
        }
        if (response.RawResponse != null)
        {
            Console.WriteLine($"  响应: {ModbusUtils.ToHexString(response.RawResponse)}");
        }
    }
}
