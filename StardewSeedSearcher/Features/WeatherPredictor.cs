using System;
using System.Collections.Generic;
using System.Linq;
using StardewSeedSearcher.Core;

namespace StardewSeedSearcher.Features
{
    /// <summary>
    /// 季节枚举
    /// </summary>
    public enum Season
    {
        Spring = 0,
        Summer = 1,
        Fall = 2
    }

    /// <summary>
    /// 天气筛选条件
    /// </summary>
    public class WeatherCondition
    {
        public Season Season { get; set; }
        public int StartDay { get; set; }
        public int EndDay { get; set; }
        public int MinRainDays { get; set; }

        public int AbsoluteStartDay => (int)Season * 28 + StartDay;
        public int AbsoluteEndDay => (int)Season * 28 + EndDay;

        public override string ToString()
        {
            string seasonName = Season switch
            {
                Season.Spring => "春",
                Season.Summer => "夏",
                Season.Fall => "秋",
                _ => "?"
            };
            return $"{seasonName}{StartDay}-{seasonName}{EndDay}: 最少{MinRainDays}个雨天";
        }
    }

    /// <summary>
    /// 天气预测功能
    /// </summary>
    public class WeatherPredictor : ISearchFeature
    {
        public List<WeatherCondition> Conditions { get; set; } = new List<WeatherCondition>();
        public string Name => "天气预测";
        public bool IsEnabled { get; set; } = true;

        /// <summary>
        /// 检查种子是否符合筛选条件
        /// </summary>
        public bool Check(int gameID, bool useLegacyRandom)
        {
            // 如果没有条件，视为不筛选（全部通过）
            if (Conditions.Count == 0)
                return true;

            // 预测第一年春夏秋所有天气（1-84天）
            var allWeather = PredictWeather(gameID, useLegacyRandom);

            // 检查每个条件
            foreach (var condition in Conditions)
            {
                int rainCount = CountRainInRange(allWeather, condition);
                if (rainCount < condition.MinRainDays)
                    return false;
            }

            return true;
        }

        /// <summary>
        /// 获取配置说明
        /// </summary>
        public string GetConfigDescription()
        {
            if (Conditions.Count == 0)
                return "无筛选条件";

            return string.Join("; ", Conditions.Select(c => c.ToString()));
        }

        /// <summary>
        /// 统计指定范围内的雨天数量
        /// </summary>
        private int CountRainInRange(Dictionary<int, bool> weather, WeatherCondition condition)
        {
            int count = 0;
            for (int day = condition.AbsoluteStartDay; day <= condition.AbsoluteEndDay; day++)
            {
                if (weather.ContainsKey(day) && weather[day])
                    count++;
            }
            return count;
        }

