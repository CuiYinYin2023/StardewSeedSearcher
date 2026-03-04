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
    public class TravelingCartPredictor : ISearchFeature
    {
        public bool IsEnabled { get; set; }
        public List<CartCondition> Conditions { get; set; } = new();

        public string Name => "猪车预测";

        public string GetConfigDescription()
        {
            return $"{Conditions.Count} 个条件";
        }

        public bool Check(int seed, bool useLegacyRandom)
        {
            if (Conditions.Count == 0)
                return true;

            // 所有条件都必须满足（AND）
            foreach (var condition in Conditions)
            {
                // 转换为绝对天数
                int startDay = TimeHelper.DateToAbsoluteDay(condition.StartYear, condition.StartSeason, condition.StartDay);
                int endDay = TimeHelper.DateToAbsoluteDay(condition.EndYear, condition.EndSeason, condition.EndDay);

                string searchTerm = condition.ItemName;
                
                // 查找第一个匹配的日期
                var match = FindFirstMatch(seed, startDay, endDay, 
                    searchTerm, condition.RequireQty5, useLegacyRandom);

                // 如果没找到匹配，淘汰这个种子
                if (match == null)
                {
                    return false;
                }
            }

            return true;
        }

        private int MapSeasonToInteger(string seasonName)
        {
            return seasonName.ToLower() switch
            {
                "spring" => 0,
                "summer" => 1,
                "fall" => 2,
                "winter" => 3,
                _ => 0
            };
        }
        public int EstimateCost(bool useLegacyRandom)
        {
            if (Conditions.Count == 0) return 0;

            // 估算每个条件的成本
            int totalCost = 0;

            // 每个猪车日期的成本：
            // - 遍历所有objects (约700个): 700次 Next()
            // - 10个物品价格+数量: 30次调用
            // - 红卷心菜保底: 最多3次
            // - 家具: 645次 Next() + 1次
            // - 季节特殊: 1次
            // - 技能书判断: 1次
            // 总计约：700 + 30 + 3 + 646 + 1 + 1 = 1381次
            int callsPerDay = 1381;

            foreach (var condition in Conditions)
            {
                int startDay = TimeHelper.DateToAbsoluteDay(condition.StartYear, condition.StartSeason, condition.StartDay);
                int endDay = TimeHelper.DateToAbsoluteDay(condition.EndYear, condition.EndSeason, condition.EndDay);

                // 计算范围内有多少个猪车日期
                int cartDayCount = 0;
                for (int day = startDay; day <= endDay; day++)
                {
                    if (IsCartDay(day))
                    {
                        cartDayCount++;
                    }
                }

                // 最坏情况：遍历整个范围
                totalCost += cartDayCount * callsPerDay;
            }

            return totalCost;
        }

        /// <summary>
        /// 判断指定天是否有猪车
        /// </summary>
        private bool IsCartDay(int day)
        {
            int dayOfWeek = day % 7;  
            int dayOfYear = day % 112;

            // 普通周五和周日
            if (dayOfWeek == 5 || dayOfWeek == 0) return true;

            // 沙漠节（春15-17）
            if (dayOfYear >= 15 && dayOfYear <= 17) return true;

            // 夜市（冬15-17）
            if (dayOfYear >= 99 && dayOfYear <= 101) return true;

            return false;
        }

        /// <summary>
        /// 获取种子简介信息
        /// </summary>
        public List<object> GetCartMatches(int seed, bool useLegacyRandom)
        {
            var cartMatches = new List<object>();

            foreach (var condition in Conditions)
            {
                int startDay = TimeHelper.DateToAbsoluteDay(condition.StartYear, condition.StartSeason, condition.StartDay);
                int endDay = TimeHelper.DateToAbsoluteDay(condition.EndYear, condition.EndSeason, condition.EndDay);

                var match = FindFirstMatch(seed, startDay, endDay,
                    condition.ItemName, condition.RequireQty5, useLegacyRandom);

                if (match != null)
                {
                    cartMatches.Add(match);
                }
            }

            return cartMatches;
        }

        /// <summary>
        /// 在日期范围内查找第一个匹配的物品
        /// </summary>
        private CartDayMatch? FindFirstMatch(int seed, int startDay, int endDay, 
            string itemName, bool requireQty5, bool useLegacyRandom)
        {
            int gameID = seed;
            
            // 计算红卷心菜保底（即使不用，也要初始化）
            int guaranteeSeed = HashHelper.GetRandomSeed(12 * gameID, 0, 0, 0, 0, useLegacyRandom);
            Random rngGuarantee = new Random(guaranteeSeed);
            int originalGuarantee = rngGuarantee.Next(2, 31);
            
            // 遍历日期范围内的所有猪车日期
            for (int day = startDay; day <= endDay; day++)
            {
                if (!IsCartDay(day)) continue;
                
                // 预测这天的猪车
                var result = PredictCartDay(gameID, day, originalGuarantee, useLegacyRandom);
                
                // 检查是否匹配
                foreach (var item in result.Items)
                {
                    // 跳过非物品项（如"还需X次访问"）
                    if (item.Quantity == 0) continue;
                    
                    // 检查数量要求
                    // 技能书的数量是-1，不能要求数量为5
                    if (requireQty5 && item.Quantity != 5) continue;
                    
                    // 检查物品名称
                    if (item.Name != itemName) continue;
                    
                    // 找到匹配！
                    var dateInfo = TimeHelper.AbsoluteDaytoDate(day);
                    return new CartDayMatch
                    {
                        Year = dateInfo.year,
                        Season = dateInfo.season,
                        Day = dateInfo.day,
                        AbsoluteDay = day, // 用于前端排序
                        ItemName = item.Name,
                        Quantity = item.Quantity,
                        Price = item.Price 
                    };
                }
            }
            
            // 未找到匹配
            return null;
        }

        /// <summary>
        /// 预测指定种子在春季的所有猪车内容（测试用）
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
            
            // ===== 5. 季节特殊物品（消耗RNG，不输出）=====
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
                    Quantity = -1,  // 无限
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
            // 步骤1: 给每个物品生成随机key
            var itemsWithRandomKey = new List<(int randomKey, string objectKey)>();
            
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
            
            // 步骤2: 按随机key排序
            var sortedItems = itemsWithRandomKey.OrderBy(x => x.randomKey).ToList();
            
            // 步骤3: 取前10个
            return sortedItems.Take(10).Select(x => x.objectKey).ToList();
        }
        
        private int CalculateVisitsRemaining(int day, int originalGuarantee)
        {
            int visitsNow = originalGuarantee - (day / 7) - ((day + 2) / 7);
            
            // 沙漠节不调整，可能因为默认状态还没开沙漠
            
            // 夜市调整（冬15-17是day 99-101）
            if (day >= 99) visitsNow--;
            if (day >= 100) visitsNow--;
            if (day >= 101) visitsNow--;
            
            return visitsNow;
        }
        
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

    /// <summary>
    /// 猪车搜索条件
    /// </summary>
    public class CartCondition
    {
        public int StartYear { get; set; }
        public int StartSeason { get; set; }  // 0-3
        public int StartDay { get; set; }
        public int EndYear { get; set; }
        public int EndSeason { get; set; }  // 0-3
        public int EndDay { get; set; }
        public string ItemName { get; set; }
        public bool RequireQty5 { get; set; }
    }

    /// <summary>
    /// 猪车匹配结果（内部使用）
    /// </summary>
    public class CartDayMatch
    {
        public int Year { get; set; }
        public int Season { get; set; } // 整数 0-3
        public int Day { get; set; }    // 1-28
        public int AbsoluteDay { get; set; } // 用于种子简介排序
        public string ItemName { get; set; }
        public int Quantity { get; set; }
        public int Price { get; set; }
    }
}