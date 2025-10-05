using System;
using StardewSeedSearcher.Core;
using StardewSeedSearcher.Data;

namespace StardewSeedSearcher.Tests
{
    /// <summary>
    /// 矿井宝箱预测功能测试
    /// </summary>
    public static class MineChestTests
    {
        public static void Run()
        {
            Console.WriteLine("=== 矿井宝箱预测测试 ===\n");

            // 输入种子
            Console.Write("请输入游戏种子：");
            string input = Console.ReadLine();
            
            if (!int.TryParse(input, out int gameID))
            {
                Console.WriteLine("无效的种子，请输入整数");
                return;
            }

            // 选择随机模式
            Console.Write("使用旧随机模式？(y/n，默认 n)：");
            string modeInput = Console.ReadLine()?.Trim().ToLower();
            bool useLegacyRandom = modeInput == "y" || modeInput == "yes";

            // 输出标题
            Console.WriteLine($"\n种子：{gameID}");
            Console.WriteLine($"随机模式：{(useLegacyRandom ? "旧随机" : "新随机")}\n");

            if (useLegacyRandom)
            {
                PredictChests_Legacy(gameID);
            }
            else
            {
                PredictChests_Modern(gameID);
            }
        }

        /// <summary>
        /// 新随机模式预测
        /// </summary>
        private static void PredictChests_Modern(int gameID)
        {
            Console.WriteLine("楼层\t物品");
            Console.WriteLine("----\t----");

            foreach (int floor in MineChestData.ChestFloors)
            {
                // 新随机：HashHelper.GetRandomSeed(gameID * 512, floor, 0, 0, 0)
                int seed = HashHelper.GetRandomSeed(gameID * 512, floor, 0, 0, 0, useLegacyRandom: false);
                
                Random rng = new Random(seed);
                string[] items = MineChestData.ItemsCN[floor];
                int index = rng.Next(items.Length);
                string item = items[index];
                
                Console.WriteLine($"{floor}\t{item}");
            }
        }

        /// <summary>
        /// 旧随机模式预测 
        /// </summary>
        private static void PredictChests_Legacy(int gameID)
        {
            Console.WriteLine("楼层\t物品");
            Console.WriteLine("----\t----");

            foreach (int floor in MineChestData.ChestFloors)
            {
                // 使用 GetRandomSeed，内部会取模
                long temp = gameID * 512L + floor;
                int seed = HashHelper.GetRandomSeed((int)(temp % int.MaxValue), 0, 0, 0, 0, useLegacyRandom: true);
                
                Random rng = new Random(seed);
                string[] items = MineChestData.ItemsCN[floor];
                int index = rng.Next(items.Length);
                string item = items[index];
                
                Console.WriteLine($"{floor}\t{item}");
            }
        }
    }
}