using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using StardewSeedSearcher.Core;
using System.Threading.Channels;
using StardewSeedSearcher.Features;
using StardewSeedSearcher.Data;

namespace StardewSeedSearcher
{
    /// <summary>
    /// Web 版主程序：提供本地 Web API 服务
    /// </summary>
    public class ProgramWeb
    {
        // 存储活跃的 WebSocket 连接
        private static readonly ConcurrentDictionary<string, WebSocket> ActiveConnections = new();

        // 当前搜索的取消令牌，用于支持停止搜索功能
        private static CancellationTokenSource? _currentSearchCts = null;


        // 获得种子简介信息
        private static object CollectAllDetails(int seed, bool useLegacy, List<ISearchFeature> features)
        {
            WeatherDetailResult weatherDetail = null;
            List<object> fairyDays = null;
            List<object> mineChestDetails = null;
            List<object> monsterLevelDetails = null;
            object desertFestivalDetails = null; 
            List<object> cartMatches = null;
            
            foreach (var feature in features)
            {
                if (feature is WeatherPredictor predictor)
                {
                    var (weather, greenRainDay) = predictor.PredictWeatherWithDetail(seed, useLegacy);
                    weatherDetail = WeatherPredictor.ExtractWeatherDetail(weather, greenRainDay);
                }
                else if (feature is FairyPredictor fairyPredictor)
                {
                    fairyDays = fairyPredictor.GetFairyDays(seed, useLegacy);
                }
                else if (feature is MineChestPredictor chest)
                {
                    mineChestDetails = chest.GetDetails(seed, useLegacy);
                }
                else if (feature is MonsterLevelPredictor monsterLevel)
                {
                    monsterLevelDetails = monsterLevel.GetDetails(seed, useLegacy);
                }
                else if (feature is DesertFestivalPredictor desertFestival)
                {
                    desertFestivalDetails = desertFestival.GetDetails(seed, useLegacy);
                }
                else if (feature is TravelingCartPredictor cartPredictor)
                {
                    cartMatches = cartPredictor.GetCartMatches(seed, useLegacy);
                }
                // 未来添加更多
            }
            
            return new
            {
                weather = weatherDetail != null ? new
                {
                    springRain = weatherDetail.SpringRain,
                    summerRain = weatherDetail.SummerRain,
                    fallRain = weatherDetail.FallRain,
                    greenRainDay = weatherDetail.GreenRainDay
                } : null,
                fairy = fairyDays != null ? new { days = fairyDays } : null,
                mineChest = mineChestDetails,
                monsterLevel = monsterLevelDetails,
                desertFestival = desertFestivalDetails,
                cart = cartMatches != null ? new { matches = cartMatches } : null
                // 未来：
            };
        }

        public static void Main(string[] args)
        {
            // 拉取猪车物品列表
            TravelingCartData.Initialize();
            
            var builder = WebApplication.CreateBuilder(args);

            // 配置 CORS（允许本地 HTML 访问）
            builder.Services.AddCors(options =>
            {
                options.AddDefaultPolicy(policy =>
                {
                    policy.AllowAnyOrigin()
                          .AllowAnyMethod()
                          .AllowAnyHeader();
                });
            });

            // 配置 JSON 序列化（支持字符串枚举）
            builder.Services.ConfigureHttpJsonOptions(options =>
            {
                options.SerializerOptions.Converters.Add(new JsonStringEnumConverter());
            });

            // 禁用默认的日志输出（保持控制台整洁）
            builder.Logging.ClearProviders();
            builder.Logging.AddConsole();
            builder.Logging.SetMinimumLevel(LogLevel.Warning);

            var app = builder.Build();
            app.UseCors();

            // WebSocket 端点 - 用于实时推送进度
            app.UseWebSockets();
            app.Map("/ws", async context =>
            {
                if (context.WebSockets.IsWebSocketRequest)
                {
                    var ws = await context.WebSockets.AcceptWebSocketAsync();
                    var connectionId = Guid.NewGuid().ToString();
                    ActiveConnections[connectionId] = ws;

                    try
                    {
                        var buffer = new byte[1024 * 4];
                        while (ws.State == WebSocketState.Open)
                        {
                            await ws.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);
                        }
                    }
                    finally
                    {
                        ActiveConnections.TryRemove(connectionId, out _);
                    }
                }
            });

