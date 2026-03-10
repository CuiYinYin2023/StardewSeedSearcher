using StardewSeedSearcher.Core;
using System;
using System.Collections.Generic;
using System.Linq;

namespace StardewSeedSearcher.Features
{
    public class WeatherConditionTracker
    {
        public int MaxRain { get; set; } = -1;
        public List<TopWeatherSeed> TopSeeds { get; set; } = new();
        public object LockObj = new object();
    }

    public class TopWeatherSeed
    {
        public int Seed { get; set; }
        public int[] Counts { get; set; }
    }

    public class WeatherCondition
    {
        public int Season { get; set; }
        public int StartDay { get; set; }
        public int EndDay { get; set; }
        public int MinRainDays { get; set; }

        public int AbsoluteStartDay => TimeHelper.DateToAbsoluteDay(1, Season, StartDay);
        public int AbsoluteEndDay => TimeHelper.DateToAbsoluteDay(1, Season, EndDay);
    }

    public class WeatherPredictor : ISearchFeature
    {
        public List<WeatherCondition> Conditions { get; set; } = new List<WeatherCondition>();
        public string Name => "天气预测";
        public bool IsEnabled { get; set; } = true;
        public int locationHash = HashHelper.GetHashFromString("location_weather");

        public WeatherConditionTracker[] Trackers { get; set; }
        public Action<object> OnMaxUpdate { get; set; }

        public bool Check(int gameID, bool useLegacyRandom)
        {
            if (Conditions.Count == 0) return true;

            if (Trackers == null)
            {
                lock (this)
                {
                    if (Trackers == null)
                    {
                        var temp = new WeatherConditionTracker[Conditions.Count];
                        for (int i = 0; i < Conditions.Count; i++) temp[i] = new WeatherConditionTracker();
                        Trackers = temp;
                    }
                }
            }

            int greenRainDay = GetGreenRainDay(gameID, useLegacyRandom);
            bool allMet = true;
            int[] actualCounts = new int[Conditions.Count];

            for (int i = 0; i < Conditions.Count; i++)
            {
                var condition = Conditions[i];
                int rainCount = 0;
                
                for (int day = condition.AbsoluteStartDay; day <= condition.AbsoluteEndDay; day++)
                {
                    int season = (int)condition.Season;
                    int dayOfMonth = ((day - 1) % 28) + 1;
                    
                    if (IsRainyDay(season, dayOfMonth, day, gameID, useLegacyRandom, greenRainDay))
                    {
                        rainCount++;
                    }
                }
                
                actualCounts[i] = rainCount;
                if (rainCount < condition.MinRainDays)
                {
                    allMet = false;
                }
            }

            for (int i = 0; i < Conditions.Count; i++)
            {
                var tracker = Trackers[i];
                int count = actualCounts[i];
                
                if (count >= tracker.MaxRain && count > 0)
                {
                    bool updated = false;
                    lock (tracker.LockObj)
                    {
                        if (count > tracker.MaxRain)
                        {
                            tracker.MaxRain = count;
                            tracker.TopSeeds.Clear();
                            tracker.TopSeeds.Add(new TopWeatherSeed { Seed = gameID, Counts = (int[])actualCounts.Clone() });
                            updated = true;
                        }
                        else if (count == tracker.MaxRain && tracker.TopSeeds.Count < 5)
                        {
                            tracker.TopSeeds.Add(new TopWeatherSeed { Seed = gameID, Counts = (int[])actualCounts.Clone() });
                            updated = true;
                        }
                    }

                    if (updated && OnMaxUpdate != null)
                    {
                        TopWeatherSeed[] snapshot;
                        int currentMax;
                        lock (tracker.LockObj)
                        {
                            snapshot = tracker.TopSeeds.ToArray();
                            currentMax = tracker.MaxRain;
                        }

                        OnMaxUpdate.Invoke(new
                        {
                            type = "weather_max",
                            conditionIndex = i,
                            maxRain = currentMax,
                            seeds = snapshot.Select(s => new { seed = s.Seed, counts = s.Counts }).ToArray()
                        });
                    }
                }
            }

            return allMet;
        }

