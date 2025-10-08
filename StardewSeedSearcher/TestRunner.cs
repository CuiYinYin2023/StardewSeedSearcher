using System;
using StardewSeedSearcher.Tests;

namespace StardewSeedSearcher
{
    /// <summary>
    /// 测试程序入口
    /// </summary>
    class TestRunner
    {
        static void Main(string[] args)
        {
            while (true)
            {
                Console.Clear();
                Console.WriteLine("=================================");
                Console.WriteLine("  星露谷物语 - 功能测试");
                Console.WriteLine("=================================\n");
                Console.WriteLine("选择要测试的功能：");
                Console.WriteLine("1. 天气预测");
                Console.WriteLine("2. 仙子预测");
                Console.WriteLine("3. 混合宝箱预测");
                Console.WriteLine("4. 怪物层预测");
                Console.WriteLine("0. 退出\n");
                Console.Write("请输入选项：");

                string input = Console.ReadLine();

                switch (input)
                {
                    case "1":
                        Console.Clear();
                        WeatherTests.Run();
                        break;
                    case "2":
                        Console.Clear();
                        FairyTests.Run();
                        break;
                    case "3":
                        Console.Clear();
                        MineChestTests.Run();
                        break;
                    case "4":
                        Console.Clear();
                        MonsterLevelTests.Run();
                        break;
                    case "0":
                        Console.WriteLine("再见！");
                        return;
                    default:
                        Console.WriteLine("无效选项，请重新选择");
                        break;
                }

                Console.WriteLine("\n按任意键继续...");
                Console.ReadKey();
            }
        }
    }
}