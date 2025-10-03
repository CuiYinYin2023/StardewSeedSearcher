using System;
using StardewSeedSearcher.Core;

namespace StardewSeedSearcher.Features
{
    /// <summary>
    /// 仙子预测器
    /// </summary>
    public class FairyPredictor
    {
        /// <summary>
        /// 查找第一次出现仙子的日期
        /// </summary>
        /// <param name="gameID">游戏种子</param>
        /// <param name="useLegacyRandom">是否使用旧随机模式</param>
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
    }
}