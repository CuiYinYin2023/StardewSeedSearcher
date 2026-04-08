using StardewSeedSearcher.Core;

namespace StardewSeedSearcher.Features
{
    // 仙子条件类
    public class FairyCondition
    {
        public int StartYear { get; set; }
        public int StartSeason { get; set; }
        public int StartDay { get; set; }
        
        public int EndYear { get; set; }
        public int EndSeason { get; set; }
        public int EndDay { get; set; }

        public int MinOccurrences { get; set; } = 1; 

        public int AbsoluteStartDay => TimeHelper.DateToAbsoluteDay(StartYear, StartSeason, StartDay);
        public int AbsoluteEndDay => TimeHelper.DateToAbsoluteDay(EndYear, EndSeason, EndDay);
    }

    /// <summary>
    /// 仙子预测器
    /// </summary>
    public class FairyPredictor : ISearchFeature
    {
        public bool IsEnabled { get; set; }
        public List<FairyCondition> Conditions { get; set; } = new();

        public string Name => "仙子预测";

        public bool Check(int seed, bool useLegacyRandom)
        {
            if (Conditions.Count == 0)
                return true;

            // 实例化天气预测器并预计算绿雨日，用于次日天气判定
            var wp = new WeatherPredictor();
            int greenRainDay = wp.GetGreenRainDay(seed, useLegacyRandom);

            // 动态排序
            // 仙子概率极低（1%），所以范围越窄的条件越容易在极短时间内证明“失败”
            // 优先检查预计耗时最短且最容易失败的范围
            var sortedConditions = Conditions.OrderBy(EstimateCostPerCondition).ToList();

            // 所有条件都必须满足（AND）
            foreach (var condition in sortedConditions)
            {
                int foundCount = 0;
                
                // 在范围内寻找仙子
                for (int day = condition.AbsoluteStartDay; day <= condition.AbsoluteEndDay; day++)
                {
                    // 如果剩余天数不足，直接跳过
                    if (foundCount + condition.AbsoluteEndDay - day + 1 < condition.MinOccurrences)
                        return false;

                    var date = TimeHelper.AbsoluteDaytoDate(day);
                    if (date.season >= 3) continue; // 跳过冬天

                    // 判定当天是否产生仙子
                    if (HasFairy(seed, day, useLegacyRandom))
                    {
                        // 判定次日(day + 1)是否下雨
                        int nextDayAbs = day + 1;
                        var nextDate = TimeHelper.AbsoluteDaytoDate(nextDayAbs);

                        bool isNextDayRainy = wp.IsRainyDay(
                            nextDate.season, 
                            nextDate.day, 
                            nextDayAbs, 
                            seed, 
                            useLegacyRandom, 
                            greenRainDay
                        );

                        // 只有次日不下雨，仙子才会真正降临，才计入总数
                        if (!isNextDayRainy)
                        {
                            foundCount++;
                            // 如果已经达到要求的数量，该范围条件满足，跳出当前范围的循环
                            if (foundCount >= condition.MinOccurrences) 
                                break;
                        }
                    }
                }

                // 如果跑完整个范围都没达到要求的次数，则该种子不符合条件
                if (foundCount < condition.MinOccurrences) 
                    return false;
            }

            return true;
        }

        /// <summary>
        /// 判断指定天是否出现仙子
        /// </summary>
        private bool HasFairy(int gameID, int day, bool useLegacyRandom)
        {
            Random rng;
            
            int seed = HashHelper.GetRandomSeed(day + 1, gameID / 2, 0, 0, 0, useLegacyRandom);
            rng = new Random(seed);
            
            // 跳过前10次随机数
            for (int i = 0; i < 10; i++)
            {
                rng.NextDouble();
            }
            
            // 判断概率
            return rng.NextDouble() < 0.01;
        }

        private int EstimateCostPerCondition(FairyCondition c)
        {
            return c.AbsoluteEndDay - c.AbsoluteStartDay + 1;
        }

        public int EstimateCost(bool useLegacyRandom)
        {
            if (Conditions.Count == 0) 
                return 0;
            
            // 旧随机:1次随机判断
            // 新随机:10次跳过 + 1次判断 = 11次
            int callsPerDay = useLegacyRandom ? 1 : 11;

            // 找到范围最窄的条件
            var bestCondition = Conditions.OrderBy(EstimateCostPerCondition).First();
            
            // 期望开销 = 预期检查天数 * 单日成本
            return EstimateCostPerCondition(bestCondition) * callsPerDay;
        }

        /// <summary>
        /// 记录条件里的天数，用于种子简介
        /// </summary>
        public List<object> GetFairyDays(int seed, bool useLegacyRandom)
        {
            var fairyDays = new List<object>();
            
            // 实例化天气预测器以使用其逻辑
            var wp = new WeatherPredictor();
            // 获取当年的绿雨日
            int greenRainDay = wp.GetGreenRainDay(seed, useLegacyRandom);


            foreach (var condition in Conditions)
            {
                for (int day = condition.AbsoluteStartDay; day <= condition.AbsoluteEndDay; day++)
                {
                    var date = TimeHelper.AbsoluteDaytoDate(day);
                    if (date.season >= 3) continue; // 跳过冬天

                    if (HasFairy(seed, day, useLegacyRandom))
                    {
                        // 直接调用 WeatherPredictor 判定次日(day + 1)天气
                        int nextDayAbs = day + 1;
                        var nextDate = TimeHelper.AbsoluteDaytoDate(nextDayAbs);

                        bool isBlocked = wp.IsRainyDay(
                            nextDate.season, 
                            nextDate.day, 
                            nextDayAbs, 
                            seed, 
                            useLegacyRandom, 
                            greenRainDay
                        );

                        fairyDays.Add(new
                        {
                            date.year,
                            date.season,
                            date.day,
                            isBlocked = isBlocked // 将“是否被雨天拦截”发送给前端
                        });
                    }
                }
            }
            return fairyDays;
        }
    }
}