// ============================================================
// 文件名: CanDataParser.cs
// 用途: CAN 总线数据解析工具，解析原始 CAN 数据帧为物理量
// ============================================================

using System;
using System.Collections.Generic;

namespace ChargeDischargeSystem.Common.Helpers
{
    /// <summary>
    /// 电池协议中单个信号的定义描述
    /// </summary>
    public class SignalDefinition
    {
        /// <summary>
        /// 获取或设置信号名称，如 voltage、current、soc 等
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// 获取或设置起始位索引(0~63)，CAN 数据帧中的位偏移
        /// </summary>
        public int StartBit { get; set; }

        /// <summary>
        /// 获取或设置信号长度(位)，占用多少位
        /// </summary>
        public int Length { get; set; }

        /// <summary>
        /// 获取或设置缩放系数，原始值乘以系数得到物理值
        /// </summary>
        public double Scale { get; set; }

        /// <summary>
        /// 获取或设置偏移量，缩放后的值加上偏移得到最终值
        /// </summary>
        public double Offset { get; set; }

        /// <summary>
        /// 获取或设置是否带符号，true 表示有符号整数
        /// </summary>
        public bool IsSigned { get; set; }

        /// <summary>
        /// 获取或设置测量单位，如 V、A、kW、°C、% 等
        /// </summary>
        public string Unit { get; set; }
    }

    /// <summary>
    /// CAN 总线数据解析工具，提供从原始 CAN 帧数据中按信号定义提取物理量的功能
    /// </summary>
    public static class CanDataParser
    {
        /// <summary>
        /// 从 CAN 数据帧的字节数组中提取指定位段的整数值
        /// 支持跨字节边界的信号提取
        /// </summary>
        /// <param name="data">CAN 数据帧(8字节数组)</param>
        /// <param name="startBit">信号起始位(0~63)，最高位为位0</param>
        /// <param name="length">信号长度(位)</param>
        /// <param name="isSigned">是否为有符号数</param>
        /// <returns>提取的整数值</returns>
        /// <exception cref="ArgumentException">参数超出范围时抛出</exception>
        public static long ExtractRawValue(byte[] data, int startBit, int length, bool isSigned)
        {
            // 检查数据数组是否为空
            if (data == null || data.Length == 0)
            {
                // 数据无效时抛出异常
                throw new ArgumentException("CAN 数据帧不能为空", nameof(data));
            }

            // 检查信号长度是否在有效范围内(1~64位)
            if (length <= 0 || length > 64)
            {
                // 长度无效时抛出异常
                throw new ArgumentException("信号长度必须在1~64之间", nameof(length));
            }

            // 计算信号占用的字节范围(起始字节~结束字节)
            int startByte = startBit / 8;
            int endByte = (startBit + length - 1) / 8;

            // 检查计算的字节范围是否超出数据数组长度
            if (endByte >= data.Length)
            {
                // 信号定义超出数据长度时抛出异常
                throw new ArgumentException($"信号定义超出数据范围: 需要 {endByte + 1} 字节，实际 {data.Length} 字节");
            }

            // 初始化结果累加器
            long rawValue = 0;

            // 按位遍历信号区间，逐位提取
            for (int i = 0; i < length; i++)
            {
                // 计算当前位在 CAN 帧中的全局位索引
                int currentBit = startBit + i;

                // 计算当前位所在的字节索引(CAN大端模式)
                int byteIndex = currentBit / 8;

                // 计算当前位在字节内的位偏移
                int bitOffset = 7 - (currentBit % 8);

                // 获取指定字节中的指定位
                byte bitValue = (byte)((data[byteIndex] >> bitOffset) & 1);

                // 将提取的位移入结果累加器的正确位置
                rawValue |= (long)bitValue << i;
            }

            // 处理有符号数的符号扩展
            if (isSigned)
            {
                // 检查最高位(符号位)是否为1
                if ((rawValue & (1L << (length - 1))) != 0)
                {
                    // 符号位为1时进行符号扩展，高位补1
                    for (int i = length; i < 64; i++)
                    {
                        // 将高位全部置1，保持负数补码表示
                        rawValue |= (1L << i);
                    }
                }
            }

            // 返回提取的原始整数值
            return rawValue;
        }

        /// <summary>
        /// 根据信号定义从 CAN 数据帧中提取并转换为物理量
        /// </summary>
        /// <param name="data">CAN 数据帧(8字节数组)</param>
        /// <param name="signal">信号定义，包含起始位、长度、缩放、偏移</param>
        /// <returns>物理量值(原始值 * 缩放 + 偏移)</returns>
        public static double ExtractPhysicalValue(byte[] data, SignalDefinition signal)
        {
            // 检查信号定义是否为空
            if (signal == null)
            {
                // 信号定义为空时抛出异常
                throw new ArgumentNullException(nameof(signal), "信号定义不能为空");
            }

            // 从数据帧中提取原始整数值
            long rawValue = ExtractRawValue(data, signal.StartBit, signal.Length, signal.IsSigned);

            // 将原始值乘以缩放系数并加上偏移量，得到物理量值
            double physicalValue = rawValue * signal.Scale + signal.Offset;

            // 返回转换后的物理量
            return physicalValue;
        }

