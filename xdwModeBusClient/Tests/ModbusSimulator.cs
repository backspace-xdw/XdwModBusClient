using System.Net;
using System.Net.Sockets;

namespace xdwModeBusClient.Tests;

/// <summary>
/// 简单的Modbus TCP模拟服务器 - 用于测试
/// </summary>
public class ModbusTcpSimulator : IDisposable
{
    private readonly TcpListener _listener;
    private readonly CancellationTokenSource _cts = new();
    private Task? _listenerTask;
    private readonly Dictionary<int, ushort[]> _holdingRegisters = new();
    private readonly Dictionary<int, ushort[]> _inputRegisters = new();
    private readonly Dictionary<int, bool[]> _coils = new();
    private readonly Dictionary<int, bool[]> _discreteInputs = new();
    private readonly object _lock = new();

    public int Port { get; }
    public bool IsRunning { get; private set; }

    public ModbusTcpSimulator(int port = 502)
    {
        Port = port;
        _listener = new TcpListener(IPAddress.Any, port);

        // 初始化默认数据 - 从站1
        InitializeSlaveData(1);
    }

    private void InitializeSlaveData(byte slaveId)
    {
        // 初始化保持寄存器 (1000个)
        var holding = new ushort[1000];
        // 填充一些测试数据
        holding[0] = 0x4248;  // Float32高字节 (123.456)
        holding[1] = 0x42F6;  // Float32低字节
        holding[2] = 0x4396;  // Float32高字节 (300.5)
        holding[3] = 0x8000;  // Float32低字节
        holding[4] = 1234;    // UInt16
        holding[5] = 5678;    // UInt16
        holding[6] = 0x00FF;  // 状态字
        holding[7] = 100;
        holding[8] = 200;
        holding[9] = 300;

        // 地址100开始的数据
        holding[100] = 1500;  // 电机转速
        holding[101] = 350;   // 电机电流 * 100
        holding[102] = 0x0000; // 累计运行时间高字
        holding[103] = 0x1234; // 累计运行时间低字
        holding[104] = 0x0000; // 生产计数高字
        holding[105] = 0x0064; // 生产计数低字

        _holdingRegisters[slaveId] = holding;

        // 初始化输入寄存器 (1000个)
        var input = new ushort[1000];
        input[0] = 32767;     // 模拟量输入1
        input[1] = 16384;     // 模拟量输入2
        input[2] = 8192;
        input[3] = 4096;
        _inputRegisters[slaveId] = input;

        // 初始化线圈 (1000个)
        var coils = new bool[1000];
        coils[0] = true;
        coils[1] = false;
        coils[2] = true;
        coils[3] = true;
        coils[4] = false;
        _coils[slaveId] = coils;

        // 初始化离散输入 (1000个)
        var discrete = new bool[1000];
        discrete[0] = true;
        discrete[1] = true;
        discrete[2] = false;
        discrete[3] = true;
        _discreteInputs[slaveId] = discrete;
    }

    public void Start()
    {
        if (IsRunning) return;

        _listener.Start();
        IsRunning = true;
        _listenerTask = Task.Run(() => AcceptClientsAsync(_cts.Token));
        Console.WriteLine($"[模拟器] Modbus TCP模拟服务器已启动，监听端口: {Port}");
    }

    public void Stop()
    {
        if (!IsRunning) return;

        _cts.Cancel();
        _listener.Stop();
        IsRunning = false;
        Console.WriteLine("[模拟器] Modbus TCP模拟服务器已停止");
    }

