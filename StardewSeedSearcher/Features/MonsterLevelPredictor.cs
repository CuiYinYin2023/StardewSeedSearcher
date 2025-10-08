using System;
using System.Collections.Generic;
using System.Linq;
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
            /// <summary>
            /// 起始日期（1-112，第一年）
            /// </summary>
            public int StartDay { get; set; }

            /// <summary>
            /// 结束日期（1-112，第一年）
            /// </summary>
            public int EndDay { get; set; }

            /// <summary>
            /// 起始层数（1-120）
            /// </summary>
            public int StartLevel { get; set; }

            /// <summary>
            /// 结束层数（1-120）
            /// </summary>
            public int EndLevel { get; set; }
        }
        
        /// <summary>
        /// 设置条件（从前端请求传入）
        /// </summary>
        public void SetConditions(List<MonsterLevelCondition> conditions)
        {
            this.Conditions = conditions ?? new();
            IsEnabled = this.Conditions.Count > 0;
        }
        
        /// <summary>
        /// 检查种子是否匹配所有条件（AND关系）
        /// </summary>
        public bool Check(int gameID, bool useLegacyRandom)
        {
            if (!IsEnabled) return true;

            // 遍历每个条件
            foreach (var condition in Conditions)
            {
                // 检查指定日期和层数范围内是否有感染层
                for (int day = condition.StartDay; day <= condition.EndDay; day++)
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
        
        /// <summary>
        /// 估算搜索成本
        /// </summary>
        public int EstimateCost(bool useLegacyRandom)
        {
            int totalCost = 0;
            foreach (var condition in Conditions)
            {
                int days = condition.EndDay - condition.StartDay + 1;
                int levels = condition.EndLevel - condition.StartLevel + 1;
                // 减去电梯层数量
                int elevatorCount = 0;
                for (int level = condition.StartLevel; level <= condition.EndLevel; level++)
                {
                    if (level % 5 == 0) elevatorCount++;
                }
                totalCost += days * (levels - elevatorCount);
            }
            return totalCost;
        }
        
        /// <summary>
        /// 获取详细信息（用于结果展示）
        /// </summary>
        public List<object> GetDetails(int gameID, bool useLegacyRandom)
        {
            return Conditions.Select(c => new
            {
                description = FormatConditionDescription(c),
                satisfied = true
            }).ToList<object>();
        }

        /// <summary>
        /// 获取配置描述（用于显示当前设置）
        /// </summary>
        public string GetConfigDescription()
        {
            if (!IsEnabled || Conditions.Count == 0)
            {
                return "未启用";
            }
            
            var descriptions = Conditions.Select(c => FormatConditionDescription(c));
            return string.Join(", ", descriptions);
        }

        /// <summary>
        /// 格式化单个条件的描述
        /// </summary>
        private string FormatConditionDescription(MonsterLevelCondition c)
        {
            string[] seasonNames = { "春", "夏", "秋", "冬" };
            int startSeason = (c.StartDay - 1) / 28;
            int startDayOfMonth = ((c.StartDay - 1) % 28) + 1;
            int endSeason = (c.EndDay - 1) / 28;
            int endDayOfMonth = ((c.EndDay - 1) % 28) + 1;
            
            string dateRange = c.StartDay == c.EndDay
                ? $"{seasonNames[startSeason]}{startDayOfMonth}"
                : $"{seasonNames[startSeason]}{startDayOfMonth}-{seasonNames[endSeason]}{endDayOfMonth}";
            
            return $"{dateRange} {c.StartLevel}-{c.EndLevel}层无怪物层";
        }
    }
}