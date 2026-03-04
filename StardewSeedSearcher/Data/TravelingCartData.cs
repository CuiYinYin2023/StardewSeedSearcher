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
            // 确保获取的是程序运行时的绝对根目录，方便后续打包
            string basePath = AppContext.BaseDirectory;
            string jsonPath = Path.Combine(basePath, "Data", "TravelingCartData.json");

            string jsonString = File.ReadAllText(jsonPath);
            Objects = JsonSerializer.Deserialize<Dictionary<string, ItemInfo>>(jsonString) ?? new();
        }
    }
}