using xdwModeBusClient.Models;

namespace xdwModeBusClient.Utils;

/// <summary>
/// Modbus工具类
/// </summary>
public static class ModbusUtils
{
    /// <summary>
    /// CRC16查找表（预计算）
    /// </summary>
    private static readonly ushort[] Crc16Table = GenerateCrc16Table();

    /// <summary>
    /// 生成CRC16查找表
    /// </summary>
    private static ushort[] GenerateCrc16Table()
    {
        var table = new ushort[256];
        const ushort polynomial = 0xA001;

        for (ushort i = 0; i < 256; i++)
        {
            ushort crc = i;
            for (int j = 0; j < 8; j++)
            {
                if ((crc & 0x0001) != 0)
                    crc = (ushort)((crc >> 1) ^ polynomial);
                else
                    crc >>= 1;
            }
            table[i] = crc;
        }
        return table;
    }

    /// <summary>
    /// 计算Modbus RTU CRC16校验
    /// </summary>
    /// <param name="data">数据</param>
    /// <returns>CRC16校验值（低字节在前）</returns>
    public static ushort CalculateCrc16(byte[] data)
    {
        return CalculateCrc16(data, 0, data.Length);
    }

    /// <summary>
    /// 计算Modbus RTU CRC16校验
    /// </summary>
    /// <param name="data">数据</param>
    /// <param name="offset">偏移量</param>
    /// <param name="length">长度</param>
    /// <returns>CRC16校验值</returns>
    public static ushort CalculateCrc16(byte[] data, int offset, int length)
    {
        ushort crc = 0xFFFF;
        for (int i = offset; i < offset + length; i++)
        {
            crc = (ushort)((crc >> 8) ^ Crc16Table[(crc ^ data[i]) & 0xFF]);
        }
        return crc;
    }

    /// <summary>
    /// 验证CRC16校验
    /// </summary>
    /// <param name="data">包含CRC的完整数据</param>
    /// <returns>是否校验通过</returns>
    public static bool VerifyCrc16(byte[] data)
    {
        if (data.Length < 3) return false;

        var calculatedCrc = CalculateCrc16(data, 0, data.Length - 2);
        var receivedCrc = (ushort)(data[^2] | (data[^1] << 8));
        return calculatedCrc == receivedCrc;
    }

    /// <summary>
    /// 追加CRC16到数据末尾
    /// </summary>
    /// <param name="data">数据</param>
    /// <returns>追加CRC后的数据</returns>
    public static byte[] AppendCrc16(byte[] data)
    {
        var result = new byte[data.Length + 2];
        Array.Copy(data, result, data.Length);
        var crc = CalculateCrc16(data);
        result[^2] = (byte)(crc & 0xFF);        // CRC低字节
        result[^1] = (byte)((crc >> 8) & 0xFF); // CRC高字节
        return result;
    }

    /// <summary>
    /// 获取Modbus异常代码描述
    /// </summary>
    /// <param name="exceptionCode">异常代码</param>
    /// <returns>异常描述</returns>
    public static string GetExceptionDescription(byte exceptionCode)
    {
        return exceptionCode switch
        {
            0x01 => "非法功能码",
            0x02 => "非法数据地址",
            0x03 => "非法数据值",
            0x04 => "从站设备故障",
            0x05 => "确认",
            0x06 => "从站设备忙",
            0x08 => "存储奇偶校验错误",
            0x0A => "网关路径不可用",
            0x0B => "网关目标设备未响应",
            _ => $"未知异常代码: 0x{exceptionCode:X2}"
        };
    }

    /// <summary>
    /// 获取功能码所需的寄存器数量
    /// </summary>
    /// <param name="dataType">数据类型</param>
    /// <returns>寄存器数量</returns>
    public static int GetRegisterCount(ModbusDataType dataType)
    {
        return dataType switch
        {
            ModbusDataType.UInt16 => 1,
            ModbusDataType.Int16 => 1,
            ModbusDataType.UInt32 => 2,
            ModbusDataType.Int32 => 2,
            ModbusDataType.Float32 => 2,
            ModbusDataType.UInt64 => 4,
            ModbusDataType.Int64 => 4,
            ModbusDataType.Float64 => 4,
            ModbusDataType.Boolean => 1,
            _ => 1
        };
    }

    /// <summary>
    /// 将字节数组转换为十六进制字符串
    /// </summary>
    /// <param name="data">数据</param>
    /// <param name="separator">分隔符</param>
    /// <returns>十六进制字符串</returns>
    public static string ToHexString(byte[] data, string separator = " ")
    {
        return string.Join(separator, data.Select(b => b.ToString("X2")));
    }

    /// <summary>
    /// 将十六进制字符串转换为字节数组
    /// </summary>
    /// <param name="hex">十六进制字符串</param>
    /// <returns>字节数组</returns>
    public static byte[] FromHexString(string hex)
    {
        hex = hex.Replace(" ", "").Replace("-", "");
        if (hex.Length % 2 != 0)
            throw new ArgumentException("十六进制字符串长度必须为偶数");

        var bytes = new byte[hex.Length / 2];
        for (int i = 0; i < bytes.Length; i++)
        {
            bytes[i] = Convert.ToByte(hex.Substring(i * 2, 2), 16);
        }
        return bytes;
    }
}