        /// <summary>
        /// 预测第一年春夏秋所有天气（1-84天）
        /// </summary>
        public Dictionary<int, bool> PredictWeather(int gameID, bool useLegacyRandom)
        {
            var weather = new Dictionary<int, bool>();
            
            // 预先计算绿雨日期
            int year = 1;
            int greenRainSeed = HashHelper.GetRandomSeed(year * 777, gameID, 0, 0, 0, useLegacyRandom);
            Random greenRainRng = new Random(greenRainSeed);
            int[] greenRainDays = { 5, 6, 7, 14, 15, 16, 18, 23 };
            int greenRainDay = greenRainDays[greenRainRng.Next(greenRainDays.Length)];

            for (int absoluteDay = 1; absoluteDay <= 84; absoluteDay++)
            {
                int season = (absoluteDay - 1) / 28;       // 0=春, 1=夏, 2=秋
                int dayOfMonth = ((absoluteDay - 1) % 28) + 1;

                bool isRain = IsRainyDay(season, dayOfMonth, absoluteDay, gameID, useLegacyRandom, greenRainDay);
                weather[absoluteDay] = isRain;
            }

            return weather;
        }
        /// <summary>
        /// 判断某一天是否下雨
        /// </summary>
        private bool IsRainyDay(int season, int dayOfMonth, int absoluteDay, int gameID, bool useLegacyRandom, int greenRainDay)
        {
            // 固定天气规则
            if (dayOfMonth == 1)
            {
                return false; // 季节第一天强制晴天
            }
            // 春季 (season 0)
            if (season == 0)
            {
                if (dayOfMonth == 2 || dayOfMonth == 4)
                    return false; // 晴天
                if (dayOfMonth == 3)
                    return true; // 雨天
                if (dayOfMonth == 13 || dayOfMonth == 24)
                    return false; // 节日固定晴天
            }
            
            // 夏季 (season 1)
            else if (season == 1)
            {
                if (dayOfMonth == greenRainDay)  // 绿雨直接用传入的参数
                    return true;
                if (dayOfMonth == 11 || dayOfMonth == 28)
                    return false; // 节日固定晴天
                if (dayOfMonth % 13 == 0) // 第13、26天
                    return true; // 雷暴（算雨天）
                
                // 普通雨天：概率随日期递增
                int rainSeed = HashHelper.GetRandomSeed(absoluteDay - 1, gameID / 2, HashHelper.GetHashFromString("summer_rain_chance"), 0, 0, useLegacyRandom);
                Random rainRng = new Random(rainSeed);
                double rainChance = 0.12 + 0.003 * (dayOfMonth - 1);
                return rainRng.NextDouble() < rainChance;
            }
            
            // 秋季 (season 2)
            else if (season == 2)
            {
                if (dayOfMonth == 16 || dayOfMonth == 27)
                    return false; // 节日固定晴天
            }

            // 春季和秋季的普通日期：18.3% 概率
            int locationHash = HashHelper.GetHashFromString("location_weather");
            int seed = HashHelper.GetRandomSeed(locationHash, gameID, absoluteDay - 1, 0, 0, useLegacyRandom);
            Random rng = new Random(seed);
            return rng.NextDouble() < 0.183;
        }

        /// <summary>
        /// 预测天气并返回详细信息（用于前端展示）
        /// </summary>
        public (Dictionary<int, bool> weather, int greenRainDay) PredictWeatherWithDetail(int gameID, bool useLegacyRandom)
        {
            var weather = new Dictionary<int, bool>();
            
            // 计算绿雨日期
            int year = 1;
            int greenRainSeed = HashHelper.GetRandomSeed(year * 777, gameID, 0, 0, 0, useLegacyRandom);
            Random greenRainRng = new Random(greenRainSeed);
            int[] greenRainDays = { 5, 6, 7, 14, 15, 16, 18, 23 };
            int greenRainDay = greenRainDays[greenRainRng.Next(greenRainDays.Length)];

            for (int absoluteDay = 1; absoluteDay <= 84; absoluteDay++)
            {
                int season = (absoluteDay - 1) / 28;
                int dayOfMonth = ((absoluteDay - 1) % 28) + 1;

                bool isRain = IsRainyDay(season, dayOfMonth, absoluteDay, gameID, useLegacyRandom, greenRainDay);
                weather[absoluteDay] = isRain;
            }

            return (weather, greenRainDay);
        }

        /// <summary>
        /// 从天气字典提取雨天列表和详细信息
        /// </summary>
        public static WeatherDetailResult ExtractWeatherDetail(Dictionary<int, bool> weather, int greenRainDay)
        {
            var result = new WeatherDetailResult { GreenRainDay = greenRainDay };
            
            for (int day = 1; day <= 28; day++)
            {
                if (weather[day]) result.SpringRain.Add(day);
                if (weather[day + 28]) result.SummerRain.Add(day);
                if (weather[day + 56]) result.FallRain.Add(day);
            }
            
            return result;
        }
    }

    /// <summary>
    /// 天气详情结果
    /// </summary>
    public class WeatherDetailResult
    {
        public List<int> SpringRain { get; set; } = new List<int>();
        public List<int> SummerRain { get; set; } = new List<int>();
        public List<int> FallRain { get; set; } = new List<int>();
        public int GreenRainDay { get; set; }
    }
}