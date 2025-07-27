namespace BinanceFuturesTrader.Models
{
    /// <summary>
    /// 执行状态常量定义 - 用于JSON文件和UI显示
    /// </summary>
    public static class StatusConstants
    {
        /// <summary>
        /// 等待触发状态（英文）
        /// </summary>
        public const string Waiting = "waiting";
        
        /// <summary>
        /// 已执行状态（英文）
        /// </summary>
        public const string Executed = "executed";
        
        /// <summary>
        /// 执行中状态（英文）
        /// </summary>
        public const string Executing = "executing";
        
        /// <summary>
        /// 执行失败状态（英文）
        /// </summary>
        public const string Failed = "failed";
        
        /// <summary>
        /// 模拟执行状态（英文）
        /// </summary>
        public const string SimulationExecuted = "simulation_executed";
        
        // UI显示符号
        
        /// <summary>
        /// 等待符号：横线
        /// </summary>
        public const string WaitingSymbol = "-";
        
        /// <summary>
        /// 已执行符号：对勾
        /// </summary>
        public const string ExecutedSymbol = "√";
        
        // 中文状态（向后兼容）
        
        /// <summary>
        /// 未触发（中文）
        /// </summary>
        public const string WaitingChinese = "未触发";
        
        /// <summary>
        /// 已执行（中文）
        /// </summary>
        public const string ExecutedChinese = "已执行";
        
        /// <summary>
        /// 执行中（中文）
        /// </summary>
        public const string ExecutingChinese = "执行中";
        
        /// <summary>
        /// 将中文状态转换为英文状态
        /// </summary>
        public static string ConvertChineseToEnglish(string chineseStatus)
        {
            return chineseStatus switch
            {
                WaitingChinese or "未触发" => Waiting,
                ExecutedChinese or "已执行" => Executed,
                ExecutingChinese or "执行中" => Executing,
                "执行失败" => Failed,
                "模拟执行" => SimulationExecuted,
                WaitingSymbol or "-" => WaitingSymbol,
                ExecutedSymbol or "√" => ExecutedSymbol,
                _ => chineseStatus // 如果已经是英文或其他格式，保持不变
            };
        }
        
        /// <summary>
        /// 将英文状态转换为中文状态（向后兼容）
        /// </summary>
        public static string ConvertEnglishToChinese(string englishStatus)
        {
            return englishStatus switch
            {
                Waiting => WaitingChinese,
                Executed => ExecutedChinese,
                Executing => ExecutingChinese,
                Failed => "执行失败",
                SimulationExecuted => "模拟执行",
                WaitingSymbol => "-",
                ExecutedSymbol => "√",
                _ => englishStatus
            };
        }
        
        /// <summary>
        /// 获取状态的显示符号
        /// </summary>
        public static string GetStatusSymbol(string status)
        {
            return status switch
            {
                Waiting or WaitingChinese or "未触发" or WaitingSymbol => WaitingSymbol,
                Executed or ExecutedChinese or "已执行" or ExecutedSymbol => ExecutedSymbol,
                _ => WaitingSymbol
            };
        }
        
        /// <summary>
        /// 检查状态是否为已执行
        /// </summary>
        public static bool IsExecuted(string status)
        {
            return status == Executed || status == ExecutedChinese || status == "已执行" || status == ExecutedSymbol;
        }
        
        /// <summary>
        /// 检查状态是否为等待触发
        /// </summary>
        public static bool IsWaiting(string status)
        {
            return status == Waiting || status == WaitingChinese || status == "未触发" || status == WaitingSymbol;
        }
        
        /// <summary>
        /// 获取状态的英文显示文本（用于UI显示）
        /// </summary>
        public static string GetEnglishDisplayText(string status)
        {
            return status switch
            {
                WaitingChinese or "未触发" or WaitingSymbol => Waiting,
                ExecutedChinese or "已执行" or ExecutedSymbol => Executed,
                ExecutingChinese or "执行中" => Executing,
                _ => ConvertChineseToEnglish(status)
            };
        }
    }
} 