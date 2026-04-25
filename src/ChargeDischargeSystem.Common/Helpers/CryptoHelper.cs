// ============================================================
// 文件名: CryptoHelper.cs
// 用途: 加密与哈希工具类，提供密码哈希(SHA-256+SALT)、密码验证、UUID生成等安全功能
// ============================================================

using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace ChargeDischargeSystem.Common.Helpers
{
    /// <summary>
    /// 加密与哈希工具类，用于用户密码的安全存储和验证
    /// </summary>
    public static class CryptoHelper
    {
        // PBKDF2 哈希迭代次数，增加暴力破解难度
        private const int Iterations = 10000;

        // 随机盐值长度(字节)，32字节提供256位熵
        private const int SaltSize = 32;

        // 哈希输出长度(字节)，256位SHA-256输出
        private const int HashSize = 32;

        /// <summary>
        /// 使用 SHA-256 和随机盐值对密码进行哈希
        /// 采用 PBKDF2 算法迭代 10000 次，有效抵御彩虹表和暴力攻击
        /// </summary>
        /// <param name="password">明文密码</param>
        /// <returns>包含 Base64 编码的哈希值和盐值的元组</returns>
        /// <exception cref="ArgumentException">密码为空时抛出</exception>
        public static (string Hash, string Salt) HashPassword(string password)
        {
            // 检查密码是否为空
            if (string.IsNullOrEmpty(password))
            {
                // 密码为空时抛出参数异常
                throw new ArgumentException("密码不能为空", nameof(password));
            }

            // 使用安全随机数生成器生成指定长度的随机盐值
            byte[] salt = RandomNumberGenerator.GetBytes(SaltSize);

            // 使用 PBKDF2 算法对密码加盐哈希，迭代指定次数
            byte[] hash = Rfc2898DeriveBytes.Pbkdf2(
                Encoding.UTF8.GetBytes(password),
                salt,
                Iterations,
                HashAlgorithmName.SHA256,
                HashSize
            );

            // 返回 Base64 编码的哈希值和盐值
            return (Convert.ToBase64String(hash), Convert.ToBase64String(salt));
        }

        /// <summary>
        /// 验证密码是否匹配存储的哈希值
        /// 使用常量时间比较防止时序攻击
        /// </summary>
        /// <param name="password">待验证的明文密码</param>
        /// <param name="storedHash">数据库中存储的哈希值(Base64)</param>
        /// <param name="storedSalt">数据库中存储的盐值(Base64)</param>
        /// <returns>密码是否匹配</returns>
        public static bool VerifyPassword(string password, string storedHash, string storedSalt)
        {
            // 检查输入参数是否有空值
            if (string.IsNullOrEmpty(password) || string.IsNullOrEmpty(storedHash) || string.IsNullOrEmpty(storedSalt))
            {
                // 任一参数为空时返回验证失败
                return false;
            }

            try
            {
                // 将 Base64 编码的盐值解码为字节数组
                byte[] salt = Convert.FromBase64String(storedSalt);

                // 使用相同的算法和参数重新计算密码哈希
                byte[] hash = Rfc2898DeriveBytes.Pbkdf2(
                    Encoding.UTF8.GetBytes(password),
                    salt,
                    Iterations,
                    HashAlgorithmName.SHA256,
                    HashSize
                );

                // 将 Base64 编码的存储哈希值解码为字节数组
                byte[] expectedHash = Convert.FromBase64String(storedHash);

                // 使用常量时间比较防止时序攻击
                return CryptographicOperations.FixedTimeEquals(hash, expectedHash);
            }
            catch
            {
                // 任何异常情况均返回验证失败
                return false;
            }
        }

        /// <summary>
        /// 生成 UUID 格式的唯一标识符，用于数据库记录ID、会话ID等场景
        /// </summary>
        /// <returns>32位大写十六进制 UUID 字符串</returns>
        public static string GenerateUuid()
        {
            // 生成新的 GUID 并转换为无连字符的大写字符串
            return Guid.NewGuid().ToString("N").ToUpper();
        }

        /// <summary>
        /// 计算文件的 SHA-256 校验和，用于固件升级后的完整性验证
        /// </summary>
        /// <param name="filePath">文件路径</param>
        /// <returns>十六进制格式的 SHA-256 哈希值，文件不存在时返回空字符串</returns>
        public static string ComputeFileHash(string filePath)
        {
            // 检查文件是否存在
            if (!File.Exists(filePath))
            {
                // 文件不存在时返回空字符串
                return string.Empty;
            }

            // 创建 SHA-256 计算实例
            using SHA256 sha256 = SHA256.Create();

            // 以只读方式打开文件流
            using FileStream stream = File.OpenRead(filePath);

            // 计算文件流的哈希值
            byte[] hash = sha256.ComputeHash(stream);

            // 将哈希值转换为小写十六进制字符串
            return Convert.ToHexString(hash).ToLower();
        }

        /// <summary>
        /// 验证密码复杂度
        /// 要求: 长度 >= 8，包含大写字母、小写字母、数字和特殊字符
        /// </summary>
        /// <param name="password">待验证的密码</param>
        /// <returns>包含验证结果和错误消息的元组</returns>
        public static (bool IsValid, string ErrorMessage) ValidatePasswordComplexity(string password)
        {
            // 检查密码是否为空或长度不足8位
            if (string.IsNullOrEmpty(password) || password.Length < 8)
            {
                // 长度不足时返回验证失败
                return (false, "密码长度不能少于8个字符");
            }

            // 初始化各类字符存在标志
            bool hasUpper = false;
            bool hasLower = false;
            bool hasDigit = false;
            bool hasSpecial = false;

            // 遍历密码中的每个字符进行分类检测
            foreach (char c in password)
            {
                // 检测是否为大写字母
                if (char.IsUpper(c))
                {
                    hasUpper = true;
                }
                else if (char.IsLower(c))
                {
                    // 检测是否为小写字母
                    hasLower = true;
                }
                else if (char.IsDigit(c))
                {
                    // 检测是否为数字
                    hasDigit = true;
                }
                else
                {
                    // 其他字符归类为特殊字符
                    hasSpecial = true;
                }
            }

            // 验证是否包含大写字母
            if (!hasUpper)
            {
                // 缺少大写字母时返回验证失败
                return (false, "密码必须包含大写字母");
            }

            // 验证是否包含小写字母
            if (!hasLower)
            {
                // 缺少小写字母时返回验证失败
                return (false, "密码必须包含小写字母");
            }

            // 验证是否包含数字
            if (!hasDigit)
            {
                // 缺少数字时返回验证失败
                return (false, "密码必须包含数字");
            }

            // 验证是否包含特殊字符
            if (!hasSpecial)
            {
                // 缺少特殊字符时返回验证失败
                return (false, "密码必须包含特殊字符");
            }

            // 所有验证通过，返回成功
            return (true, "密码符合要求");
        }
    }
}
