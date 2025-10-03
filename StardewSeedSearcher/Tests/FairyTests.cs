using System;
using StardewSeedSearcher.Features;

namespace StardewSeedSearcher.Tests
{
    /// <summary>
    /// 仙子预测功能测试
    /// </summary>
    public static class FairyTests
    {
        public static void Run()
        {
            Console.WriteLine("=== 仙子预测测试 ===\n");

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

            // 预测仙子
            var predictor = new FairyPredictor();
            int? firstFairyDay = predictor.FindFirstFairy(gameID, useLegacyRandom);

            // 输出结果
            Console.WriteLine($"\n种子：{gameID}");
            Console.WriteLine($"随机模式：{(useLegacyRandom ? "旧随机" : "新随机")}\n");

            if (firstFairyDay.HasValue)
            {
                // 转换为年份、季节和日期
                int absoluteDay = firstFairyDay.Value;
                int year = (absoluteDay - 1) / 112 + 1;
                int dayInYear = (absoluteDay - 1) % 112 + 1;
                int season = (dayInYear - 1) / 28;
                int dayOfMonth = ((dayInYear - 1) % 28) + 1;
                
                string[] seasonNames = { "春", "夏", "秋", "冬" };
                Console.WriteLine($"仙子首次出现：第{year}年 {seasonNames[season]}{dayOfMonth}");
            }
            else
            {
                Console.WriteLine("搜索范围内没有出现仙子");
            }
        }
    }
}