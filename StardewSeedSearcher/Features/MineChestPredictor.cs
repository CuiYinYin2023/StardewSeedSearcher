using System;
using System.Collections.Generic;
using System.Linq;
using StardewSeedSearcher.Core;
using StardewSeedSearcher.Data;

namespace StardewSeedSearcher.Features
{
    public class MineChestPredictor : ISearchFeature
    {
        public List<MineChestCondition> Conditions { get; set; } = new();
        
        public string Name => "矿井宝箱";
        
        public bool IsEnabled { get; set; }
        
        public class MineChestCondition
        {
            public int Floor { get; set; }
            public string ItemName { get; set; }
        }
        
        /// <summary>
        /// 设置条件（从前端请求传入）
        /// </summary>
        public void SetConditions(List<MineChestCondition> conditions)
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
            
            foreach (var condition in Conditions)
            {
                string actualItem = PredictItem(gameID, condition.Floor, useLegacyRandom);
                if (actualItem != condition.ItemName)
                {
                    return false;
                }
            }
            return true;
        }
        
        /// <summary>
        /// 预测指定楼层的宝箱物品
        /// </summary>
        private string PredictItem(int gameID, int floor, bool useLegacyRandom)
        {
            int seed;
            if (useLegacyRandom)
            {
                long temp = gameID * 512L + floor;
                seed = HashHelper.GetRandomSeed((int)(temp % int.MaxValue), 0, 0, 0, 0, true);
            }
            else
            {
                long temp = (long)gameID * 512;
                int safeValue = (int)(temp % 2147483647);
                seed = HashHelper.GetRandomSeed(safeValue, floor, 0, 0, 0, false); // 先取模防止整数溢出
            }
            
            Random rng = new Random(seed);
            string[] items = MineChestData.ItemsCN[floor];
            int index = rng.Next(items.Length);
            return items[index];
        }
        
        /// <summary>
        /// 估算搜索成本
        /// </summary>
        public int EstimateCost(bool useLegacyRandom)
        {
            return Conditions.Count;
        }
        
        /// <summary>
        /// 获取详细信息（用于结果展示）
        /// </summary>
        public List<object> GetDetails(int gameID, bool useLegacyRandom)  // 改为返回 List<object>
        {
            var results = new List<object>();
            foreach (var condition in Conditions)
            {
                string actualItem = PredictItem(gameID, condition.Floor, useLegacyRandom);
                results.Add(new
                {
                    floor = condition.Floor,
                    item = actualItem,
                    matched = actualItem == condition.ItemName
                });
            }
            return results;
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
            
            var descriptions = Conditions.Select(c => $"{c.Floor}层:{c.ItemName}");
            return string.Join(", ", descriptions);
        }
    }
}