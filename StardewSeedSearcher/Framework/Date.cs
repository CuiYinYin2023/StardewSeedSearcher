namespace StardewSeedSearcher.Framework;

[Serializable]
public class Date
{
    private const int DaysPerSeason = 28;
    private const int DaysPerYear = 112;

    /// <summary>
    /// 年份信息
    /// </summary>
    public int Year { get; set; }

    /// <summary>
    /// 季节信息
    /// </summary>
    public Season Season { get; set; }

    /// <summary>
    /// 日期信息
    /// </summary>
    public int Day { get; set; }

    public Date() { }

    public Date(int year, Season season, int day)
    {
        Year = year;
        Season = season;
        Day = day;
    }

    /// <summary>
    /// 将日期信息转为总天数信息。
    /// </summary>
    public int ConvertToDaysPlayed()
    {
        return (Year - 1) * DaysPerYear + (int)Season * DaysPerSeason + Day;
    }

    public static Date ConvertFromDaysPlayed(int daysPlayed)
    {
        var totalDays = daysPlayed - 1;
        var year = totalDays / DaysPerYear + 1;
        var seasonIndex = totalDays % DaysPerYear / DaysPerSeason;
        var season = seasonIndex switch
        {
            0 => Season.Spring,
            1 => Season.Summer,
            2 => Season.Fall,
            _ => Season.Winter
        };
        var day = totalDays % DaysPerSeason + 1;
        return new Date(year, season, day);
    }

    public override string ToString() => $"第 {Year} 年{Season.ConvertToString()} {Day} 日：";
}