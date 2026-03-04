using System.Collections.Generic;
using System.Text.Json;

namespace StardewSeedSearcher.Data
{
    [Serializable]
    public class ItemInfo
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string Type { get; set; }
        public int Category { get; set; }
        public int Price { get; set; }
        public bool OffLimits { get; set; }
    }

    public static class TravelingCartData
    {
        
        // 技能书列表
        public static readonly string[] SkillBooks = {
            "星露谷年历",
            "鱼饵和浮漂",
            "樵夫周刊",
            "采矿月刊",
            "战斗季刊"
            // "Stardew Valley Almanac",
            // "Bait And Bobber",
            // "Woodcutter's Weekly",
            // "Mining Monthly",
            // "Combat Quarterly"
        };

        // 春季猪车日期
        public static readonly int[] SpringCartDays =
        [
            5, 7, 12, 14, 15, 16, 17, 19, 21, 26, 28, 
            28+5, 28+7, 28+12, 28+14, 28+19, 28+21, 28+26, 28+28, 
            56+5, 56+7, 56+12, 56+14, 56+19, 56+21, 56+26, 56+28,
            84+5, 84+7, 84+12, 84+14, 84+15, 84+16, 84+17, 84+19, 84+21, 84+26, 84+28 
        ];
        // public static readonly int[] SpringCartDays = { 5, 7, 12, 14 };

        public static Dictionary<string, ItemInfo> Objects = new();

        public static void Initialize()
        {
            var tcData = File.OpenRead(Path.Combine("Data", "TravelingCartData.json"));
            Objects = JsonSerializer.Deserialize<Dictionary<string, ItemInfo>>(tcData)!;
        }
    }
}