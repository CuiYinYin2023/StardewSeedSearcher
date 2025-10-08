using System.Collections.Generic;

namespace StardewSeedSearcher.Data
{
    /// <summary>
    /// 矿井宝箱相关数据，目前仅用于测试
    /// 来源：StardewValley.Locations.MineShaft.GetReplacementChestItem()
    /// </summary>
    public static class MineChestData
    {
        /// <summary>有宝箱的楼层列表</summary>
        public static readonly int[] ChestFloors = { 10, 20, 50, 60, 80, 90, 110 };
        
        /// <summary>每层宝箱的物品池（中文名）</summary>
        public static readonly Dictionary<int, string[]> ItemsCN = new Dictionary<int, string[]>
        {
            // 10层：
            {10, new[]
            {
                "皮靴",
                "工作靴",
                "木剑",
                "铁制短剑",
                "疾风利剑",
                "股骨"
            }},
            
            // 20层：
            {20, new[]
            {
                "钢制轻剑",
                "木棒",
                "精灵之刃",
                "光辉戒指",
                "磁铁戒指"
            }},
            
            // 50层：
            {50, new[]
            {
                "冻土靴",
                "热能靴",
                "战靴",
                "镀银军刀",
                "海盗剑"
            }},
            
            // 60层：
            {60, new[]
            {
                "水晶匕首",
                "弯刀",
                "铁刃",
                "飞贼之胫",
                "木锤"
            }},
            
            // 80层：
            {80, new[]
            {
                "蹈火者靴",
                "黑暗之靴",
                "双刃大剑",
                "圣堂之刃",
                "长柄锤",
                "暗影匕首"
            }},
            
            // 90层：
            {90, new[]
            {
                "黑曜石之刃",
                "淬火阔剑",
                "蛇形邪剑",
                "骨剑",
                "骨化剑"
            }},
            
            // 110层：
            {110, new[]
            {
                "太空之靴",
                "水晶鞋",
                "钢刀",
                "巨锤"
            }}
        };
    }
}
