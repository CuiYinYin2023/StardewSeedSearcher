using System;
using StardewSeedSearcher.Features;

namespace StardewSeedSearcher.Tests
{
    /// <summary>
    /// 怪物层预测功能测试
    /// </summary>
    public static class MonsterLevelTests
    {
        public static void Run()
        {
            Console.WriteLine("=== 矿井怪物层预测测试 ===\n");

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

            // 预测怪物层
            var predictor = new MonsterLevelPredictor();
            var results = predictor.PredictMonsterLevels(gameID, useLegacyRandom);

            // 输出结果
            Console.WriteLine($"\n种子：{gameID}");
            Console.WriteLine($"随机模式：{(useLegacyRandom ? "旧随机" : "新随机")}\n");
            Console.WriteLine("春季前5天的怪物层预测：");
            Console.WriteLine(new string('=', 50));

            foreach (var kvp in results)
            {
                int day = kvp.Key;
                var info = kvp.Value;

                Console.WriteLine($"\n春{day}:");
                
                if (info.MonsterLevels.Count > 0)
                {
                    Console.WriteLine($"  怪物层：{string.Join(", ", info.MonsterLevels)}");
                }
                
                if (info.SlimeLevels.Count > 0)
                {
                    Console.WriteLine($"  史莱姆层：{string.Join(", ", info.SlimeLevels)}");
                }
                
                if (info.MonsterLevels.Count == 0 && info.SlimeLevels.Count == 0)
                {
                    Console.WriteLine("  无感染层");
                }
            }

            Console.WriteLine();
        }
    }
}