# xdwModeBusClient

Modbus TCP/RTU 客户端工具，功能类似于 ModScan32，基于 .NET 9 开发。

## 功能特性

### 协议支持
- **Modbus TCP** - 支持标准Modbus TCP协议
- **Modbus RTU** - 支持标准Modbus RTU协议（串口通信）

### 功能码支持
| 功能码 | 描述 | 说明 |
|--------|------|------|
| 0x01 | 读线圈状态 | Read Coils |
| 0x02 | 读离散输入 | Read Discrete Inputs |
| 0x03 | 读保持寄存器 | Read Holding Registers |
| 0x04 | 读输入寄存器 | Read Input Registers |
| 0x05 | 写单个线圈 | Write Single Coil |
| 0x06 | 写单个寄存器 | Write Single Register |
| 0x0F | 写多个线圈 | Write Multiple Coils |
| 0x10 | 写多个寄存器 | Write Multiple Registers |

### 数据类型支持
- UInt16 / Int16 (16位整数)
- UInt32 / Int32 (32位整数)
- Float32 (32位浮点数)
- UInt64 / Int64 (64位整数)
- Float64 (64位浮点数)
- Boolean (布尔值)
- String (ASCII字符串)

### 字节顺序支持
- **BigEndian** (ABCD) - 大端模式
- **LittleEndian** (DCBA) - 小端模式
- **BigEndianByteSwap** (BADC) - 大端字节交换
- **LittleEndianByteSwap** (CDAB) - 小端字节交换

### 其他特性
- 多连接支持（同时连接多个设备）
- 多数据包配置（支持不同轮询间隔）
- 数据点位配置（支持缩放、偏移、位操作）
- 自动重试机制
- 数据存储预留接口
- 数据显示预留接口
- 完整的日志记录

## 项目结构

```
xdwModeBusClient/
├── Configuration/
│   └── ModbusConfiguration.cs    # 配置模型
├── Interfaces/
│   ├── IModbusClient.cs          # Modbus客户端接口
│   └── IDataProcessor.cs         # 数据处理接口
├── Models/
│   └── ModbusDataType.cs         # 枚举定义
├── Services/
│   ├── Modbus/
│   │   ├── ModbusTcpClient.cs    # TCP客户端实现
│   │   └── ModbusRtuClient.cs    # RTU客户端实现
│   ├── DataProcessing/
│   │   └── DataProcessor.cs      # 数据处理器
│   └── Polling/
│       └── PollingManager.cs     # 轮询管理器
├── Utils/
│   ├── ModbusUtils.cs            # CRC校验等工具
│   └── DataConverter.cs          # 数据转换器
├── Program.cs                    # 主程序入口
├── appsettings.json              # 配置文件
└── xdwModeBusClient.csproj       # 项目文件
```

## 快速开始

### 编译项目

```bash
cd xdwModeBusClient/xdwModeBusClient
dotnet build
```

### 运行项目

```bash
dotnet run
```

### 配置文件说明

配置文件 `appsettings.json` 包含以下主要部分：

#### 应用设置 (AppSettings)

```json
{
  "AppSettings": {
    "PollingIntervalMs": 1000,      // 默认轮询间隔(毫秒)
    "RequestTimeoutMs": 3000,       // 请求超时时间
    "RetryCount": 3,                // 重试次数
    "RetryIntervalMs": 500,         // 重试间隔
    "EnableLogging": true,          // 启用日志
    "EnableDataStorage": true,      // 启用数据存储
    "DataStoragePath": "data/",     // 数据存储路径
    "RequestDelayMs": 50            // 请求间延迟
  }
}
```

#### TCP连接配置

```json
{
  "ConnectionId": "tcp-plc-01",
  "Name": "主PLC设备",
  "ConnectionType": 0,              // 0=TCP, 1=RTU
  "Enabled": true,
  "TcpConfig": {
    "IpAddress": "192.168.1.100",
    "Port": 502,
    "ConnectTimeoutMs": 5000,
    "KeepAlive": true
  }
}
```

#### RTU连接配置

