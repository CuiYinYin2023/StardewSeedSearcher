using System;
using System.Collections.Generic;
using StardewSeedSearcher.Features;

namespace StardewSeedSearcher.Tests
{
    /// <summary>
    /// 沙漠节商人预测功能测试
    /// </summary>
    public static class DesertFestivalTests
    {
        public static void Run()
        {
            Console.WriteLine("=== 沙漠节商人预测测试 ===\n");

            // 输入种子
            Console.Write("请输入游戏种子: ");
            string input = Console.ReadLine();
            
            if (!int.TryParse(input, out int gameID))
            {
                Console.WriteLine("无效的种子，请输入整数");
                return;
            }

            // 选择随机模式
            Console.Write("使用旧随机模式? (y/n, 默认 n): ");
            string modeInput = Console.ReadLine()?.Trim().ToLower();
            bool useLegacyRandom = modeInput == "y" || modeInput == "yes";

            // 预测商人
            var predictor = new DesertFestivalPredictor();
            Dictionary<int, List<string>> vendors = predictor.PredictVendors(gameID, useLegacyRandom);

            // 输出结果
            Console.WriteLine($"\n种子: {gameID}");
            Console.WriteLine($"年份: 第1年");
            Console.WriteLine($"随机模式: {(useLegacyRandom ? "旧随机" : "新随机")}\n");

            string[] dayNames = { "春15", "春16", "春17" };
            for (int d = 0; d < 3; d++)
            {
                Console.WriteLine($"{dayNames[d]}: {string.Join(", ", vendors[d])}");
            }

            Console.WriteLine("\n按任意键退出...");
            Console.ReadKey();
        }
    }
}