    private async Task AcceptClientsAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                var client = await _listener.AcceptTcpClientAsync(cancellationToken);
                Console.WriteLine($"[模拟器] 客户端已连接: {client.Client.RemoteEndPoint}");
                _ = HandleClientAsync(client, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[模拟器] 接受连接错误: {ex.Message}");
            }
        }
    }

    private async Task HandleClientAsync(TcpClient client, CancellationToken cancellationToken)
    {
        using var stream = client.GetStream();
        var buffer = new byte[1024];

        try
        {
            while (!cancellationToken.IsCancellationRequested && client.Connected)
            {
                var bytesRead = await stream.ReadAsync(buffer, cancellationToken);
                if (bytesRead == 0) break;

                var request = buffer.Take(bytesRead).ToArray();
                Console.WriteLine($"[模拟器] 收到请求: {BitConverter.ToString(request)}");

                var response = ProcessRequest(request);
                if (response != null)
                {
                    await stream.WriteAsync(response, cancellationToken);
                    Console.WriteLine($"[模拟器] 发送响应: {BitConverter.ToString(response)}");
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[模拟器] 处理客户端错误: {ex.Message}");
        }
        finally
        {
            client.Close();
            Console.WriteLine("[模拟器] 客户端已断开");
        }
    }

    private byte[]? ProcessRequest(byte[] request)
    {
        if (request.Length < 12) return null;

        // MBAP Header
        var transactionId = (ushort)((request[0] << 8) | request[1]);
        var protocolId = (ushort)((request[2] << 8) | request[3]);
        var slaveId = request[6];
        var functionCode = request[7];

        // 确保从站存在
        if (!_holdingRegisters.ContainsKey(slaveId))
        {
            InitializeSlaveData(slaveId);
        }

        var startAddress = (ushort)((request[8] << 8) | request[9]);
        var quantity = (ushort)((request[10] << 8) | request[11]);

        byte[]? pdu = functionCode switch
        {
            0x01 => ReadCoils(slaveId, startAddress, quantity),
            0x02 => ReadDiscreteInputs(slaveId, startAddress, quantity),
            0x03 => ReadHoldingRegisters(slaveId, startAddress, quantity),
            0x04 => ReadInputRegisters(slaveId, startAddress, quantity),
            0x05 => WriteSingleCoil(slaveId, startAddress, quantity, request),
            0x06 => WriteSingleRegister(slaveId, startAddress, quantity, request),
            0x0F => WriteMultipleCoils(slaveId, startAddress, quantity, request),
            0x10 => WriteMultipleRegisters(slaveId, startAddress, quantity, request),
            _ => CreateExceptionResponse(functionCode, 0x01) // 非法功能码
        };

        if (pdu == null) return null;

        // 构建响应
        var response = new byte[6 + pdu.Length];
        response[0] = (byte)(transactionId >> 8);
        response[1] = (byte)(transactionId & 0xFF);
        response[2] = 0x00;
        response[3] = 0x00;
        response[4] = (byte)((pdu.Length + 1) >> 8);
        response[5] = (byte)((pdu.Length + 1) & 0xFF);
        Array.Copy(pdu, 0, response, 6, pdu.Length);

        // 插入slaveId
        var finalResponse = new byte[response.Length + 1];
        Array.Copy(response, 0, finalResponse, 0, 6);
        finalResponse[6] = slaveId;
        Array.Copy(pdu, 0, finalResponse, 7, pdu.Length);
        finalResponse[4] = (byte)((pdu.Length + 1) >> 8);
        finalResponse[5] = (byte)((pdu.Length + 1) & 0xFF);

        return finalResponse;
    }

    private byte[] ReadCoils(byte slaveId, ushort startAddress, ushort quantity)
    {
        lock (_lock)
        {
            var coils = _coils[slaveId];
            var byteCount = (quantity + 7) / 8;
            var pdu = new byte[2 + byteCount];
            pdu[0] = 0x01;
            pdu[1] = (byte)byteCount;

            for (int i = 0; i < quantity; i++)
            {
                if (startAddress + i < coils.Length && coils[startAddress + i])
                {
                    pdu[2 + i / 8] |= (byte)(1 << (i % 8));
                }
            }

            return pdu;
        }
    }

    private byte[] ReadDiscreteInputs(byte slaveId, ushort startAddress, ushort quantity)
    {
        lock (_lock)
        {
            var discrete = _discreteInputs[slaveId];
            var byteCount = (quantity + 7) / 8;
            var pdu = new byte[2 + byteCount];
            pdu[0] = 0x02;
            pdu[1] = (byte)byteCount;

            for (int i = 0; i < quantity; i++)
            {
                if (startAddress + i < discrete.Length && discrete[startAddress + i])
                {
                    pdu[2 + i / 8] |= (byte)(1 << (i % 8));
                }
            }

            return pdu;
        }
    }

    private byte[] ReadHoldingRegisters(byte slaveId, ushort startAddress, ushort quantity)
    {
        lock (_lock)
        {
            var registers = _holdingRegisters[slaveId];
            var byteCount = quantity * 2;
            var pdu = new byte[2 + byteCount];
            pdu[0] = 0x03;
            pdu[1] = (byte)byteCount;

            for (int i = 0; i < quantity; i++)
            {
                var value = startAddress + i < registers.Length ? registers[startAddress + i] : (ushort)0;
                pdu[2 + i * 2] = (byte)(value >> 8);
                pdu[2 + i * 2 + 1] = (byte)(value & 0xFF);
            }

            return pdu;
        }
    }

    private byte[] ReadInputRegisters(byte slaveId, ushort startAddress, ushort quantity)
    {
        lock (_lock)
        {
            var registers = _inputRegisters[slaveId];
            var byteCount = quantity * 2;
            var pdu = new byte[2 + byteCount];
            pdu[0] = 0x04;
            pdu[1] = (byte)byteCount;

            for (int i = 0; i < quantity; i++)
            {
                var value = startAddress + i < registers.Length ? registers[startAddress + i] : (ushort)0;
                pdu[2 + i * 2] = (byte)(value >> 8);
                pdu[2 + i * 2 + 1] = (byte)(value & 0xFF);
            }

            return pdu;
        }
    }

    private byte[] WriteSingleCoil(byte slaveId, ushort address, ushort value, byte[] request)
    {
        lock (_lock)
        {
            var coils = _coils[slaveId];
            if (address < coils.Length)
            {
                coils[address] = value == 0xFF00;
            }

            return [0x05, (byte)(address >> 8), (byte)(address & 0xFF), (byte)(value >> 8), (byte)(value & 0xFF)];
        }
    }

    private byte[] WriteSingleRegister(byte slaveId, ushort address, ushort value, byte[] request)
    {
        lock (_lock)
        {
            var registers = _holdingRegisters[slaveId];
            if (address < registers.Length)
            {
                registers[address] = value;
            }

            return [0x06, (byte)(address >> 8), (byte)(address & 0xFF), (byte)(value >> 8), (byte)(value & 0xFF)];
        }
    }

    private byte[] WriteMultipleCoils(byte slaveId, ushort startAddress, ushort quantity, byte[] request)
    {
        lock (_lock)
        {
            var coils = _coils[slaveId];
            var byteCount = request[12];

            for (int i = 0; i < quantity && startAddress + i < coils.Length; i++)
            {
                var byteIndex = i / 8;
                var bitIndex = i % 8;
                if (13 + byteIndex < request.Length)
                {
                    coils[startAddress + i] = ((request[13 + byteIndex] >> bitIndex) & 1) == 1;
                }
            }

            return [0x0F, (byte)(startAddress >> 8), (byte)(startAddress & 0xFF), (byte)(quantity >> 8), (byte)(quantity & 0xFF)];
        }
    }

    private byte[] WriteMultipleRegisters(byte slaveId, ushort startAddress, ushort quantity, byte[] request)
    {
        lock (_lock)
        {
            var registers = _holdingRegisters[slaveId];
            var byteCount = request[12];

            for (int i = 0; i < quantity && startAddress + i < registers.Length; i++)
            {
                if (13 + i * 2 + 1 < request.Length)
                {
                    registers[startAddress + i] = (ushort)((request[13 + i * 2] << 8) | request[13 + i * 2 + 1]);
                }
            }

            return [0x10, (byte)(startAddress >> 8), (byte)(startAddress & 0xFF), (byte)(quantity >> 8), (byte)(quantity & 0xFF)];
        }
    }

    private static byte[] CreateExceptionResponse(byte functionCode, byte exceptionCode)
    {
        return [(byte)(functionCode | 0x80), exceptionCode];
    }

    public void Dispose()
    {
        Stop();
        _cts.Dispose();
        GC.SuppressFinalize(this);
    }
}
