using System.Data.HashFunction.xxHash;
using System.Text;

namespace StardewSeedSearcher.Core
{
    /// <summary>
    /// 哈希和随机种子计算辅助类
    /// 基于 Underscore76 的实现
    /// </summary>
    public static class HashHelper
    {
        /// <summary>XXHash 32位哈希函数</summary>
        private static readonly IxxHash Hasher = xxHashFactory.Instance.Create(new xxHashConfig { HashSizeInBits = 32 });

        /// <summary>
        /// 获取字符串的确定性哈希值
        /// </summary>
        public static int GetHashFromString(string value)
        {
            byte[] data = Encoding.UTF8.GetBytes(value);
            return GetHashFromBytes(data);
        }

        /// <summary>
        /// 获取整数数组的确定性哈希值
        /// </summary>
        public static int GetHashFromArray(params int[] values)
        {
            byte[] data = new byte[values.Length * 4];
            Buffer.BlockCopy(values, 0, data, 0, data.Length);
            return GetHashFromBytes(data);
        }

        /// <summary>
        /// 获取字节数组的确定性哈希值
        /// </summary>
        private static int GetHashFromBytes(byte[] data)
        {
            byte[] hash = Hasher.ComputeHash(data).Hash;
            return BitConverter.ToInt32(hash, 0);
        }

        /// <summary>
        /// 计算随机种子
        /// 模拟 StardewValley.Utility.CreateRandomSeed()
        /// </summary>
        /// <param name="useLegacyRandom">是否使用旧随机模式</param>
        public static int GetRandomSeed(int a, int b, int c, int d, int e, bool useLegacyRandom)
        {
            // 确保参数在有效范围内
            a %= 2147483647;
            b %= 2147483647;
            c %= 2147483647;
            d %= 2147483647;
            e %= 2147483647;

            if (useLegacyRandom)
            {
                // 旧随机：简单相加取模
                long sum = (long)a + b + c + d + e;
                return (int)(sum % 2147483647);
            }
            else
            {
                // 新随机：使用 XXHash
                return GetHashFromArray(a, b, c, d, e);
            }
        }
    }
}