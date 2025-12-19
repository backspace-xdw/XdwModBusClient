using System.Text;
using xdwModeBusClient.Interfaces;
using xdwModeBusClient.Models;

namespace xdwModeBusClient.Utils;

/// <summary>
/// 数据转换器实现
/// </summary>
public class DataConverter : IDataConverter
{
    /// <summary>
    /// 将寄存器数据转换为指定类型
    /// </summary>
    public object ConvertFromRegisters(ushort[] registers, int offset, ModbusDataType dataType, ByteOrder byteOrder)
    {
        return dataType switch
        {
            ModbusDataType.UInt16 => GetUInt16(registers, offset, byteOrder),
            ModbusDataType.Int16 => GetInt16(registers, offset, byteOrder),
            ModbusDataType.UInt32 => GetUInt32(registers, offset, byteOrder),
            ModbusDataType.Int32 => GetInt32(registers, offset, byteOrder),
            ModbusDataType.Float32 => GetFloat32(registers, offset, byteOrder),
            ModbusDataType.UInt64 => GetUInt64(registers, offset, byteOrder),
            ModbusDataType.Int64 => GetInt64(registers, offset, byteOrder),
            ModbusDataType.Float64 => GetFloat64(registers, offset, byteOrder),
            ModbusDataType.Boolean => registers[offset] != 0,
            ModbusDataType.String => GetString(registers, offset, registers.Length - offset),
            _ => throw new ArgumentException($"不支持的数据类型: {dataType}")
        };
    }

    /// <summary>
    /// 将值转换为寄存器数据
    /// </summary>
    public ushort[] ConvertToRegisters(object value, ModbusDataType dataType, ByteOrder byteOrder)
    {
        return dataType switch
        {
            ModbusDataType.UInt16 => SetUInt16(Convert.ToUInt16(value), byteOrder),
            ModbusDataType.Int16 => SetInt16(Convert.ToInt16(value), byteOrder),
            ModbusDataType.UInt32 => SetUInt32(Convert.ToUInt32(value), byteOrder),
            ModbusDataType.Int32 => SetInt32(Convert.ToInt32(value), byteOrder),
            ModbusDataType.Float32 => SetFloat32(Convert.ToSingle(value), byteOrder),
            ModbusDataType.UInt64 => SetUInt64(Convert.ToUInt64(value), byteOrder),
            ModbusDataType.Int64 => SetInt64(Convert.ToInt64(value), byteOrder),
            ModbusDataType.Float64 => SetFloat64(Convert.ToDouble(value), byteOrder),
            ModbusDataType.Boolean => [Convert.ToBoolean(value) ? (ushort)1 : (ushort)0],
            ModbusDataType.String => SetString(value.ToString() ?? "", 0),
            _ => throw new ArgumentException($"不支持的数据类型: {dataType}")
        };
    }

    #region 读取方法

    private static ushort GetUInt16(ushort[] registers, int offset, ByteOrder byteOrder)
    {
        var value = registers[offset];
        return byteOrder switch
        {
            ByteOrder.BigEndian => value,
            ByteOrder.LittleEndian => SwapBytes(value),
            ByteOrder.BigEndianByteSwap => SwapBytes(value),
            ByteOrder.LittleEndianByteSwap => value,
            _ => value
        };
    }

    private static short GetInt16(ushort[] registers, int offset, ByteOrder byteOrder)
    {
        return (short)GetUInt16(registers, offset, byteOrder);
    }

    private static uint GetUInt32(ushort[] registers, int offset, ByteOrder byteOrder)
    {
        var bytes = GetBytes(registers, offset, 2, byteOrder);
        return BitConverter.ToUInt32(bytes, 0);
    }

    private static int GetInt32(ushort[] registers, int offset, ByteOrder byteOrder)
    {
        var bytes = GetBytes(registers, offset, 2, byteOrder);
        return BitConverter.ToInt32(bytes, 0);
    }

