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


        public static Dictionary<string, ItemInfo> Objects = new();

        public static void Initialize()
        {
            var tcData = File.OpenRead(Path.Combine("Data", "TravelingCartData.json"));
            Objects = JsonSerializer.Deserialize<Dictionary<string, ItemInfo>>(tcData)!;
        }
    }
}