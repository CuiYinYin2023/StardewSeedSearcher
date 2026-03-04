namespace StardewSeedSearcher.Framework;

/// <remarks>索引如下：
/// <list type="table">
/// <item>0 - 春季</item>
/// <item>1 - 夏季</item>
/// <item>2 - 秋季</item>
/// <item>3 - 冬季</item>
/// </list>
/// </remarks>
public enum Season
{
    Spring = 0,
    Summer = 1,
    Fall = 2,
    Winter = 3
}

public static class SeasonUtilities
{
    public static string ConvertToString(this Season season) => season switch
    {
        Season.Spring => "春季",
        Season.Summer => "夏季",
        Season.Fall => "秋季",
        Season.Winter => "冬季",
        _ => throw new ArgumentOutOfRangeException(nameof(season), season, null)
    };
}