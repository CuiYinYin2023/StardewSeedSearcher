using System.Data.HashFunction.xxHash;
using System.Runtime.CompilerServices;
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
                return GetHashFromFiveInts(a, b, c, d, e);
            }
        }

        // xxHash32 over exactly five little-endian Int32 values, with seed 0.
        // Equivalent to GetHashFromArray(a, b, c, d, e), without heap allocations.
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static int GetHashFromFiveInts(int a, int b, int c, int d, int e)
        {
            const uint prime1 = 2654435761U;
            const uint prime2 = 2246822519U;
            const uint prime3 = 3266489917U;
            const uint prime4 = 668265263U;

            static uint Round(uint accumulator, uint input)
            {
                accumulator += input * prime2;
                accumulator = RotateLeft(accumulator, 13);
                return accumulator * prime1;
            }

            unchecked
            {
                uint v1 = prime1 + prime2;
                uint v2 = prime2;
                uint v3 = 0;
                uint v4 = 0U - prime1;

                v1 = Round(v1, (uint)a);
                v2 = Round(v2, (uint)b);
                v3 = Round(v3, (uint)c);
                v4 = Round(v4, (uint)d);

                uint hash = RotateLeft(v1, 1)
                          + RotateLeft(v2, 7)
                          + RotateLeft(v3, 12)
                          + RotateLeft(v4, 18);

                hash += 20;
                hash += (uint)e * prime3;
                hash = RotateLeft(hash, 17) * prime4;

                hash ^= hash >> 15;
                hash *= prime2;
                hash ^= hash >> 13;
                hash *= prime3;
                hash ^= hash >> 16;

                return (int)hash;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static uint RotateLeft(uint value, int count) =>
            (value << count) | (value >> (32 - count));
    }
}
