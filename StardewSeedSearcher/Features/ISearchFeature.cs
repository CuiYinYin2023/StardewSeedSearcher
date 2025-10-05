namespace StardewSeedSearcher.Features
{
    /// <summary>
    /// 搜种器功能接口
    /// 所有筛选功能都实现此接口
    /// </summary>
    public interface ISearchFeature
    {
        /// <summary>功能名称</summary>
        string Name { get; }

        /// <summary>是否启用此功能</summary>
        bool IsEnabled { get; set; }

        /// <summary>
        /// 检查种子是否符合此功能的筛选条件
        /// </summary>
        /// <param name="gameID">游戏种子</param>
        /// <param name="useLegacyRandom">是否使用旧随机模式</param>
        /// <returns>true 表示符合条件，false 表示不符合</returns>
        bool Check(int gameID, bool useLegacyRandom);

        /// <summary>估算最坏情况的随机数调用次数，用于动态成本计算</summary>
        int EstimateCost(bool useLegacyRandom);

        /// <summary>
        /// 获取功能的配置说明
        /// </summary>
        string GetConfigDescription();
    }
}