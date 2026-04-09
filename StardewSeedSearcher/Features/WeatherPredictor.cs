using StardewSeedSearcher.Core;

namespace StardewSeedSearcher.Features
{
    
    /// <summary>
    /// 天气筛选条件
    /// </summary>
    public class WeatherCondition
    {
        public int Season { get; set; }
        public int StartDay { get; set; }
        public int EndDay { get; set; }
        public int MinRainDays { get; set; }

        public bool IsRecordBest { get; set; }

        public int AbsoluteStartDay => TimeHelper.DateToAbsoluteDay(1, Season, StartDay);
        public int AbsoluteEndDay => TimeHelper.DateToAbsoluteDay(1, Season, EndDay);
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
        
        public Action<int, int> OnRecordBestUpdate { get; set; }

        /// <summary>
        /// 检查种子是否符合筛选条件
        /// </summary>
        /// <summary>
        /// 检查种子是否符合筛选条件
        /// </summary>
        public bool Check(int gameID, bool useLegacyRandom)
        {
            if (Conditions.Count == 0)
                return true;

            // 只计算一次绿雨
            int greenRainDay = GetGreenRainDay(gameID, useLegacyRandom);

            // 动态排序，优先检查范围窄、要求高的条件
            var sortedConditions = Conditions.OrderBy(EstimateCostPerCondition).ToList();
            
            // 逐条件检查，失败立即返回
            foreach (var condition in sortedConditions)
            {
                int rainCount = 0;
                
                if (condition.IsRecordBest)
                {
                    for (int day = condition.AbsoluteStartDay; day <= condition.AbsoluteEndDay; day++)
                    {
                        if (rainCount + condition.AbsoluteEndDay - day + 1 < condition.MinRainDays) 
                            break; 

                        int season = condition.Season;
                        int dayOfMonth = ((day - 1) % 28) + 1;
                        
                        if (IsRainyDay(season, dayOfMonth, day, gameID, useLegacyRandom, greenRainDay))
                        {
                            if (++rainCount >= condition.MinRainDays) break;
                        }
                    }
                    
                    if (rainCount < condition.MinRainDays)
                    {
                        OnRecordBestUpdate?.Invoke(gameID, rainCount);
                        return false; 
                    }
                }
                else
                {
                    for (int day = condition.AbsoluteStartDay; day <= condition.AbsoluteEndDay; day++)
                    {
                        int season = condition.Season;
                        int dayOfMonth = ((day - 1) % 28) + 1;
                        
                        // 检查当天是否下雨
                        if (IsRainyDay(season, dayOfMonth, day, gameID, useLegacyRandom, greenRainDay))
                        {
                            rainCount++;
                            if (rainCount >= condition.MinRainDays)
                                break;
                        }

                        // 剩余天数不足以满足最小需求时，提前失败
                        int remainingDays = condition.AbsoluteEndDay - day;
                        if (rainCount + remainingDays < condition.MinRainDays)
                            return false; 
                    }
                    
                    if (rainCount < condition.MinRainDays)
                        return false;
                }
            }

            return true;
        }

        /// <summary>
        /// 判断某一天是否下雨
        /// </summary>
        public bool IsRainyDay(int season, int dayOfMonth, int absoluteDay, int gameID, bool useLegacyRandom, int greenRainDay)
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
        public int GetGreenRainDay(int gameID, bool useLegacyRandom)
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

        private int EstimateCostPerCondition(WeatherCondition c)
        {
            // 计算该条件涉及的总天数
            int totalDays = c.AbsoluteEndDay - c.AbsoluteStartDay + 1;

            // 春秋雨天概率~18.3%，夏季雨天概率~23.5%
            // 按概率推算最少需要检查天数
            int theoreticalTotalDays = (int)(c.Season == 1 
                ? c.MinRainDays / 0.235 
                : c.MinRainDays / 0.183);

            // 例，如果春1-28想搜3个雨天，理论上只需要搜16天
            // 如果春15-18想搜3个雨天，也只需要搜3天
            return Math.Min(totalDays, theoreticalTotalDays); 
        }

        /// <summary>
        /// 计算搜索成本
        /// </summary>
        public int EstimateCost(bool useLegacyRandom)
        {
            if (Conditions.Count == 0) return 0;
            
            // 找到最容易失败（最便宜）的那个条件
            var bestCondition = Conditions.OrderBy(EstimateCostPerCondition).First();
            
            // 基础开销(绿雨56次) + 第一个条件的预期天数
            return 56 + EstimateCostPerCondition(bestCondition);
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