            // 猪车物品列表
            app.MapGet("/api/cart-items", () =>
            {
                var items = TravelingCartData.OptimizedItems
                    .Where(item => item.IsEligible) // 所有已经预处理过的、合格的物品（归类为前10个基础物品）
                    .Select(item => item.Name)
                    .Concat(TravelingCartData.SkillBooks) // 技能书
                    .Distinct()
                    .OrderBy(name => name)
                    .ToList();
                
                return Results.Ok(items);
            });

            app.MapGet("/api/seasons", () =>
            {
                // 利用 TimeHelper 循环生成季节 ID 和名称的列表
                var seasons = Enumerable.Range(0, 4).Select(id => new 
                { 
                    id = id, 
                    name = TimeHelper.GetSeasonName(id) 
                }).ToList();
                
                return Results.Ok(seasons);
            });

            // 搜索 API
            app.MapPost("/api/search", async (SearchRequest request) =>
            {
                var weatherDetailsCache = new ConcurrentDictionary<int, WeatherDetailResult>();
                var results = new List<int>();
                var stopwatch = Stopwatch.StartNew();
                long totalSeeds = request.EndSeed - request.StartSeed + 1;
                int checkedCount = 0;

                // 配置所有搜种功能
                var features = InitializeFeatures(request);

                var featurePassCounts = new ConcurrentDictionary<string, int>();
                foreach (var f in features) featurePassCounts[f.Name] = 0;

                /* 各 Worker 线程为 Channel 的生产者，负责暴力搜索并将结果传递给 Channel，Channel 消费者则负责广播结果（发送给前端更新进度）和提前终止搜索
                 * [Worker 线程 1] ──┐
                 * [Worker 线程 2] ──┤──► Channel<消息> ──► [单一消费者] ──► BroadcastMessage (async)
                 * [Worker 线程 N] ──┘
                 */
                var channel = Channel.CreateUnbounded<SearchMessage>(new UnboundedChannelOptions { SingleReader = true });

                // userStopCts：由 /api/stop 触发，即用户主动停止
                _currentSearchCts?.Cancel();
                var userStopCts = new CancellationTokenSource();
                _currentSearchCts = userStopCts;

                // limitCts：达到输出上限时触发，代表"自动停止"
                var limitCts = new CancellationTokenSource();

                // linkedCts：合并两者，搜索线程只需监听它
                var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(userStopCts.Token, limitCts.Token);

                // 消费者任务：从 Channel 读取数据并广播
                var consumerTask = Task.Run(async () =>
                {
                    try
                    {
                        while (await channel.Reader.WaitToReadAsync())
                        {
                            while (channel.Reader.TryRead(out var msg))
                            {
                                await BroadcastMessage(msg.Data);

                                // 如果达到搜索上限，通知生产者停止
                                if (msg.Type == "found" && results.Count >= request.OutputLimit)
                                {
                                    limitCts.Cancel();
                                }
                            }
                        }
                    }
                    catch (Exception ex) { Console.WriteLine($"Consumer Error: {ex.Message}"); }
                });

                // 发送开始消息
                await BroadcastMessage(new { type = "start", total = totalSeeds });
            
                var sortedFeatures = features.Where(f => f.IsEnabled).OrderBy(f=>f.EstimateCost(request.UseLegacyRandom)).ToList();

                // 多线程暴力搜索 （同时是 Channel 的生产者）
                await Task.Run(() =>
                {
                    // 动态成本计算
                    var sortedFeatures = features
                        .Where(f => f.IsEnabled) // 只看启用功能
                        .OrderBy(f => f.EstimateCost(request.UseLegacyRandom))
                        .ToList();

                    // 使用 Partitioner 减少线程调度开销
                    var rangePartitioner = Partitioner.Create(request.StartSeed, request.EndSeed + 1);

                    // 预留一个核心给 Channel 消费者以防止进度不能及时更新，单核时则不能避免
                    int degreeOfParallelism = Math.Max(1, Environment.ProcessorCount - 1);
                    var parallelOptions = new ParallelOptions
                    {
                        CancellationToken = linkedCts.Token,
                        MaxDegreeOfParallelism = degreeOfParallelism
                    };

                    try
                    {
                        Parallel.ForEach(rangePartitioner, parallelOptions, (range, state) =>
                        {
                            for (long seedL = range.Item1; seedL < range.Item2; seedL++)
                            {
                                // 提前终止
                                if (linkedCts.Token.IsCancellationRequested) break;

                                int seed = (int)seedL;

                                // 检查是否符合所有启用的功能条件
                                bool allMatch = true;
                                foreach (var feature in sortedFeatures)  // 使用排序后的列表
                                {
                                    if (!feature.Check(seed, request.UseLegacyRandom))
                                    {
                                        allMatch = false;
                                        break;
                                    }
                                    featurePassCounts.AddOrUpdate(feature.Name, 1, (key, val) => val + 1);
                                }

                                int localChecked = Interlocked.Increment(ref checkedCount);

                                if (allMatch)
                                {
                                    lock (results) // 保证 List 线程安全
                                    {
                                        if (results.Count < request.OutputLimit)
                                        {
                                            results.Add(seed);
                                            var details = CollectAllDetails(seed, request.UseLegacyRandom, features);
                                            channel.Writer.TryWrite(new SearchMessage("found", new
                                            {
                                                type = "found",
                                                seed,
                                                details,
                                                enabledFeatures = GetEnabledFeatures(request)
                                            }));
                                        }
                                        else { state.Stop(); break; }
                                    }
                                }

                                // 每1000个种子更新一次进度（避免过于频繁）
                                if (localChecked % 1000 == 0 || localChecked == totalSeeds)
                                {
                                    var stats = sortedFeatures.Select(f => new {
                                        name = f.Name,
                                        passCount = featurePassCounts[f.Name]
                                    }).ToList();

                                    double progress = (double)localChecked / totalSeeds * 100;
                                    double speed = localChecked / stopwatch.Elapsed.TotalSeconds;

                                    channel.Writer.TryWrite(new SearchMessage("progress", new
                                    {
                                        type = "progress",
                                        checkedCount = localChecked,
                                        total = totalSeeds,
                                        progress = Math.Round(progress, 2),
                                        speed = Math.Round(speed, 0),
                                        elapsed = Math.Round(stopwatch.Elapsed.TotalSeconds, 1),
                                        featureStats = stats
                                    }));
                                }
                            }
                        });
                    }
                    catch (OperationCanceledException) 
                    {
                        // 搜索完成，正常退出
                    }
                    finally
                    {
                        channel.Writer.Complete(); // 标记 Channel 完成
                    }
                });

                stopwatch.Stop();
                await consumerTask; // 等待所有广播发送完毕
                
                // 搜索完成过快会导致统计不精确
                var finalStats = sortedFeatures.Select(f => new 
                {
                    name = f.Name,
                    passCount = featurePassCounts[f.Name]
                }).ToList();

                // 发送完成消息
                // 发送最后一次精确的进度更新。
                // 这确保了即使用户的搜索范围小于100，或者搜索提前结束，
                // 前端的进度条和统计数据也能更新到循环终止时的确切状态。
                double finalProgress = (double)checkedCount / totalSeeds * 100;
                await BroadcastMessage(new
                {
                    type = "progress",
                    checkedCount,
                    progress = Math.Floor(finalProgress), // 这里也取整
                    speed = Math.Round(checkedCount / stopwatch.Elapsed.TotalSeconds, 0),
                    elapsed = Math.Round(stopwatch.Elapsed.TotalSeconds, 1),
                    featureStats = finalStats // 最终统计数据及时返回
                });
                
                // 广播"完成"消息
                await BroadcastMessage(new
                {
                    type = "complete",
                    totalFound = results.Count,
                    elapsed = Math.Round(stopwatch.Elapsed.TotalSeconds, 1),
                    cancelled = userStopCts.IsCancellationRequested
                });

                return Results.Ok(new { message = "Search started." });
            });

