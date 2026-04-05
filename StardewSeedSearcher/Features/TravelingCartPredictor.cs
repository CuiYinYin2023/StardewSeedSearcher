using StardewSeedSearcher.Core;
using StardewSeedSearcher.Data;

namespace StardewSeedSearcher.Features
{
    // 商品
    public class CartItem
    {
        public string Category { get; set; }
        public string Name { get; set; }
        public int Quantity { get; set; }
        public int Price { get; set; }
    }
    
    // 单日猪车出售列表
    public class CartDayResult
    {
        public int Day { get; set; }
        public List<CartItem> Items { get; set; } = new List<CartItem>();
    }

    // 猪车搜索条件
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
        public int MinOccurrences { get; set; } = 1;

        public int AbsoluteStartDay => TimeHelper.DateToAbsoluteDay(StartYear, StartSeason, StartDay);
        public int AbsoluteEndDay => TimeHelper.DateToAbsoluteDay(EndYear, EndSeason, EndDay);

        public int totalCartDays => TravelingCartPredictor.CountCartDay(AbsoluteStartDay, AbsoluteEndDay);
    }

    // 猪车匹配结果（内部使用）
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

    /// <summary>
    /// 猪车预测器
    /// </summary>
    public class TravelingCartPredictor : ISearchFeature
    {
        public bool IsEnabled { get; set; }
        public List<CartCondition> Conditions { get; set; } = new();

        public string Name => "猪车预测";

        public bool Check(int seed, bool useLegacyRandom)
        {
            if (Conditions.Count == 0)
                return true;

            // --- 动态排序优化 ---
            // 按照分数从小到大排序，优先执行最稀有、范围最小的条件
            var sortedConditions = Conditions.OrderBy(EstimateCostPerCondition).ToList();

            int guaranteeSeed = HashHelper.GetRandomSeed(12 * seed, 0, 0, 0, 0, useLegacyRandom);
            int originalGuarantee = new Random(guaranteeSeed).Next(2, 31);

            // 所有条件都必须满足（AND）
            foreach (var condition in sortedConditions)
            {
                // 找到 minOccurrences 个匹配即可提前退出
                int matches = 0;

                for (int day = condition.AbsoluteStartDay; day <= condition.AbsoluteEndDay; day++)
                {
                    if (!IsCartDay(day)) 
                        continue;

                    // 调用高性能匹配
                    if (InternalDayMatch(seed, day, originalGuarantee, condition, useLegacyRandom))
                    {
                        matches++;
                        if (matches >= condition.MinOccurrences) 
                            break;
                    }
                }
                if (matches < condition.MinOccurrences) 
                    return false;
            }

            return true;
        }

        // 内部&外部动态排序使用
        private double EstimateCostPerCondition(CartCondition c)
        {
            // 每个猪车日期的成本：
            // - 遍历所有objects (约700个): 700次 Next()
            // - 10个物品价格+数量: 30次调用
            // - 红卷心菜保底: 最多3次
            // - 家具: 645次 Next() + 1次
            // - 季节特殊: 1次
            // - 技能书判断: 1次
            // 总计约：700 + 30 + 3 + 646 + 1 + 1 = 1381次
            int maxCalls = 1381;

            // 概率因子：如果要搜的是技能书，只有5%的种子符合。基础物品需调用730次随机数
            double calls = TravelingCartData.SkillBookSet.Contains(c.ItemName) ? 0.05 * maxCalls : 730;
            
            // 时间跨度（天数）
            int totalDays = 0;
            for (int day = c.AbsoluteStartDay; day <= c.AbsoluteEndDay; day++)
                if (IsCartDay(day))
                    totalDays ++;
            
            // 分数 = 跨度 * 概率
            return totalDays * calls;
        }


        public int EstimateCost(bool useLegacyRandom)
        {
            if (Conditions.Count == 0) return 0;

            // 估算每个条件的成本
            double totalCost = 0;

            foreach (var condition in Conditions)
            {
                totalCost += EstimateCostPerCondition(condition);
            }

            return (int)totalCost;
        }

        /// <summary>
        /// 判断指定天是否有猪车
        /// </summary>
        public static bool IsCartDay(int day)
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

        public static int CountCartDay(int absoluteStartDay, int absoluteEndDay)
        {
            int totalCartDays = 0;

            for (int day = absoluteStartDay; day <= absoluteEndDay; day++)
            {
                if (IsCartDay(day))
                {
                    totalCartDays++;
                }
            }

            return totalCartDays;
        }


        /// <summary>
        /// 获取种子简介信息（返回所有匹配项）
        /// </summary>
        public List<object> GetCartMatches(int seed, bool useLegacyRandom)
        {
            var cartMatches = new List<object>();

            foreach (var condition in Conditions)
            {
                var matches = FindAllMatches(seed, condition.AbsoluteStartDay, condition.AbsoluteEndDay,
                    condition.ItemName, condition.RequireQty5, useLegacyRandom);

                cartMatches.AddRange(matches.Cast<object>());
            }

            return cartMatches;
        }

        /// <summary>
        /// 在日期范围内查找所有匹配的物品出现记录，找到 stopAt 个后提前退出
        /// </summary>
        private List<CartDayMatch> FindAllMatches(int seed, int startDay, int endDay,
            string itemName, bool requireQty5, bool useLegacyRandom, int stopAt = int.MaxValue)
        {
            var matches = new List<CartDayMatch>();
            int gameID = seed;

            // 计算红叶卷心菜保底（即使不用，也要初始化）
            int guaranteeSeed = HashHelper.GetRandomSeed(12 * gameID, 0, 0, 0, 0, useLegacyRandom);
            Random rngGuarantee = new Random(guaranteeSeed);
            int originalGuarantee = rngGuarantee.Next(2, 31);

            // 遍历日期范围内的所有猪车日期
            for (int day = startDay; day <= endDay; day++)
            {
                if (!IsCartDay(day)) continue;

                // 获取该天全量数据（包含价格和数量）
                var result = PredictCartDay(seed, day, originalGuarantee, useLegacyRandom);

                foreach (var item in result.Items)
                {
                    if (item.Name != itemName) 
                        continue;

                    if (requireQty5 && item.Quantity != 5) 
                        continue;
                    
                    var dateInfo = TimeHelper.AbsoluteDaytoDate(day);

                    matches.Add(new CartDayMatch
                    {
                        Year = dateInfo.year, 
                        Season = dateInfo.season, 
                        Day = dateInfo.day,
                        AbsoluteDay = day, 
                        ItemName = item.Name, 
                        Quantity = item.Quantity, 
                        Price = item.Price
                    });

                    break; 
                }

                if (matches.Count >= stopAt) 
                    break;
            }
            return matches;
        }
        
        /// <summary>
        /// 预测指定日期的猪车内容
        /// </summary>
        private CartDayResult PredictCartDay(int gameID, int day, int originalGuarantee, bool useLegacyRandom)
        {
            var result = new CartDayResult
            {
                Day = day
            };
            
            // 1. 创建主RNG
            int seed = HashHelper.GetRandomSeed(day, gameID / 2, 0, 0, 0, useLegacyRandom);
            Random rng = new Random(seed);
            
            // 2. 获取10个基础物品
            List<int> selectedItemKeys = GetRandomItems(rng);
            
            bool seenRareSeed = false;
            
            for (int i = 0; i < selectedItemKeys.Count; i++)
            {
                var item = TravelingCartData.OptimizedItems[selectedItemKeys[i]];
                
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
        /// 优化版搜索逻辑
        /// </summary>
        private bool InternalDayMatch(int seed, int day, int originalGuarantee, CartCondition cond, bool useLegacyRandom)
        {
            // 1. 如果搜的是技能书，先判定当日是否会出现技能书（5%概率）。如果不出现，直接排除当日
            bool isBookSearch = TravelingCartData.SkillBookSet.Contains(cond.ItemName);
            if (isBookSearch)
            {
                int skillHash = HashHelper.GetHashFromString("travelerSkillBook");
                int skillSeed = HashHelper.GetRandomSeed(skillHash, seed, day, 0, 0, useLegacyRandom);
                if (new Random(skillSeed).NextDouble() >= 0.05) return false; // 直接退出
            }

            // 2. 果不是技能书/当日有技能书，再开始创建主RNG
            int mainSeed = HashHelper.GetRandomSeed(day, seed / 2, 0, 0, 0, useLegacyRandom);
            Random rng = new Random(mainSeed);

            // 3. 基础物品生成
            // 猪车洗牌算法：先把所有物品赋予一个随机key，再将排序前10的物品放入出售列表
            // 这里使用topKeys维护，避免排序整个大列表
            var allItems = TravelingCartData.OptimizedItems;
            Span<int> topKeys = stackalloc int[10];
            Span<int> topIndices = stackalloc int[10];
            topKeys.Fill(int.MaxValue);

            for (int i = 0; i < allItems.Length; i++)
            {
                int randomKey = rng.Next(); // 必须调用，保证同步
                if (!allItems[i].IsEligible) 
                    continue; // 不符合资格的，不入选

                if (randomKey < topKeys[9]) // 手动维护前10名
                {
                    int j = 8;
                    while (j >= 0 && topKeys[j] > randomKey) { topKeys[j + 1] = topKeys[j]; topIndices[j + 1] = topIndices[j]; j--; }
                    topKeys[j + 1] = randomKey; topIndices[j + 1] = i;
                }
            }

            // 3. 结果判定
            bool seenRareSeed = false;
            if (!isBookSearch)
            {
                for (int i = 0; i < 10; i++)
                {
                    var item = allItems[topIndices[i]];
                    if (item.Name == "Rare Seed") seenRareSeed = true;

                    if (item.Name == cond.ItemName)
                    {
                        // 仅在名字匹配时计算数量
                        for (int k = 0; k < i; k++) 
                        { 
                            rng.Next(1, 11); 
                            rng.Next(3, 6); 
                            rng.NextDouble(); 
                        } // 消耗前 i-1 个

                        rng.Next(1, 11); 
                        rng.Next(3, 6); // 当前价格

                        int qty = (rng.NextDouble() < 0.1) ? 5 : 1; // 当前数量
                        
                        if (!cond.RequireQty5 || qty == 5) 
                            return true;

                        return false; 
                    }
                }
                return false; // 基础物品没中
            }

            // 4. 只有搜书且通过了5%概率，才跑后面最重的同步逻辑和家具循环
            for (int i = 0; i < 10; i++) { 
                if (allItems[topIndices[i]].Name == "Rare Seed") 
                    seenRareSeed = true;
                rng.Next(1, 11); rng.Next(3, 6); rng.NextDouble(); 
            }

            if (CalculateVisitsRemaining(day, originalGuarantee) == 0) 
            { 
                rng.Next(1, 11); 
                rng.Next(3, 6); 
                rng.NextDouble(); 
            }

            for (int i = 0; i < 645; i++) 
                rng.Next(); // 645次家具消耗

            rng.Next(1, 11);

            if ((day - 1) / 28 < 2 && !seenRareSeed) 
                rng.NextDouble();
            
            string bookName = TravelingCartData.SkillBooks[rng.Next(TravelingCartData.SkillBooks.Length)];
            return bookName == cond.ItemName;
        }
                
        /// <summary>
        /// 获取随机物品（核心算法）
        /// </summary>
        private List<int> GetRandomItems(Random rng)
        {
            // 使用预处理好的 OptimizedItems 数组
            var allItems = TravelingCartData.OptimizedItems;
            
            // 手动维护前 10 个最小 Key 的物品索引
            Span<int> topKeys = stackalloc int[10];
            Span<int> topIndices = stackalloc int[10];
            topKeys.Fill(int.MaxValue);

            for (int i = 0; i < allItems.Length; i++)
            {
                // 【关键】每一件物品都必须消耗一次 RNG，以保证与游戏同步
                int randomKey = rng.Next();

                // 只有 eligible 的物品才参与 Top 10 竞争
                if (!allItems[i].IsEligible) 
                    continue;

                // 手动插入排序：如果当前随机 Key 比 Top-10 里的最大值还小
                if (randomKey < topKeys[9])
                {
                    int j = 8;
                    // 找到它在 Top-10 中的位置
                    while (j >= 0 && topKeys[j] > randomKey)
                    {
                        topKeys[j + 1] = topKeys[j];
                        topIndices[j + 1] = topIndices[j];
                        j--;
                    }
                    topKeys[j + 1] = randomKey;
                    topIndices[j + 1] = i;
                }
            }

            // 将前 10 名的物品名称提取出来
            var result = new List<int>(10);
            for (int i = 0; i < 10; i++)
            {
                result.Add(topIndices[i]);
            }
            
            return result;            
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
    }
}