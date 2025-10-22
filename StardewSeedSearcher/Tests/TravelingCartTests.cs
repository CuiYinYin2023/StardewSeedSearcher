using System;
using StardewSeedSearcher.Features;

namespace StardewSeedSearcher.Tests
{
    public static class TravelingCartTests
    {
        public static void Run()
        {
            Console.WriteLine("=== 猪车预测测试 ===\n");
            
            int testSeed = 10000;
            bool useLegacyRandom = true;  // 1.6+ 用 false
            
            var predictor = new TravelingCartPredictor();
            
            Console.WriteLine($"种子: {testSeed}");
            Console.WriteLine($"Legacy Random: {useLegacyRandom}\n");
            
            var results = predictor.PredictSpring(testSeed, useLegacyRandom);
            
            foreach (var dayResult in results)
            {
                Console.WriteLine($"=== {dayResult.DayName} ===");
                
                foreach (var item in dayResult.Items)
                {
                    if (item.Quantity > 0)
                    {
                        Console.WriteLine($"  {item.Category}: {item.Name}, 数量{item.Quantity}, 价格{item.Price}g");
                    }
                    else if (item.Quantity == -1)
                    {
                        Console.WriteLine($"  {item.Category}: {item.Name}, 数量不限, 价格{item.Price}g");
                    }
                    else
                    {
                        Console.WriteLine($"  {item.Category}: {item.Name}");
                    }
                }
                
                Console.WriteLine();
            }
        }
    }
}