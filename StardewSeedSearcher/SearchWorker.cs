using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using StardewSeedSearcher.Data;
using StardewSeedSearcher.Features;

namespace StardewSeedSearcher
{
    internal static class SearchWorker
    {
        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            PropertyNameCaseInsensitive = true,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        };

        public static async Task RunAsync()
        {
            Console.InputEncoding = Encoding.UTF8;
            Console.OutputEncoding = Encoding.UTF8;
            TravelingCartData.Initialize();

            string? line;
            while ((line = await Console.In.ReadLineAsync()) != null)
            {
                if (string.IsNullOrWhiteSpace(line))
                    continue;

                WorkerResponse response;
                try
                {
                    var request = JsonSerializer.Deserialize<WorkerRequest>(line, JsonOptions)
                                  ?? throw new InvalidOperationException("Empty worker request.");

                    response = request.Type switch
                    {
                        "cartItems" => BuildCartItemsResponse(request.Id),
                        "search" => BuildSearchResponse(request),
                        _ => WorkerResponse.Failed(request.Id, $"Unknown worker request type: {request.Type}")
                    };
                }
                catch (Exception ex)
                {
                    response = WorkerResponse.Failed(null, ex.Message);
                }

                await Console.Out.WriteLineAsync(JsonSerializer.Serialize(response, JsonOptions));
                await Console.Out.FlushAsync();
            }
        }

        private static WorkerResponse BuildCartItemsResponse(string? id)
        {
            var items = TravelingCartData.OptimizedItems
                .Where(item => item.IsEligible)
                .Select(item => item.Name)
                .Concat(TravelingCartData.SkillBooks)
                .Distinct()
                .OrderBy(name => name)
                .ToList();

            return new WorkerResponse
            {
                Id = id,
                Type = "cartItems",
                Items = items
            };
        }

        private static WorkerResponse BuildSearchResponse(WorkerRequest workerRequest)
        {
            var request = workerRequest.Request
                          ?? throw new InvalidOperationException("Search request is required.");

            var features = ProgramWeb.InitializeFeatures(request);
            var sortedFeatures = features
                .Where(f => f.IsEnabled)
                .OrderBy(f => f.EstimateCost(request.UseLegacyRandom))
                .ToList();

            var passCounts = sortedFeatures.ToDictionary(f => f.Name, _ => 0);
            var matches = new List<WorkerSeedMatch>();
            var enabledFeatures = ProgramWeb.GetEnabledFeatures(request);
            var maxResults = workerRequest.MaxResults <= 0 ? int.MaxValue : workerRequest.MaxResults;
            var checkedCount = 0;

            for (long longSeed = workerRequest.StartSeed; longSeed <= workerRequest.EndSeed; longSeed++)
            {
                var seed = (int)longSeed;
                var allMatch = true;

                foreach (var feature in sortedFeatures)
                {
                    if (!feature.Check(seed, request.UseLegacyRandom))
                    {
                        allMatch = false;
                        break;
                    }

                    passCounts[feature.Name]++;
                }

                checkedCount++;

                if (allMatch)
                {
                    matches.Add(new WorkerSeedMatch
                    {
                        Seed = seed,
                        Details = ProgramWeb.CollectAllDetails(seed, request.UseLegacyRandom, features),
                        EnabledFeatures = enabledFeatures
                    });

                    if (matches.Count >= maxResults)
                        break;
                }

                if (longSeed == int.MaxValue)
                    break;
            }

            return new WorkerResponse
            {
                Id = workerRequest.Id,
                Type = "search",
                CheckedCount = checkedCount,
                Matches = matches,
                FeatureStats = sortedFeatures
                    .Select(feature => new WorkerFeatureStat { Name = feature.Name, PassCount = passCounts[feature.Name] })
                    .ToList()
            };
        }
    }

    internal sealed class WorkerRequest
    {
        [JsonPropertyName("id")]
        public string? Id { get; set; }

        [JsonPropertyName("type")]
        public string Type { get; set; } = "search";

        [JsonPropertyName("request")]
        public SearchRequest? Request { get; set; }

        [JsonPropertyName("startSeed")]
        public int StartSeed { get; set; }

        [JsonPropertyName("endSeed")]
        public long EndSeed { get; set; }

        [JsonPropertyName("maxResults")]
        public int MaxResults { get; set; }
    }

    internal sealed class WorkerResponse
    {
        [JsonPropertyName("id")]
        public string? Id { get; set; }

        [JsonPropertyName("type")]
        public string Type { get; set; } = "search";

        [JsonPropertyName("checkedCount")]
        public int CheckedCount { get; set; }

        [JsonPropertyName("matches")]
        public List<WorkerSeedMatch>? Matches { get; set; }

        [JsonPropertyName("featureStats")]
        public List<WorkerFeatureStat>? FeatureStats { get; set; }

        [JsonPropertyName("items")]
        public List<string>? Items { get; set; }

        [JsonPropertyName("error")]
        public string? Error { get; set; }

        public static WorkerResponse Failed(string? id, string error) => new()
        {
            Id = id,
            Type = "error",
            Error = error
        };
    }

    internal sealed class WorkerSeedMatch
    {
        [JsonPropertyName("seed")]
        public int Seed { get; set; }

        [JsonPropertyName("details")]
        public object? Details { get; set; }

        [JsonPropertyName("enabledFeatures")]
        public object? EnabledFeatures { get; set; }
    }

    internal sealed class WorkerFeatureStat
    {
        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;

        [JsonPropertyName("passCount")]
        public int PassCount { get; set; }
    }
}