    private static float GetFloat32(ushort[] registers, int offset, ByteOrder byteOrder)
    {
        var bytes = GetBytes(registers, offset, 2, byteOrder);
        return BitConverter.ToSingle(bytes, 0);
    }

    private static ulong GetUInt64(ushort[] registers, int offset, ByteOrder byteOrder)
    {
        var bytes = GetBytes(registers, offset, 4, byteOrder);
        return BitConverter.ToUInt64(bytes, 0);
    }

    private static long GetInt64(ushort[] registers, int offset, ByteOrder byteOrder)
    {
        var bytes = GetBytes(registers, offset, 4, byteOrder);
        return BitConverter.ToInt64(bytes, 0);
    }

    private static double GetFloat64(ushort[] registers, int offset, ByteOrder byteOrder)
    {
        var bytes = GetBytes(registers, offset, 4, byteOrder);
        return BitConverter.ToDouble(bytes, 0);
    }

    private static string GetString(ushort[] registers, int offset, int count)
    {
        var bytes = new byte[count * 2];
        for (int i = 0; i < count; i++)
        {
            bytes[i * 2] = (byte)(registers[offset + i] >> 8);
            bytes[i * 2 + 1] = (byte)(registers[offset + i] & 0xFF);
        }
        return Encoding.ASCII.GetString(bytes).TrimEnd('\0');
    }

    #endregion

    #region 写入方法

    private static ushort[] SetUInt16(ushort value, ByteOrder byteOrder)
    {
        var result = byteOrder switch
        {
            ByteOrder.BigEndian => value,
            ByteOrder.LittleEndian => SwapBytes(value),
            ByteOrder.BigEndianByteSwap => SwapBytes(value),
            ByteOrder.LittleEndianByteSwap => value,
            _ => value
        };
        return [result];
    }

    private static ushort[] SetInt16(short value, ByteOrder byteOrder)
    {
        return SetUInt16((ushort)value, byteOrder);
    }

    private static ushort[] SetUInt32(uint value, ByteOrder byteOrder)
    {
        var bytes = BitConverter.GetBytes(value);
        return SetBytes(bytes, 2, byteOrder);
    }

    private static ushort[] SetInt32(int value, ByteOrder byteOrder)
    {
        var bytes = BitConverter.GetBytes(value);
        return SetBytes(bytes, 2, byteOrder);
    }

    private static ushort[] SetFloat32(float value, ByteOrder byteOrder)
    {
        var bytes = BitConverter.GetBytes(value);
        return SetBytes(bytes, 2, byteOrder);
    }

    private static ushort[] SetUInt64(ulong value, ByteOrder byteOrder)
    {
        var bytes = BitConverter.GetBytes(value);
        return SetBytes(bytes, 4, byteOrder);
    }

    private static ushort[] SetInt64(long value, ByteOrder byteOrder)
    {
        var bytes = BitConverter.GetBytes(value);
        return SetBytes(bytes, 4, byteOrder);
    }

    private static ushort[] SetFloat64(double value, ByteOrder byteOrder)
    {
        var bytes = BitConverter.GetBytes(value);
        return SetBytes(bytes, 4, byteOrder);
    }

    private static ushort[] SetString(string value, int registerCount)
    {
        var bytes = Encoding.ASCII.GetBytes(value);
        var count = registerCount > 0 ? registerCount : (bytes.Length + 1) / 2;
        var registers = new ushort[count];

        for (int i = 0; i < count; i++)
        {
            var highByte = i * 2 < bytes.Length ? bytes[i * 2] : (byte)0;
            var lowByte = i * 2 + 1 < bytes.Length ? bytes[i * 2 + 1] : (byte)0;
            registers[i] = (ushort)((highByte << 8) | lowByte);
        }

        return registers;
    }

    #endregion

    #region 辅助方法

    private static ushort SwapBytes(ushort value)
    {
        return (ushort)((value >> 8) | (value << 8));
    }

