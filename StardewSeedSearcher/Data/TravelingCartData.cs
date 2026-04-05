using System.Text.Json;

namespace StardewSeedSearcher.Data
{
    [Serializable]
    public class ItemInfo
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty;
        public int Category { get; set; }
        public int Price { get; set; }
        public bool OffLimits { get; set; }
    }

    public struct SearchableItem 
    { 
        public string Name; 
        public int Price; 
        public bool IsEligible;
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

        // 用于快速判断要搜索的物品是否为技能书
        public static readonly HashSet<string> SkillBookSet = [.. SkillBooks];


        public static Dictionary<string, ItemInfo> Objects = new();

        // 专为搜索优化的数组
        public static SearchableItem[] OptimizedItems;

        public static void Initialize()
        {
            // 确保获取的是程序运行时的绝对根目录，方便后续打包
            string basePath = AppContext.BaseDirectory;
            string jsonPath = Path.Combine(basePath, "Data", "TravelingCartData.json");

            string jsonString = File.ReadAllText(jsonPath);
            Objects = JsonSerializer.Deserialize<Dictionary<string, ItemInfo>>(jsonString) ?? new();

            // 启动时一次性执行预处理物品池，将所有物品按顺序转为数组，并预先判定好资格，加速后续搜索
            var tempList = new List<SearchableItem>();

            // 严格按照游戏原始顺序遍历
            foreach (var kvp in Objects)
            {
                var item = kvp.Value;

                // 预先判定该物品是否能进入猪车的“基础物品池”
                bool eligible = int.TryParse(item.Id, out int id)&&
                                id >= 2 && id <= 789 && 
                                item.Price > 0 && 
                                !item.OffLimits && 
                                (item.Category < 0 || item.Category == -999) &&
                                item.Type != "Arch" && item.Type != "Minerals" && item.Type != "Quest";

                tempList.Add(new SearchableItem { 
                    Name = item.Name, 
                    Price = item.Price,
                    IsEligible = eligible 
                });
            }
            OptimizedItems = tempList.ToArray();
        }
    }
}