using System;
using System.Collections.Generic;
using StardewSeedSearcher.Core;

namespace StardewSeedSearcher.Features
{
    /// <summary>
    /// 怪物层信息
    /// </summary>
    public class MonsterLevelInfo
    {
        public List<int> MonsterLevels { get; set; } = new List<int>();
        public List<int> SlimeLevels { get; set; } = new List<int>();
    }

    /// <summary>
    /// 矿井怪物层预测器
    /// </summary>
    public class MonsterLevelPredictor
    {
        /// <summary>
        /// 预测指定天数范围内的怪物层
        /// </summary>
        /// <param name="gameID">游戏种子</param>
        /// <param name="useLegacyRandom">是否使用旧随机模式</param>
        /// <param name="startDay">起始天数（默认1）</param>
        /// <param name="endDay">结束天数（默认5）</param>
        /// <returns>每天的怪物层信息</returns>
        public Dictionary<int, MonsterLevelInfo> PredictMonsterLevels(
            int gameID, 
            bool useLegacyRandom, 
            int startDay = 1, 
            int endDay = 5)
        {
            var results = new Dictionary<int, MonsterLevelInfo>();

            for (int day = startDay; day <= endDay; day++)
            {
                var info = new MonsterLevelInfo();

                // 遍历矿井 1-119 层
                for (int mineLevel = 1; mineLevel < 120; mineLevel++)
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
                            // 50% 概率决定是怪物层还是史莱姆层
                            if (rng.NextDouble() < 0.5)
                            {
                                info.MonsterLevels.Add(mineLevel);
                            }
                            else
                            {
                                info.SlimeLevels.Add(mineLevel);
                            }
                        }
                    }
                }

                results[day] = info;
            }

            return results;
        }
    }
}