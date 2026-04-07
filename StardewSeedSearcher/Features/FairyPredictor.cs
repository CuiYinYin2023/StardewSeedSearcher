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

            // 动态排序
            // 仙子概率极低（1%），所以范围越窄的条件越容易在极短时间内证明“失败”
            // 优先检查预计耗时最短且最容易失败的范围
            var sortedConditions = Conditions.OrderBy(EstimateCostPerCondition).ToList();

            // 所有条件都必须满足（AND）
            foreach (var condition in sortedConditions)
            {
                int foundCount = 0;
                
                // 在范围内寻找至少一个仙子
                for (int day = condition.AbsoluteStartDay; day <= condition.AbsoluteEndDay; day++)
                {
                    var date = TimeHelper.AbsoluteDaytoDate(day);
                    if (date.season >= 3) continue; // 跳过冬天

                    if (HasFairy(seed, day, useLegacyRandom))
                    {
                        foundCount++;
                        // 如果已经达到要求的数量，该范围条件满足，跳出当前范围的循环
                        if (foundCount >= condition.MinOccurrences) 
                            break;
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
            
            foreach (var condition in Conditions)
            {
                for (int day = condition.AbsoluteStartDay; day <= condition.AbsoluteEndDay; day++)
                {
                    var date = TimeHelper.AbsoluteDaytoDate(day);
                    if (date.season >= 3) continue; // 跳过冬天

                    if (HasFairy(seed, day, useLegacyRandom))
                    {
                        fairyDays.Add(new
                        {
                            date.year,
                            date.season,
                            date.day
                        });
                    }
                }
            }
            return fairyDays;
        }
    }
}