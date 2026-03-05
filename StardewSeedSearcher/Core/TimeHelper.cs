namespace StardewSeedSearcher.Core;

public static class TimeHelper
{
    private const int DaysPerSeason = 28;
    private const int SeasonsPerYear = 4;
    private const int DaysPerYear = DaysPerSeason * SeasonsPerYear;

    /// <summary>
    /// 转换为绝对天数（从 Year1 Spring1 = 0 开始）
    /// </summary>
    public static int DateToAbsoluteDay(int year, int season, int day)
    {
        // year 从 1 开始
        int yearOffset = (year - 1) * DaysPerYear;
        int seasonOffset = season * DaysPerSeason;
        int dayOffset = day;

        return yearOffset + seasonOffset + dayOffset;
    }

    /// <summary>
    /// 从绝对天数还原为 (Year, Season, Day)
    /// </summary>
    public static (int year, int season, int day) AbsoluteDaytoDate(int absoluteDay)
    {   
        int dayOfYear = absoluteDay % DaysPerYear;
        if (dayOfYear == 0) { dayOfYear = DaysPerYear; }

        int year = (absoluteDay - dayOfYear) / DaysPerYear + 1;

        int day = dayOfYear % DaysPerSeason;
        if (day == 0) { day = DaysPerSeason; }

        int season = (dayOfYear - day) / DaysPerSeason;

        return (year, season, day);
    }

    /// <summary>
    /// 获取季节的中文名称
    /// </summary>
    public static string GetSeasonName(int season)
    {
        return season switch
        {
            0 => "春",
            1 => "夏",
            2 => "秋",
            3 => "冬",
            _ => "未知"
        };
    }

    public struct GameDate
    {
        public int Year;
        public int Season;
        public int Day;

        public int ToAbsolute() =>
            TimeHelper.DateToAbsoluteDay(Year, Season, Day);
        
        // 季节中文名
        public string SeasonName => TimeHelper.GetSeasonName(Season);
    }
}