        /// <summary>
        /// 批量解析，根据多个信号定义从单个 CAN 数据帧中提取所有物理量
        /// </summary>
        /// <param name="data">CAN 数据帧(8字节数组)</param>
        /// <param name="signals">信号定义集合</param>
        /// <returns>信号名称到物理量的映射字典</returns>
        public static Dictionary<string, double> ParseAllSignals(byte[] data, IEnumerable<SignalDefinition> signals)
        {
            // 检查信号集合是否为空
            if (signals == null)
            {
                // 信号集合为空时抛出异常
                throw new ArgumentNullException(nameof(signals), "信号定义集合不能为空");
            }

            // 创建结果字典，用于存储信号名到物理量的映射
            Dictionary<string, double> result = new Dictionary<string, double>();

            // 遍历所有信号定义进行批量解析
            foreach (SignalDefinition signal in signals)
            {
                // 检查信号名称是否为空
                if (string.IsNullOrEmpty(signal.Name))
                {
                    // 跳过名称为空的信号定义
                    continue;
                }

                // 根据信号定义解析物理量值
                double value = ExtractPhysicalValue(data, signal);

                // 将解析结果存入字典
                result[signal.Name] = value;
            }

            // 返回所有信号的解析结果
            return result;
        }

        /// <summary>
        /// 将物理量值编码回 CAN 数据帧的字节数组中
        /// 用于向设备发送控制指令或设定参数
        /// </summary>
        /// <param name="data">目标 CAN 数据帧(8字节数组)，会被修改</param>
        /// <param name="signal">信号定义</param>
        /// <param name="physicalValue">物理量值</param>
        public static void EncodePhysicalValue(byte[] data, SignalDefinition signal, double physicalValue)
        {
            // 检查数据数组是否为空
            if (data == null || data.Length == 0)
            {
                // 数据无效时抛出异常
                throw new ArgumentException("CAN 数据帧不能为空", nameof(data));
            }

            // 检查信号定义是否为空
            if (signal == null)
            {
                // 信号定义为空时抛出异常
                throw new ArgumentNullException(nameof(signal), "信号定义不能为空");
            }

            // 将物理量值减去偏移并除以缩放系数，还原为原始整数值
            long rawValue = (long)Math.Round((physicalValue - signal.Offset) / signal.Scale);

            // 遍历信号占用的每一位进行编码
            for (int i = 0; i < signal.Length; i++)
            {
                // 计算当前位在 CAN 帧中的全局位索引
                int currentBit = signal.StartBit + i;

                // 计算当前位所在的字节索引
                int byteIndex = currentBit / 8;

                // 计算当前位在字节内的位偏移(CAN大端模式)
                int bitOffset = 7 - (currentBit % 8);

                // 检查字节索引是否超出数据数组范围
                if (byteIndex >= data.Length)
                {
                    // 越界时停止编码
                    break;
                }

                // 提取原始值中第 i 位的值(0或1)
                byte bitValue = (byte)((rawValue >> i) & 1);

                // 将提取的位写入目标字节的对应位置
                if (bitValue == 1)
                {
                    // 位值为1时置位
                    data[byteIndex] |= (byte)(1 << bitOffset);
                }
                else
                {
                    // 位值为0时清除
                    data[byteIndex] &= (byte)~(1 << bitOffset);
                }
            }
        }

        /// <summary>
        /// 将 8 字节的 CAN 数据帧转换为十六进制字符串显示
        /// 用于日志记录和调试
        /// </summary>
        /// <param name="data">CAN 数据帧字节数组</param>
        /// <returns>十六进制字符串，格式如 "01 02 A3 B4 C5 D6 E7 F8"</returns>
        public static string DataToHexString(byte[] data)
        {
            // 检查数据是否为空
            if (data == null || data.Length == 0)
            {
                // 空数据返回空字符串
                return string.Empty;
            }

            // 使用 BitConverter 将字节数组转换为带连字符的大写十六进制格式
            string hex = BitConverter.ToString(data);

            // 用空格替换连字符，输出更直观的格式
            return hex.Replace('-', ' ');
        }

        /// <summary>
        /// 将十六进制字符串解析回 CAN 数据帧字节数组
        /// 用于解析日志或配置文件中的十六进制数据
        /// </summary>
        /// <param name="hexString">十六进制字符串，支持空格或连字符分隔</param>
        /// <returns>解析后的字节数组</returns>
        /// <exception cref="ArgumentException">字符串格式无效时抛出</exception>
        public static byte[] HexStringToData(string hexString)
        {
            // 检查十六进制字符串是否为空
            if (string.IsNullOrWhiteSpace(hexString))
            {
                // 空字符串返回空数组
                return Array.Empty<byte>();
            }

            // 移除字符串中所有的空格和连字符分隔符
            string cleanHex = hexString.Replace(" ", "").Replace("-", "");

            // 检查清理后的十六进制字符串长度是否为偶数
            if (cleanHex.Length % 2 != 0)
            {
                // 奇数长度说明格式无效
                throw new ArgumentException("十六进制字符串长度无效，必须为偶数", nameof(hexString));
            }

            // 创建结果字节数组，长度是十六进制字符串的一半
            byte[] data = new byte[cleanHex.Length / 2];

            // 每两个字符转换一个字节
            for (int i = 0; i < data.Length; i++)
            {
                // 将两位十六进制字符转换为字节值
                data[i] = Convert.ToByte(cleanHex.Substring(i * 2, 2), 16);
            }

            // 返回解析后的字节数组
            return data;
        }
    }
}
