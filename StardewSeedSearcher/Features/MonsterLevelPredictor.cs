using StardewSeedSearcher.Core;

namespace StardewSeedSearcher.Features
{
    public class MonsterLevelPredictor : ISearchFeature
    {
        public List<MonsterLevelCondition> Conditions { get; set; } = new();
        
        public string Name => "怪物层";
        
        public bool IsEnabled { get; set; }
        
        public class MonsterLevelCondition
        {
            public int StartSeason { get; set; }
            public int EndSeason { get; set; }

            public int StartDay { get; set; }
            public int EndDay { get; set; }
            
            public int StartLevel { get; set; }
            public int EndLevel { get; set; }

            public int AbsoluteStartDay => TimeHelper.DateToAbsoluteDay(1, StartSeason, StartDay);
            public int AbsoluteEndDay => TimeHelper.DateToAbsoluteDay(1, EndSeason, EndDay);
        }
        
        /// <summary>
        /// 检查种子是否匹配所有条件（AND关系）
        /// </summary>
        public bool Check(int gameID, bool useLegacyRandom)
        {
            if (!IsEnabled) return true;

            // 动态排序
            var sortedConditions = Conditions.OrderBy(EstimateCostPerCondition).ToList();

            // 遍历每个条件
            foreach (var condition in sortedConditions)
            {
                // 检查指定日期和层数范围内是否有感染层
                for (int day = condition.AbsoluteStartDay; day <= condition.AbsoluteEndDay; day++)
                {
                    for (int mineLevel = condition.StartLevel; mineLevel <= condition.EndLevel; mineLevel++)
                    {
                        // 跳过电梯层（5的倍数）
                        if (mineLevel % 5 == 0)
                        {
                            continue;
                        }

                        // 创建随机数生成器
                        Random rng;
                        if (useLegacyRandom)
                        {
                            // 旧随机模式
                            int seed = day + mineLevel * 100 + gameID / 2;
                            rng = new Random(seed);
                        }
                        else
                        {
                            // 新随机模式
                            int seed = HashHelper.GetRandomSeed(day, gameID / 2, mineLevel * 100, 0, 0, false);
                            rng = new Random(seed);
                        }

                        // 检查 4.4% 概率成为感染层
                        if (rng.NextDouble() < 0.044)
                        {
                            // 检查层数限制
                            int mod40 = mineLevel % 40;
                            if (mod40 > 5 && mod40 < 30 && mod40 != 19)
                            {
                                // 发现感染层，不满足条件
                                return false;
                            }
                        }
                    }
                }
            }

            // 所有条件都满足
            return true;
        }
        
        private int EstimateCostPerCondition(MonsterLevelCondition c)
        {
            int days = c.AbsoluteEndDay - c.AbsoluteStartDay + 1;
            int levels = c.EndLevel - c.StartLevel + 1;
            // 减去电梯层数量
            int elevatorCount = 0;
            for (int level = c.StartLevel; level <= c.EndLevel; level++)
            {
                if (level % 5 == 0) elevatorCount++;
            }
            return days * (levels - elevatorCount);
        }

        /// <summary>
        /// 估算搜索成本
        /// </summary>
        public int EstimateCost(bool useLegacyRandom)
        {
            if (!IsEnabled || Conditions.Count == 0) return 0;
            
            // 找到范围最小（检查次数最少）的条件
            var bestCondition = Conditions.OrderBy(EstimateCostPerCondition).First();
            
            // 返回该条件的 RNG 总调用次数
            return EstimateCostPerCondition(bestCondition);
        }
        
        /// <summary>
        /// 获取详细信息（用于结果展示）
        /// </summary>
        public List<object> GetDetails(int gameID, bool useLegacyRandom)
        {
            return Conditions.Select(c => new
            {
                description = FormatConditionDescription(c),
                satisfied = true,
                absoluteStartDay = c.AbsoluteStartDay
            }).ToList<object>();
        }

        /// <summary>
        /// 格式化单个条件的描述
        /// </summary>
        private string FormatConditionDescription(MonsterLevelCondition c)
        {
            string dateRange = c.AbsoluteStartDay == c.AbsoluteEndDay
                ? $"{TimeHelper.GetSeasonName(c.StartSeason)}{c.StartDay}"
                : $"{TimeHelper.GetSeasonName(c.StartSeason)}{c.StartDay}-{TimeHelper.GetSeasonName(c.EndSeason)}{c.EndDay}";
            
            return $"{dateRange} {c.StartLevel}-{c.EndLevel}层无怪物层";
        }
    }
}