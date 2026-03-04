using StardewSeedSearcher.Core;
using StardewSeedSearcher.Framework;

namespace StardewSeedSearcher.Features
{
    /// <summary>
    /// 仙子预测器
    /// </summary>
    public class FairyPredictor : ISearchFeature
    {
        public bool IsEnabled { get; set; }
        public List<Date> Conditions { get; } = new();

        public string Name => "仙子预测";

        public string GetConfigDescription()
        {
            return $"{Conditions.Count} 个条件";
        }

        public bool Check(int seed, bool useLegacyRandom)
        {
            if (Conditions.Count == 0)
                return true;

            // 所有条件都必须满足（AND）
            foreach (var condition in Conditions)
            {
                int absoluteDay = condition.ConvertToDaysPlayed();
                
                if (!HasFairy(seed, absoluteDay, useLegacyRandom))
                {
                    return false;
                }
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
            
            // 每个条件检查一天
            // 旧随机:1次随机判断
            // 新随机:10次跳过 + 1次判断 = 11次
            int callsPerDay = useLegacyRandom ? 1 : 11;
            return Conditions.Count * callsPerDay;
        }

        /// <summary>
        /// 记录条件里的天数，用于种子简介
        /// </summary>
        public List<object> GetFairyDays(int seed, bool useLegacyRandom)
        {
            var fairyDays = new List<object>();
            
            foreach (var date in Conditions)
            {
                int absoluteDay = date.ConvertToDaysPlayed();
                
                if (HasFairy(seed, absoluteDay, useLegacyRandom))
                {
                    fairyDays.Add(new
                    {
                        year = date.Year,
                        season = date.Season.ToString(),
                        day = date.Day
                    });
                }
            }
            
            return fairyDays;
        }

        /// <summary>
        /// 查找第一次出现仙子的日期，仅用于测试
        /// </summary>
        /// <param name="gameID">游戏种子</param>
        /// <param name="useLegacyRandom">是否使用旧随机模式</param>
        /// <param name="maxDays">总共搜索这么多天</param>
        /// <returns>绝对天数,如果搜索范围内没有则返回null</returns>
        public int? FindFirstFairy(int gameID, bool useLegacyRandom, int maxDays = 1000)
        {
            // 持续搜索直到找到第一个仙子或达到最大天数
            for (int day = 1; day <= maxDays; day++)
            {
                // 计算月份(季节): 0=春, 1=夏, 2=秋, 3=冬
                int month = (day - 1) % 112 / 28;
                
                // 仙子只在春夏秋出现,冬天跳过
                if (month >= 3)
                {
                    continue;
                }
                
                // 判断当天是否出现仙子
                if (HasFairy(gameID, day, useLegacyRandom))
                {
                    return day;
                }
            }
            
            // 搜索范围内没有仙子
            return null;
        }
    }
}