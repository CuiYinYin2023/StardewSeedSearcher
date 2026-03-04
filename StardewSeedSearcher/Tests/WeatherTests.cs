using StardewSeedSearcher.Features;

namespace StardewSeedSearcher.Tests
{
    /// <summary>
    /// 天气预测功能测试
    /// </summary>
    public static class WeatherTests
    {
        public static void Run()
        {
            Console.WriteLine("=== 天气预测测试 ===\n");

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

            // 预测天气
            var predictor = new WeatherPredictor();
            var allWeather = predictor.PredictWeather(gameID, useLegacyRandom);

            // 分季节统计
            var springRain = GetRainDaysInSeason(allWeather, 0);
            var summerRain = GetRainDaysInSeason(allWeather, 1);
            var fallRain = GetRainDaysInSeason(allWeather, 2);

            // 输出结果
            Console.WriteLine($"\n种子：{gameID}");
            Console.WriteLine($"随机模式：{(useLegacyRandom ? "旧随机" : "新随机")}\n");

            PrintSeasonRain("春季", springRain);
            PrintSeasonRain("夏季", summerRain);
            PrintSeasonRain("秋季", fallRain);

            // 显示所有天气详情（可选）
            Console.Write("\n是否显示全部天气详情？(y/n)：");
            string detailInput = Console.ReadLine()?.Trim().ToLower();
            
            if (detailInput == "y" || detailInput == "yes")
            {
                ShowDetailedWeather(allWeather);
            }
        }

        /// <summary>
        /// 获取指定季节的雨天日期
        /// </summary>
        private static List<int> GetRainDaysInSeason(Dictionary<int, bool> weather, int season)
        {
            var rainyDays = new List<int>();
            int startDay = season * 28 + 1;
            int endDay = season * 28 + 28;

            for (int absoluteDay = startDay; absoluteDay <= endDay; absoluteDay++)
            {
                if (weather.ContainsKey(absoluteDay) && weather[absoluteDay])
                {
                    int dayOfMonth = ((absoluteDay - 1) % 28) + 1;
                    rainyDays.Add(dayOfMonth);
                }
            }

            return rainyDays;
        }

        /// <summary>
        /// 打印季节雨天信息
        /// </summary>
        private static void PrintSeasonRain(string seasonName, List<int> rainyDays)
        {
            if (rainyDays.Count > 0)
            {
                string rainyDaysStr = string.Join("，", rainyDays.Select(d => $"{seasonName.Substring(0, 1)}{d}"));
                Console.WriteLine($"{seasonName}雨天：{rainyDaysStr}");
            }
            else
            {
                Console.WriteLine($"{seasonName}雨天：无");
            }
            Console.WriteLine($"{seasonName}雨天总数：{rainyDays.Count}\n");
        }

        /// <summary>
        /// 显示所有天气详情
        /// </summary>
        private static void ShowDetailedWeather(Dictionary<int, bool> weather)
        {
            Console.WriteLine("\n=== 天气详情 ===");
            string[] seasons = { "春", "夏", "秋" };

            for (int season = 0; season < 3; season++)
            {
                Console.WriteLine($"\n{seasons[season]}季:");
                Console.WriteLine("日期\t天气");
                Console.WriteLine("-------------------");

                for (int dayOfMonth = 1; dayOfMonth <= 28; dayOfMonth++)
                {
                    int absoluteDay = season * 28 + dayOfMonth;
                    string weatherStr = weather[absoluteDay] ? "雨天" : "晴天";
                    Console.WriteLine($"{seasons[season]}{dayOfMonth}\t{weatherStr}");
                }
            }
        }
    }
}