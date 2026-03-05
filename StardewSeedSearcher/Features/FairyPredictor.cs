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

            // 所有条件都必须满足（AND）
            foreach (var condition in Conditions)
            {
                int startAbs = TimeHelper.DateToAbsoluteDay(condition.StartYear, condition.StartSeason, condition.StartDay);
                int endAbs = TimeHelper.DateToAbsoluteDay(condition.EndYear, condition.EndSeason, condition.EndDay);
                
                bool foundInRange = false;
                
                // 在范围内寻找至少一个仙子
                for (int day = startAbs; day <= endAbs; day++)
                {
                    var date = TimeHelper.AbsoluteDaytoDate(day);
                    if (date.season >= 3) continue; // 跳过冬天

                    if (HasFairy(seed, day, useLegacyRandom))
                    {
                        foundInRange = true;
                        break; // 只要找到一次，该范围条件即满足
                    }
                }

                if (!foundInRange) return false;
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
        
        public int EstimateCost(bool useLegacyRandom)
        {
            if (Conditions.Count == 0) return 0;
            
            // 旧随机:1次随机判断
            // 新随机:10次跳过 + 1次判断 = 11次
            int callsPerDay = useLegacyRandom ? 1 : 11;

            int totalDays = 0;
            
            // 所有条件天数总和
            foreach (var condition in Conditions)
            {
                int startAbs = TimeHelper.DateToAbsoluteDay(condition.StartYear, condition.StartSeason, condition.StartDay);
                int endAbs = TimeHelper.DateToAbsoluteDay(condition.EndYear, condition.EndSeason, condition.EndDay);
            
                totalDays += endAbs - startAbs + 1;
            }

            // 总计算次数
            return totalDays * callsPerDay;
        }

        /// <summary>
        /// 记录条件里的天数，用于种子简介
        /// </summary>
        public List<object> GetFairyDays(int seed, bool useLegacyRandom)
        {
            var fairyDays = new List<object>();
            
            foreach (var condition in Conditions)
            {
                int startAbs = TimeHelper.DateToAbsoluteDay(condition.StartYear, condition.StartSeason, condition.StartDay);
                int endAbs = TimeHelper.DateToAbsoluteDay(condition.EndYear, condition.EndSeason, condition.EndDay);
                
                for (int day = startAbs; day <= endAbs; day++)
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