        private bool IsRainyDay(int season, int dayOfMonth, int absoluteDay, int gameID, bool useLegacyRandom, int greenRainDay)
        {
            if (dayOfMonth == 1) return false; 

            switch (season)
            {
                case 0: 
                    return dayOfMonth switch 
                    {
                        2 => false, 3 => true, 4 => false, 5 => false, 13 => false, 24 => false, 
                        _ => IsRainyDaySpringFall(gameID, absoluteDay, useLegacyRandom)
                    };
                case 1: 
                    return dayOfMonth == greenRainDay || dayOfMonth switch 
                    {
                        11 => false, 13 => true, 28 => false, 26 => true, 
                        _ => IsRainyDaySummer(gameID, absoluteDay, useLegacyRandom, dayOfMonth)
                    };
                case 2: 
                    return dayOfMonth switch
                    {
                        16 => false, 27 => false, 
                        _ => IsRainyDaySpringFall(gameID, absoluteDay, useLegacyRandom) 
                    };
                default: 
                    return false;
            }
        }

        private int GetGreenRainDay(int gameID, bool useLegacyRandom)
        {
            int greenRainSeed = HashHelper.GetRandomSeed(777, gameID, 0, 0, 0, useLegacyRandom);
            Random greenRainRng = new Random(greenRainSeed);
            int[] greenRainDays = { 5, 6, 7, 14, 15, 16, 18, 23 };
            return greenRainDays[greenRainRng.Next(greenRainDays.Length)];
        }

        private bool IsRainyDaySpringFall(int gameID, int absoluteDay, bool useLegacyRandom)
        {
            int seed = HashHelper.GetRandomSeed(locationHash, gameID, absoluteDay - 1, 0, 0, useLegacyRandom);
            Random rng = new Random(seed);
            return rng.NextDouble() < 0.183;
        }

        private bool IsRainyDaySummer(int gameID, int absoluteDay, bool useLegacyRandom, int dayOfMonth)
        {
            int rainSeed = HashHelper.GetRandomSeed(
                absoluteDay - 1, gameID / 2, HashHelper.GetHashFromString("summer_rain_chance"), 0, 0, useLegacyRandom);
            Random rainRng = new Random(rainSeed);
            double rainChance = 0.12 + 0.003 * (dayOfMonth - 1);
            return rainRng.NextDouble() < rainChance;
        }

        public int EstimateCost(bool useLegacyRandom)
        {
            if (Conditions.Count == 0) return 0;
            int totalDays = 0;
            foreach (var condition in Conditions)
            {
                totalDays += condition.AbsoluteEndDay - condition.AbsoluteStartDay + 1;
            }
            return 56 + totalDays;
        }
        
        public (Dictionary<int, bool> weather, int greenRainDay) PredictWeatherWithDetail(int gameID, bool useLegacyRandom)
        {
            var weather = new Dictionary<int, bool>();
            int greenRainSeed = HashHelper.GetRandomSeed(777, gameID, 0, 0, 0, useLegacyRandom); 
            Random greenRainRng = new Random(greenRainSeed);
            int[] greenRainDays = { 5, 6, 7, 14, 15, 16, 18, 23 };
            int greenRainDay = greenRainDays[greenRainRng.Next(greenRainDays.Length)];

            for (int absoluteDay = 1; absoluteDay <= 84; absoluteDay++)
            {
                int season = (absoluteDay - 1) / 28;
                int dayOfMonth = ((absoluteDay - 1) % 28) + 1;
                weather[absoluteDay] = IsRainyDay(season, dayOfMonth, absoluteDay, gameID, useLegacyRandom, greenRainDay);
            }
            return (weather, greenRainDay);
        }

        public static WeatherDetailResult ExtractWeatherDetail(Dictionary<int, bool> weather, int greenRainDay)
        {
            var result = new WeatherDetailResult { GreenRainDay = greenRainDay };
            for (int day = 1; day <= 28; day++)
            {
                if (weather[day]) result.SpringRain.Add(day);
                if (weather[day + 28]) result.SummerRain.Add(day);
                if (weather[day + 56]) result.FallRain.Add(day);
            }
            return result;
        }
    }

    public class WeatherDetailResult
    {
        public List<int> SpringRain { get; set; } = new List<int>();
        public List<int> SummerRain { get; set; } = new List<int>();
        public List<int> FallRain { get; set; } = new List<int>();
        public int GreenRainDay { get; set; }
    }
}