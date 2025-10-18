using System;
using System.Collections.Generic;
using StardewSeedSearcher.Core;

namespace StardewSeedSearcher.Features
{
    /// <summary>
    /// 沙漠节商人预测器
    /// 预测春季15-17日（沙漠节）每天的2个摊贩村民
    /// </summary>
    public class DesertFestivalPredictor
    {
        // 27个有资格成为沙漠节商人的村民
        private static readonly HashSet<string> POSSIBLE_VENDORS = new HashSet<string>
        {
            "Abigail", "Caroline", "Clint", "Demetrius", "Elliott", "Emily", "Evelyn", "George",
            "Gus", "Haley", "Harvey", "Jas", "Jodi", "Alex", "Kent", "Leah", "Marnie", "Maru",
            "Pam", "Penny", "Pierre", "Robin", "Sam", "Sebastian", "Shane", "Vincent", "Leo"
        };

        // 每日排除规则（春15/16/17 对应 day 0/1/2）
        private static readonly Dictionary<int, HashSet<string>> SCHEDULE_EXCLUSION = new Dictionary<int, HashSet<string>>
        {
            { 0, new HashSet<string> { "Abigail", "Caroline", "Elliott", "Gus", "Alex", "Leah", "Pierre", "Sam", "Sebastian", "Haley" } },
            { 1, new HashSet<string> { "Haley", "Clint", "Demetrius", "Maru", "Pam", "Penny", "Robin", "Leo" } },
            { 2, new HashSet<string> { "Evelyn", "George", "Jas", "Jodi", "Kent", "Marnie", "Shane", "Vincent" } }
        };

        // 角色固定顺序（来自游戏存档的角色列表顺序）
        private static readonly List<string> CHARACTERS_IN_ORDER = new List<string>
        {
            "Evelyn", "George", "Alex", "Emily", "Haley", "Jodi", "Sam", "Vincent",
            "Clint", "Lewis", "Abigail", "Caroline", "Pierre", "Gus", "Pam", "Penny",
            "Harvey", "Elliott", "Demetrius", "Maru", "Robin", "Sebastian", "Linus",
            "Wizard", "Jas", "Marnie", "Shane", "Leah", "Dwarf", "Sandy", "Willy"
        };

        /// <summary>
        /// 预测第一年沙漠节三天的商人
        /// </summary>
        /// <param name="gameID">游戏种子</param>
        /// <param name="useLegacyRandom">是否使用旧随机模式</param>
        /// <returns>字典，key为0/1/2（对应春15/16/17），value为2个商人名字的列表</returns>
        public Dictionary<int, List<string>> PredictVendors(int gameID, bool useLegacyRandom)
        {
            var vendors = new Dictionary<int, List<string>>
            {
                { 0, new List<string>() },
                { 1, new List<string>() },
                { 2, new List<string>() }
            };

            // 遍历三天（春15/16/17）
            for (int d = 0; d < 3; d++)
            {
                int day = 15 + d;

                // 构建当天的候选池
                List<string> vendorPool = BuildVendorPool(d);

                // 初始化RNG
                int seed = HashHelper.GetRandomSeed(day, gameID / 2, 0, 0, 0, useLegacyRandom);
                Random rng = new Random(seed);

                // 预移除：跨日去重逻辑
                // 第0天移除0个，第1天移除2个，第2天移除4个
                for (int k = 0; k < d; k++)
                {
                    for (int m = 0; m < 2; m++)
                    {
                        int index = rng.Next(vendorPool.Count);
                        vendorPool.RemoveAt(index);
                    }
                }

                // 选择当天的2个商人
                for (int i = 0; i < 2; i++)
                {
                    int index = rng.Next(vendorPool.Count);
                    string selectedVendor = vendorPool[index];
                    vendors[d].Add(selectedVendor);
                    vendorPool.RemoveAt(index);
                }
            }

            return vendors;
        }

        /// <summary>
        /// 构建指定日期的候选村民池
        /// </summary>
        /// <param name="d">相对日期（0=春15, 1=春16, 2=春17）</param>
        /// <returns>候选村民列表（保持固定顺序）</returns>
        private List<string> BuildVendorPool(int d)
        {
            var pool = new List<string>();
            var exclusion = SCHEDULE_EXCLUSION[d];

            foreach (string name in CHARACTERS_IN_ORDER)
            {
                // 检查是否在候选名单中
                if (!POSSIBLE_VENDORS.Contains(name))
                    continue;

                // 检查是否被当天排除
                if (exclusion.Contains(name))
                    continue;

                pool.Add(name);
            }

            return pool;
        }
    }
}