```json
{
  "ConnectionId": "rtu-device-01",
  "Name": "RTU从站设备",
  "ConnectionType": 1,
  "Enabled": true,
  "RtuConfig": {
    "PortName": "/dev/ttyUSB0",     // Linux: /dev/ttyUSB0, Windows: COM1
    "BaudRate": 9600,
    "DataBits": 8,
    "StopBits": 1,                  // 0=None, 1=One, 2=Two, 3=OnePointFive
    "Parity": 0,                    // 0=None, 1=Odd, 2=Even, 3=Mark, 4=Space
    "ReadTimeoutMs": 1000,
    "WriteTimeoutMs": 1000,
    "FrameIntervalMs": 50
  }
}
```

#### 数据包配置

```json
{
  "PacketId": "packet-holding-01",
  "Name": "保持寄存器数据包1",
  "ConnectionId": "tcp-plc-01",
  "Enabled": true,
  "SlaveId": 1,                     // 从站地址 (1-247)
  "FunctionCode": 3,                // 功能码
  "StartAddress": 0,                // 起始地址
  "RegisterCount": 10,              // 寄存器数量 (最大126)
  "PollingIntervalMs": 1000,        // 轮询间隔 (0=使用全局设置)
  "DataPoints": [...]               // 数据点配置
}
```

#### 数据点配置

```json
{
  "PointId": "temperature",
  "Name": "温度",
  "Offset": 0,                      // 相对于包起始地址的偏移
  "DataType": 4,                    // 0=UInt16, 1=Int16, 2=UInt32, 3=Int32, 4=Float32...
  "ByteOrder": 0,                   // 0=BigEndian, 1=LittleEndian, 2=BigEndianByteSwap, 3=LittleEndianByteSwap
  "Scale": 0.1,                     // 缩放系数
  "OffsetValue": 0.0,               // 偏移量
  "Unit": "°C",                     // 单位
  "Description": "当前温度值",
  "BitIndex": -1,                   // 位索引 (-1表示不使用位操作)
  "StringLength": 0                 // 字符串长度
}
```

## 扩展开发

### 自定义数据存储

实现 `IDataStorage` 接口：

```csharp
public class MyDataStorage : IDataStorage
{
    public async Task StoreAsync(PollingResult result, CancellationToken cancellationToken = default)
    {
        // 存储到数据库、文件、云端等
    }

    public async Task<IEnumerable<PollingResult>> QueryAsync(string packetId, DateTime startTime, DateTime endTime, CancellationToken cancellationToken = default)
    {
        // 查询历史数据
    }
}
```

### 自定义数据显示

实现 `IDataDisplay` 接口：

```csharp
public class MyDataDisplay : IDataDisplay
{
    public void Display(PollingResult result)
    {
        // 显示到UI、Web页面等
    }

    public void DisplayError(string packetId, string errorMessage)
    {
        // 显示错误信息
    }

    public void DisplayConnectionStatus(string connectionId, bool isConnected)
    {
        // 显示连接状态
    }
}
```

### 自定义数据处理

使用 `DataProcessor.RegisterDataHandler` 注册回调：

```csharp
var dataProcessor = serviceProvider.GetRequiredService<IDataProcessor>();
((DataProcessor)dataProcessor).RegisterDataHandler(result =>
{
    // 处理采集到的数据
    Console.WriteLine($"收到数据: {result.PacketId}");
});
```

### 事件订阅

订阅 `PollingManager` 的事件：

```csharp
pollingManager.ConnectionStatusChanged += (sender, e) =>
{
    Console.WriteLine($"连接 {e.ConnectionId} 状态: {(e.IsConnected ? "已连接" : "已断开")}");
};

pollingManager.DataPolled += (sender, result) =>
{
    Console.WriteLine($"数据包 {result.PacketId} 采集完成");
};
```

## 注意事项

1. **单次请求寄存器限制**: Modbus协议限制单次请求最多读取125个寄存器（250字节），本项目建议配置不超过126个。

2. **RTU串口权限**: Linux系统需要确保用户有串口访问权限：
   ```bash
   sudo usermod -a -G dialout $USER
   ```

3. **防火墙设置**: 确保TCP 502端口可访问。

4. **数据存储路径**: 确保程序有写入权限。

## 许可证

MIT License
