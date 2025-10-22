using System;
using System.Collections.Generic;
using System.Linq;
using StardewSeedSearcher.Core;
using StardewSeedSearcher.Data;

namespace StardewSeedSearcher.Features
{
    public class CartItem
    {
        public string Category { get; set; }
        public string Name { get; set; }
        public int Quantity { get; set; }
        public int Price { get; set; }
    }
    
    public class CartDayResult
    {
        public int Day { get; set; }
        public string DayName { get; set; }
        public List<CartItem> Items { get; set; } = new List<CartItem>();
    }

    /// <summary>
    /// 猪车预测器
    /// </summary>
    public class TravelingCartPredictor
    {
        /// <summary>
        /// 预测指定种子在春季的所有猪车内容
        /// </summary>
        public List<CartDayResult> PredictSpring(int seed, bool useLegacyRandom)
        {
            int gameID = seed;
            
            // 红卷心菜保底
            int guaranteeSeed = HashHelper.GetRandomSeed(12 * gameID, 0, 0, 0, 0, useLegacyRandom);
            Random rngGuarantee = new Random(guaranteeSeed);
            int originalGuarantee = rngGuarantee.Next(2, 31);
            
            var results = new List<CartDayResult>();
            
            foreach (int day in TravelingCartData.SpringCartDays)
            {
                var result = PredictCartDay(gameID, day, originalGuarantee, useLegacyRandom);
                results.Add(result);
            }
            
            return results;
        }
        
        /// <summary>
        /// 预测指定日期的猪车内容
        /// </summary>
        private CartDayResult PredictCartDay(int gameID, int day, int originalGuarantee, bool useLegacyRandom)
        {
            var result = new CartDayResult
            {
                Day = day,
                DayName = GetDayName(day)
            };
            
            // 1. 创建主RNG
            int seed = HashHelper.GetRandomSeed(day, gameID / 2, 0, 0, 0, useLegacyRandom);
            Random rng = new Random(seed);
            
            // 2. 获取10个基础物品
            List<string> selectedItemKeys = GetRandomItems(rng);
            
            bool seenRareSeed = false;
            
            for (int i = 0; i < selectedItemKeys.Count; i++)
            {
                ItemInfo item = TravelingCartData.Objects[selectedItemKeys[i]];
                int price = Math.Max(rng.Next(1, 11) * 100, rng.Next(3, 6) * item.Price);
                int qty = (rng.NextDouble() < 0.1) ? 5 : 1;
                
                if (item.Name == "Rare Seed")
                {
                    seenRareSeed = true;
                }
                
                result.Items.Add(new CartItem
                {
                    Category = $"基础物品{i + 1}",
                    Name = item.Name,
                    Quantity = qty,
                    Price = price
                });
            }
            
            // ===== 3. 红卷心菜保底（消耗RNG，不输出）=====
            int visitsNow = CalculateVisitsRemaining(day, originalGuarantee);
            if (visitsNow == 0)
            {
                rng.Next(1, 11);   // 消耗价格计算的第一次
                rng.Next(3, 6);    // 消耗价格计算的第二次
                rng.NextDouble();  // 消耗数量判断
            }
            
            // ===== 4. 家具（消耗RNG，不输出）=====
            int furnitureCount = 645;  // 家具总数
            for (int i = 0; i < furnitureCount; i++)
            {
                rng.Next();  // 每个家具消耗一次
            }
            rng.Next(1, 11);  // 家具价格消耗一次
            
            // 5. 季节特殊物品（消耗RNG，不输出）
            int season = (day - 1) / 28;
            if (season < 2 && !seenRareSeed)
            {
                rng.NextDouble();  // 消耗一次用于数量判断
            }
            
            // 6. 技能书
            int skillHash = HashHelper.GetHashFromString("travelerSkillBook");
            int skillSeed = HashHelper.GetRandomSeed(skillHash, gameID, day, 0, 0, useLegacyRandom);
            Random rngSkill = new Random(skillSeed);
            
            if (rngSkill.NextDouble() < 0.05)
            {
                string book = TravelingCartData.SkillBooks[rng.Next(TravelingCartData.SkillBooks.Length)];
                result.Items.Add(new CartItem
                {
                    Category = "技能书",
                    Name = book,
                    Quantity = -1, // 特殊值表示无限
                    Price = 6000
                });
            }
            else
            {
                result.Items.Add(new CartItem
                {
                    Category = "技能书",
                    Name = "(None)",
                    Quantity = 0,
                    Price = 0
                });
            }
            
            return result;
        }
        
        /// <summary>
        /// 获取随机物品（核心算法）
        /// </summary>
        private List<string> GetRandomItems(Random rng)
        {
            var itemsWithRandomKey = new List<(int randomKey, string objectKey)>();
            
            // 必须按照原始定义顺序遍历
            foreach (var kvp in TravelingCartData.Objects)
            {
                // 关键：每个物品都要消耗一次RNG
                int randomKey = rng.Next();
                
                ItemInfo item = kvp.Value;
                
                // 过滤条件（这里才判断是否加入列表）
                if (!int.TryParse(item.Id, out int itemID)) continue;
                if (itemID < 2 || itemID > 789) continue;
                if (item.Price <= 0) continue;
                if (item.OffLimits) continue;
                if (item.Category >= 0 || item.Category == -999) continue;
                if (item.Type == "Arch" || item.Type == "Minerals" || item.Type == "Quest") continue;
                
                itemsWithRandomKey.Add((randomKey, kvp.Key));
            }
            
            // 排序并取前10个
            var sortedItems = itemsWithRandomKey.OrderBy(x => x.randomKey).ToList();
            return sortedItems.Take(10).Select(x => x.objectKey).ToList();
        }
        
        /// <summary>
        /// 计算红卷心菜保底剩余访问次数
        /// </summary>
        private int CalculateVisitsRemaining(int day, int originalGuarantee)
        {
            int visitsNow = originalGuarantee - (day / 7) - ((day + 2) / 7);
            
            // 沙漠节调整（春15-17是day 15-17）
            if (day >= 15) visitsNow--;  // 春15
            if (day >= 16) visitsNow--;  // 春16
            if (day >= 17) visitsNow--;  // 春17
            
            // 夜市调整（冬15-17是day 99-101）
            if (day >= 99) visitsNow--;   // 冬15
            if (day >= 100) visitsNow--;  // 冬16
            if (day >= 101) visitsNow--;  // 冬17
            
            return visitsNow;
        }
        
        /// <summary>
        /// 获取日期名称
        /// </summary>
        private string GetDayName(int day)
        {
            string[] seasonNames = { "春", "夏", "秋", "冬" };
            string[] dayNames = { "周一", "周二", "周三", "周四", "周五", "周六", "周日" };
            
            int season = (day - 1) / 28;
            int dayOfMonth = ((day - 1) % 28) + 1;
            int dayOfWeek = (day - 1) % 7;
            
            return $"{seasonNames[season]}{dayOfMonth} ({dayNames[dayOfWeek]})";
        }
    }
}