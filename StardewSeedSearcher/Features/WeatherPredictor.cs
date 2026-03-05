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
        public int locationHash = HashHelper.GetHashFromString("location_weather");


        /// <summary>
        /// 检查种子是否符合筛选条件
        /// </summary>
        public bool Check(int gameID, bool useLegacyRandom)
        {
            if (Conditions.Count == 0)
                return true;
            
            // 只计算一次绿雨
            int greenRainDay = GetGreenRainDay(gameID, useLegacyRandom);

            // 逐条件检查，失败立即返回
            foreach (var condition in Conditions)
            {
                int rainCount = 0;
                
                // 每一天检查
                for (int day = condition.AbsoluteStartDay; day <= condition.AbsoluteEndDay; day++)
                {
                    int season = (int)condition.Season;
                    int dayOfMonth = ((day - 1) % 28) + 1;
                    
                    // 检查当天是否下雨
                    if (IsRainyDay(season, dayOfMonth, day, gameID, useLegacyRandom, greenRainDay))
                    {
                        // 下雨则增加计数
                        rainCount++;
                        
                        // 提前成功：已经满足最少雨天数，不用算后面的
                        if (rainCount >= condition.MinRainDays)
                            break;
                    }
                }
                
                // 这个条件不满足，直接返回 false
                if (rainCount < condition.MinRainDays)
                    return false;
            }

            return true;
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

            switch (season)
            {
                case 0: // 春季
                    return dayOfMonth switch // 第一年固定天气
                    {
                        2 => false, // 晴天
                        3 => true, // 雨天
                        4 => false, // 晴天
                        5 => false, // 晴天
                        13 => false, // 节日固定晴天
                        24 => false, // 节日固定晴天
                        _ => IsRainyDaySpringFall(gameID, absoluteDay, useLegacyRandom) // 预测
                    };

                case 1: // 夏季
                    return dayOfMonth == greenRainDay || dayOfMonth switch // 先判定绿雨
                    {
                        11 => false, // 节日固定晴天
                        13 => true, // 雷暴
                        28 => false, // 节日固定晴天
                        26 => true, // 雷暴
                        _ => IsRainyDaySummer(gameID, absoluteDay, useLegacyRandom, dayOfMonth) // 预测
                    };

                case 2: // 秋季
                    return dayOfMonth switch
                    {
                        16 => false, // 节日固定晴天
                        27 => false, // 节日固定晴天
                        _ => IsRainyDaySpringFall(gameID, absoluteDay, useLegacyRandom) // 预测
                    };

                default: // 冬季
                    return false;
            }
        }

        /// <summary>
        /// 计算绿雨日期
        /// </summary>
        private int GetGreenRainDay(int gameID, bool useLegacyRandom)
        {
            int greenRainSeed = HashHelper.GetRandomSeed(777, gameID, 0, 0, 0, useLegacyRandom);
            Random greenRainRng = new Random(greenRainSeed);
            int[] greenRainDays = { 5, 6, 7, 14, 15, 16, 18, 23 };
            int greenRainDay = greenRainDays[greenRainRng.Next(greenRainDays.Length)];
            return greenRainDay;
        }

        /// <summary>
        /// 按概率计算春秋雨天
        /// </summary>
        private bool IsRainyDaySpringFall(int gameID, int absoluteDay, bool useLegacyRandom)
        {
            int seed = HashHelper.GetRandomSeed(locationHash, gameID, absoluteDay - 1, 0, 0, useLegacyRandom);
            Random rng = new Random(seed);
            // 春季和秋季的普通日期：18.3% 概率
            return rng.NextDouble() < 0.183;
        }

        /// <summary>
        /// 按概率计算夏季雨天
        /// </summary>
        private bool IsRainyDaySummer(int gameID, int absoluteDay, bool useLegacyRandom, int dayOfMonth)
        {
            int rainSeed = HashHelper.GetRandomSeed(
                absoluteDay - 1, 
                gameID / 2, 
                HashHelper.GetHashFromString("summer_rain_chance"), 0, 0, useLegacyRandom);
            Random rainRng = new Random(rainSeed);
            double rainChance = 0.12 + 0.003 * (dayOfMonth - 1);
            return rainRng.NextDouble() < rainChance;
        }

        /// <summary>
        /// 计算搜索成本
        /// </summary>
        public int EstimateCost(bool useLegacyRandom)
        {
            if (Conditions.Count == 0) return 0;
            
            int totalDays = 0;
            foreach (var condition in Conditions)
            {
                totalDays += condition.AbsoluteEndDay - condition.AbsoluteStartDay + 1;
            }
            // 绿雨计算56次 + 每天1次天气判断
            return 56 + totalDays;
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
        /// 预测天气并返回详细信息（用于前端展示）
        /// </summary>
        public (Dictionary<int, bool> weather, int greenRainDay) PredictWeatherWithDetail(int gameID, bool useLegacyRandom)
        {
            var weather = new Dictionary<int, bool>();
            
            // 计算绿雨日期
            int greenRainSeed = HashHelper.GetRandomSeed(777, gameID, 0, 0, 0, useLegacyRandom); // 其实这里是year * 777，但目前只支持第一年，省略了
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