            // 停止搜索
            app.MapPost("/api/stop", () =>
            {
                _currentSearchCts?.Cancel();
                return Results.Ok(new { message = "Stop requested." });
            });

            // 健康检查
            app.MapGet("/api/health", () => Results.Ok(new { status = "ok", version = "1.0" }));

            // 根路径提示
            app.MapGet("/", () => Results.Content(@"
<!DOCTYPE html>
<html>
<head>
    <meta charset='utf-8'>
    <title>星露谷种子搜索器 API</title>
    <style>
        body {
            font-family: 'Segoe UI', sans-serif;
            max-width: 600px;
            margin: 50px auto;
            padding: 20px;
            background: linear-gradient(135deg, #667eea 0%, #764ba2 100%);
            color: white;
        }
        .card {
            background: white;
            color: #333;
            border-radius: 12px;
            padding: 30px;
            box-shadow: 0 10px 40px rgba(0,0,0,0.3);
        }
        h1 { margin-top: 0; color: #667eea; }
        .status { color: #4caf50; font-weight: bold; }
        code { background: #f5f5f5; padding: 2px 6px; border-radius: 3px; }
    </style>
</head>
<body>
    <div class='card'>
        <h1>🌾 星露谷种子搜索器 API</h1>
        <p>服务器运行 <span class='status'>正常</span>！</p>
        <p>请打开 <code>index.html</code> 开始使用。</p>
        <hr style='margin: 20px 0; border: none; border-top: 1px solid #eee;'>
        <p style='color: #666; font-size: 0.9em; margin: 0;'>
            端口: 5000 | 状态: 运行中<br>
            WebSocket: ws://localhost:5000/ws
        </p>
    </div>
</body>
</html>
", "text/html", Encoding.UTF8));

            // 启动提示
            Console.WriteLine("╔════════════════════════════════════════╗");
            Console.WriteLine("║  🌾 星露谷种子搜索器 - Web 服务启动  ║");
            Console.WriteLine("╚════════════════════════════════════════╝");
            Console.WriteLine();
            Console.WriteLine("✓ 服务器地址: http://localhost:5000");
            Console.WriteLine("✓ WebSocket: ws://localhost:5000/ws");
            Console.WriteLine();
            Console.WriteLine("📝 请打开 index.html 开始使用");
            Console.WriteLine("⚠️  按 Ctrl+C 停止服务器");
            Console.WriteLine();

            app.Run("http://localhost:5000");
        }

        /// <summary>
        /// 广播消息到所有连接的客户端
        /// </summary>
        private static async Task BroadcastMessage(object message)
        {
            var json = JsonSerializer.Serialize(message);
            var bytes = Encoding.UTF8.GetBytes(json);

            var tasks = ActiveConnections.Values
                .Where(ws => ws.State == WebSocketState.Open)
                .Select(ws => ws.SendAsync(
                    new ArraySegment<byte>(bytes),
                    WebSocketMessageType.Text,
                    true,
                    CancellationToken.None
                ));

            await Task.WhenAll(tasks);
        }

        /// <summary>
        /// 配置各搜种功能
        /// </summary>
        /// <param name="request"></param>
        /// <returns></returns>
        private static List<ISearchFeature> InitializeFeatures(SearchRequest request)
        {
            var features = new List<ISearchFeature>();
            // 配置天气功能
            if (request.WeatherConditions != null && request.WeatherConditions.Count > 0)
            {
                var predictor = new WeatherPredictor { IsEnabled = true };

                foreach (var conditionDto in request.WeatherConditions)
                {
                    var condition = new WeatherCondition
                    {
                        Season = conditionDto.Season,
                        StartDay = conditionDto.StartDay,
                        EndDay = conditionDto.EndDay,
                        MinRainDays = conditionDto.MinRainDays
                    };
                    predictor.Conditions.Add(condition);
                }

                features.Add(predictor);
            }

            // 配置仙子预测
            if (request.FairyConditions != null && request.FairyConditions.Count > 0)
            {
                var fairyPredictor = new FairyPredictor { IsEnabled = true };

                foreach (var conditionDto in request.FairyConditions)
                {
                    var condition = new FairyCondition
                    {
                        StartYear = conditionDto.StartYear,
                        StartSeason = conditionDto.StartSeason,
                        StartDay = conditionDto.StartDay,
                        EndYear = conditionDto.EndYear,
                        EndSeason = conditionDto.EndSeason,
                        EndDay = conditionDto.EndDay,
                        MinOccurrences = conditionDto.MinOccurrences
                    };
                    fairyPredictor.Conditions.Add(condition);
                }

                features.Add(fairyPredictor);
            }

            // 配置矿井宝箱功能
            if (request.MineChestConditions != null && request.MineChestConditions.Count > 0)
            {
                var mineChestPredictor = new MineChestPredictor { IsEnabled = true };

                foreach (var conditionDto in request.MineChestConditions)
                {
                    var condition = new MineChestPredictor.MineChestCondition
                    {
                        Floor = conditionDto.Floor,
                        ItemName = conditionDto.ItemName
                    };
                    mineChestPredictor.Conditions.Add(condition);
                }

                features.Add(mineChestPredictor);
            }

            // 配置怪物层预测
            if (request.MonsterLevelConditions != null && request.MonsterLevelConditions.Count > 0)
            {
                var monsterLevelPredictor = new MonsterLevelPredictor { IsEnabled = true };

                foreach (var conditionDto in request.MonsterLevelConditions)
                {
                    var condition = new MonsterLevelPredictor.MonsterLevelCondition
                    {
                        StartSeason=conditionDto.StartSeason,
                        EndSeason=conditionDto.EndSeason,

                        StartDay=conditionDto.StartDay,
                        EndDay=conditionDto.EndDay,

                        StartLevel=conditionDto.StartLevel,
                        EndLevel=conditionDto.EndLevel,
                    };
                    monsterLevelPredictor.Conditions.Add(condition);
                }

                features.Add(monsterLevelPredictor);
            }

            // 配置沙漠节预测
            if (request.DesertFestivalCondition != null &&
                (request.DesertFestivalCondition.RequireJas || request.DesertFestivalCondition.RequireLeah))
            {
                var desertFestivalPredictor = new DesertFestivalPredictor
                {
                    IsEnabled = true,
                    RequireJas = request.DesertFestivalCondition.RequireJas,
                    RequireLeah = request.DesertFestivalCondition.RequireLeah
                };

                features.Add(desertFestivalPredictor);
            }

            // 配置猪车预测
            if (request.CartConditions != null && request.CartConditions.Count > 0)
            {
                var cartPredictor = new TravelingCartPredictor { IsEnabled = true };

                foreach (var conditionDto in request.CartConditions)
                {
                    var condition = new CartCondition
                    {
                        StartYear = conditionDto.StartYear,
                        StartSeason = conditionDto.StartSeason,
                        StartDay = conditionDto.StartDay,
                        EndYear = conditionDto.EndYear,
                        EndSeason = conditionDto.EndSeason,
                        EndDay = conditionDto.EndDay,
                        ItemName = conditionDto.ItemName,
                        RequireQty5 = conditionDto.RequireQty5,
                        MinOccurrences = conditionDto.MinOccurrences < 1 ? 1 : conditionDto.MinOccurrences
                    };
                    cartPredictor.Conditions.Add(condition);
                }

                features.Add(cartPredictor);
            }

            return features;
        }

        /// <summary>
        /// 获取各功能的启用情况
        /// </summary>
        /// <param name="request"></param>
        /// <returns></returns>
        private static object GetEnabledFeatures(SearchRequest request) => new
        {
            weather = request.WeatherConditions?.Count > 0, // 如果为null则false，否则判断数量是否大于0
            weatherSeasons = request.WeatherConditions?.Select(c => c.Season).Distinct().ToList() ?? [],
            fairy = request.FairyConditions?.Count > 0,
            mineChest = request.MineChestConditions?.Count > 0,
            monsterLevel = request.MonsterLevelConditions?.Count > 0,
            desertFestival = request.DesertFestivalCondition != null && (request.DesertFestivalCondition.RequireJas || request.DesertFestivalCondition.RequireLeah),
            cart = request.CartConditions?.Count > 0
        };

    }

    /// <summary>
    /// 搜索请求模型
    /// </summary>
    public class SearchRequest
    {
        [JsonPropertyName("startSeed")]
        public int StartSeed { get; set; }

        [JsonPropertyName("endSeed")]
        public long EndSeed { get; set; }

        [JsonPropertyName("useLegacyRandom")]
        public bool UseLegacyRandom { get; set; }

        [JsonPropertyName("weatherConditions")]
        public List<WeatherConditionDto> WeatherConditions { get; set; } = new();
        
        [JsonPropertyName("fairyConditions")]
        public List<FairyConditionDto> FairyConditions { get; set; } = new();

        [JsonPropertyName("mineChestConditions")]
        public List<MineChestConditionDto> MineChestConditions { get; set; } = new();

        [JsonPropertyName("monsterLevelConditions")]
        public List<MonsterLevelConditionDto> MonsterLevelConditions { get; set; } = new();

        [JsonPropertyName("desertFestivalCondition")]
        public DesertFestivalConditionDto DesertFestivalCondition { get; set; }
        
        [JsonPropertyName("cartConditions")]
        public List<CartConditionDto> CartConditions { get; set; } = new();

        [JsonPropertyName("outputLimit")]
        public int OutputLimit { get; set; }
    }

    /// <summary>
    /// 天气条件 DTO（用于 JSON 反序列化）
    /// </summary>
    public class WeatherConditionDto
    {
        [JsonPropertyName("season")]
        public int Season { get; set; }

        [JsonPropertyName("startDay")]
        public int StartDay { get; set; }

        [JsonPropertyName("endDay")]
        public int EndDay { get; set; }

        [JsonPropertyName("minRainDays")]
        public int MinRainDays { get; set; }
    }

    public class FairyConditionDto
    {
        [JsonPropertyName("startYear")]
        public int StartYear { get; set; }

        [JsonPropertyName("startSeason")]
        public int StartSeason { get; set; }

        [JsonPropertyName("startDay")]
        public int StartDay { get; set; }

        [JsonPropertyName("endYear")]
        public int EndYear { get; set; }

        [JsonPropertyName("endSeason")]
        public int EndSeason { get; set; }

        [JsonPropertyName("endDay")]
        public int EndDay { get; set; }

        [JsonPropertyName("minOccurrences")]
        public int MinOccurrences { get; set; }
    }

    public class MineChestConditionDto
    {
        [JsonPropertyName("floor")]
        public int Floor { get; set; }

        [JsonPropertyName("itemname")]
        public string ItemName { get; set; }
    }

    public class MonsterLevelConditionDto
    {
        [JsonPropertyName("startSeason")]
        public int StartSeason { get; set; }

        [JsonPropertyName("endSeason")]
        public int EndSeason { get; set; }

        [JsonPropertyName("startDay")]
        public int StartDay { get; set; }

        [JsonPropertyName("endDay")]
        public int EndDay { get; set; }

        [JsonPropertyName("startLevel")]
        public int StartLevel { get; set; }

        [JsonPropertyName("endLevel")]
        public int EndLevel { get; set; }
    }

    public class DesertFestivalConditionDto
    {
        [JsonPropertyName("requireJas")]
        public bool RequireJas { get; set; }
        
        [JsonPropertyName("requireLeah")]
        public bool RequireLeah { get; set; }
    }
    
    public class CartConditionDto
    {

        [JsonPropertyName("startYear")]
        public int StartYear { get; set; }

        [JsonPropertyName("startSeason")]
        public int StartSeason { get; set; }

        [JsonPropertyName("startDay")]
        public int StartDay { get; set; }

        [JsonPropertyName("endYear")]
        public int EndYear { get; set; }

        [JsonPropertyName("endSeason")]
        public int EndSeason { get; set; }

        [JsonPropertyName("endDay")]
        public int EndDay { get; set; }

        [JsonPropertyName("itemName")]
        public string ItemName { get; set; }

        [JsonPropertyName("requireQty5")]
        public bool RequireQty5 { get; set; }

        [JsonPropertyName("minOccurrences")]
        public int MinOccurrences { get; set; } = 1;
    }

    record SearchMessage(string Type, object Data);
}