    private static byte[] GetBytes(ushort[] registers, int offset, int registerCount, ByteOrder byteOrder)
    {
        var bytes = new byte[registerCount * 2];

        // 先按大端模式提取字节 (每个寄存器高字节在前)
        for (int i = 0; i < registerCount; i++)
        {
            bytes[i * 2] = (byte)(registers[offset + i] >> 8);
            bytes[i * 2 + 1] = (byte)(registers[offset + i] & 0xFF);
        }

        // 根据输入数据的字节顺序，转换为 BitConverter 需要的 Little-Endian 格式
        // 输入格式 -> 需要的转换 -> BitConverter 期望的 DCBA
        return byteOrder switch
        {
            ByteOrder.BigEndian => ReverseArray(bytes),              // ABCD -> DCBA
            ByteOrder.LittleEndian => bytes,                          // DCBA -> DCBA (已正确)
            ByteOrder.BigEndianByteSwap => SwapWordBytesAndReverse(bytes), // BADC -> DCBA
            ByteOrder.LittleEndianByteSwap => SwapWordBytes(bytes),   // CDAB -> DCBA
            _ => bytes
        };
    }

    private static ushort[] SetBytes(byte[] bytes, int registerCount, ByteOrder byteOrder)
    {
        // BitConverter 产生的是 Little-Endian (DCBA)，需要转换为目标字节顺序
        // DCBA -> 目标格式
        var orderedBytes = byteOrder switch
        {
            ByteOrder.BigEndian => ReverseArray(bytes),              // DCBA -> ABCD
            ByteOrder.LittleEndian => bytes,                          // DCBA -> DCBA
            ByteOrder.BigEndianByteSwap => SwapWordBytesAndReverse(bytes), // DCBA -> BADC
            ByteOrder.LittleEndianByteSwap => SwapWordBytes(bytes),   // DCBA -> CDAB
            _ => bytes
        };

        var registers = new ushort[registerCount];
        for (int i = 0; i < registerCount; i++)
        {
            registers[i] = (ushort)((orderedBytes[i * 2] << 8) | orderedBytes[i * 2 + 1]);
        }

        return registers;
    }

    private static byte[] ReverseArray(byte[] bytes)
    {
        var result = new byte[bytes.Length];
        for (int i = 0; i < bytes.Length; i++)
        {
            result[i] = bytes[bytes.Length - 1 - i];
        }
        return result;
    }

    private static byte[] SwapWordBytes(byte[] bytes)
    {
        var result = new byte[bytes.Length];
        for (int i = 0; i < bytes.Length; i += 2)
        {
            result[i] = bytes[i + 1];
            result[i + 1] = bytes[i];
        }
        return result;
    }

    private static byte[] SwapWordBytesAndReverse(byte[] bytes)
    {
        var swapped = SwapWordBytes(bytes);
        return ReverseArray(swapped);
    }

    #endregion

    #region 位操作

    /// <summary>
    /// 从寄存器值中获取指定位
    /// </summary>
    /// <param name="value">寄存器值</param>
    /// <param name="bitIndex">位索引(0-15)</param>
    /// <returns>位值</returns>
    public static bool GetBit(ushort value, int bitIndex)
    {
        if (bitIndex < 0 || bitIndex > 15)
            throw new ArgumentOutOfRangeException(nameof(bitIndex), "位索引必须在0-15之间");

        return ((value >> bitIndex) & 1) == 1;
    }

    /// <summary>
    /// 设置寄存器值中的指定位
    /// </summary>
    /// <param name="value">原始寄存器值</param>
    /// <param name="bitIndex">位索引(0-15)</param>
    /// <param name="bitValue">位值</param>
    /// <returns>新的寄存器值</returns>
    public static ushort SetBit(ushort value, int bitIndex, bool bitValue)
    {
        if (bitIndex < 0 || bitIndex > 15)
            throw new ArgumentOutOfRangeException(nameof(bitIndex), "位索引必须在0-15之间");

        if (bitValue)
            return (ushort)(value | (1 << bitIndex));
        else
            return (ushort)(value & ~(1 << bitIndex));
    }

    #endregion
}
