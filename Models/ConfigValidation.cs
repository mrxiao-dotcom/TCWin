using System;
using System.Collections.Generic;
using System.Linq;
using BinanceFuturesTrader.Models;

namespace BinanceFuturesTrader.Models
{
    /// <summary>
    /// 配置验证结果
    /// </summary>
    public class ConfigValidationResult
    {
        /// <summary>
        /// 验证是否通过
        /// </summary>
        public bool IsValid { get; set; }
        
        /// <summary>
        /// 验证错误列表
        /// </summary>
        public List<ValidationError> Errors { get; set; } = new();
        
        /// <summary>
        /// 验证警告列表
        /// </summary>
        public List<ValidationWarning> Warnings { get; set; } = new();
        
        /// <summary>
        /// 配置建议列表
        /// </summary>
        public List<ConfigSuggestion> Suggestions { get; set; } = new();
        
        /// <summary>
        /// 自动修复列表
        /// </summary>
        public List<ConfigFix> AutoFixes { get; set; } = new();
        
        /// <summary>
        /// 验证结果摘要
        /// </summary>
        public string Summary => 
            $"验证结果: {(IsValid ? "通过" : "失败")} | 错误: {Errors.Count} | 警告: {Warnings.Count} | 建议: {Suggestions.Count}";
        
        /// <summary>
        /// 是否存在问题
        /// </summary>
        public bool HasIssues => Errors.Any() || Warnings.Any();
        
        /// <summary>
        /// 最高严重程度
        /// </summary>
        public ValidationSeverity MaxSeverity
        {
            get
            {
                if (Errors.Any(e => e.Severity == ValidationSeverity.Critical))
                    return ValidationSeverity.Critical;
                if (Errors.Any())
                    return ValidationSeverity.Error;
                if (Warnings.Any())
                    return ValidationSeverity.Warning;
                return ValidationSeverity.Info;
            }
        }
    }

    /// <summary>
    /// 验证错误
    /// </summary>
    public class ValidationError
    {
        /// <summary>
        /// 错误代码
        /// </summary>
        public string ErrorCode { get; set; } = string.Empty;
        
        /// <summary>
        /// 配置项名称
        /// </summary>
        public string ConfigKey { get; set; } = string.Empty;
        
        /// <summary>
        /// 当前值
        /// </summary>
        public object? CurrentValue { get; set; }
        
        /// <summary>
        /// 错误消息
        /// </summary>
        public string Message { get; set; } = string.Empty;
        
        /// <summary>
        /// 详细信息
        /// </summary>
        public string? Details { get; set; }
        
        /// <summary>
        /// 修复建议
        /// </summary>
        public string? FixSuggestion { get; set; }
        
        /// <summary>
        /// 严重程度
        /// </summary>
        public ValidationSeverity Severity { get; set; } = ValidationSeverity.Error;
        
        /// <summary>
        /// 是否可以自动修复
        /// </summary>
        public bool CanAutoFix { get; set; }
    }

    /// <summary>
    /// 验证警告
    /// </summary>
    public class ValidationWarning
    {
        /// <summary>
        /// 警告代码
        /// </summary>
        public string WarningCode { get; set; } = string.Empty;
        
        /// <summary>
        /// 配置项名称
        /// </summary>
        public string ConfigKey { get; set; } = string.Empty;
        
        /// <summary>
        /// 当前值
        /// </summary>
        public object? CurrentValue { get; set; }
        
        /// <summary>
        /// 警告消息
        /// </summary>
        public string Message { get; set; } = string.Empty;
        
        /// <summary>
        /// 推荐值
        /// </summary>
        public object? RecommendedValue { get; set; }
        
        /// <summary>
        /// 风险描述
        /// </summary>
        public string? RiskDescription { get; set; }
        
        /// <summary>
        /// 严重程度
        /// </summary>
        public ValidationSeverity Severity { get; set; } = ValidationSeverity.Warning;
    }

    /// <summary>
    /// 配置建议
    /// </summary>
    public class ConfigSuggestion
    {
        /// <summary>
        /// 建议代码
        /// </summary>
        public string SuggestionCode { get; set; } = string.Empty;
        
        /// <summary>
        /// 配置项名称
        /// </summary>
        public string ConfigKey { get; set; } = string.Empty;
        
        /// <summary>
        /// 当前值
        /// </summary>
        public object? CurrentValue { get; set; }
        
        /// <summary>
        /// 建议值
        /// </summary>
        public object? SuggestedValue { get; set; }
        
        /// <summary>
        /// 建议原因
        /// </summary>
        public string Reason { get; set; } = string.Empty;
        
        /// <summary>
        /// 预期效果
        /// </summary>
        public string? ExpectedBenefit { get; set; }
        
        /// <summary>
        /// 优先级
        /// </summary>
        public SuggestionPriority Priority { get; set; } = SuggestionPriority.Medium;
    }

    /// <summary>
    /// 配置自动修复
    /// </summary>
    public class ConfigFix
    {
        /// <summary>
        /// 修复代码
        /// </summary>
        public string FixCode { get; set; } = string.Empty;
        
        /// <summary>
        /// 配置项名称
        /// </summary>
        public string ConfigKey { get; set; } = string.Empty;
        
        /// <summary>
        /// 原始值
        /// </summary>
        public object? OriginalValue { get; set; }
        
        /// <summary>
        /// 修复后的值
        /// </summary>
        public object? FixedValue { get; set; }
        
        /// <summary>
        /// 修复原因
        /// </summary>
        public string FixReason { get; set; } = string.Empty;
        
        /// <summary>
        /// 修复时间
        /// </summary>
        public DateTime FixTime { get; set; } = DateTime.Now;
    }

    /// <summary>
    /// 验证严重程度
    /// </summary>
    public enum ValidationSeverity
    {
        Info = 0,       // 信息
        Warning = 1,    // 警告
        Error = 2,      // 错误
        Critical = 3    // 严重错误
    }

    /// <summary>
    /// 建议优先级
    /// </summary>
    public enum SuggestionPriority
    {
        Low = 0,        // 低优先级
        Medium = 1,     // 中等优先级
        High = 2,       // 高优先级
        Critical = 3    // 关键优先级
    }

    /// <summary>
    /// 配置验证规则
    /// </summary>
    public class ConfigValidationRule
    {
        /// <summary>
        /// 规则ID
        /// </summary>
        public string RuleId { get; set; } = string.Empty;
        
        /// <summary>
        /// 规则名称
        /// </summary>
        public string RuleName { get; set; } = string.Empty;
        
        /// <summary>
        /// 规则描述
        /// </summary>
        public string Description { get; set; } = string.Empty;
        
        /// <summary>
        /// 适用的配置项
        /// </summary>
        public string[] ApplicableConfigs { get; set; } = Array.Empty<string>();
        
        /// <summary>
        /// 验证函数
        /// </summary>
        public Func<object?, ConfigValidationContext, ValidationRuleResult> ValidateFunc { get; set; } = null!;
        
        /// <summary>
        /// 是否启用
        /// </summary>
        public bool IsEnabled { get; set; } = true;
        
        /// <summary>
        /// 规则优先级
        /// </summary>
        public int Priority { get; set; } = 0;
    }

    /// <summary>
    /// 配置验证上下文
    /// </summary>
    public class ConfigValidationContext
    {
        /// <summary>
        /// 当前配置对象
        /// </summary>
        public object ConfigObject { get; set; } = null!;
        
        /// <summary>
        /// 配置项名称
        /// </summary>
        public string ConfigKey { get; set; } = string.Empty;
        
        /// <summary>
        /// 配置项值
        /// </summary>
        public object? ConfigValue { get; set; }
        
        /// <summary>
        /// 验证模式
        /// </summary>
        public ValidationMode Mode { get; set; } = ValidationMode.Strict;
        
        /// <summary>
        /// 是否允许自动修复
        /// </summary>
        public bool AllowAutoFix { get; set; } = true;
        
        /// <summary>
        /// 额外的上下文数据
        /// </summary>
        public Dictionary<string, object> Context { get; set; } = new();
    }

    /// <summary>
    /// 验证规则结果
    /// </summary>
    public class ValidationRuleResult
    {
        /// <summary>
        /// 是否通过验证
        /// </summary>
        public bool IsValid { get; set; } = true;
        
        /// <summary>
        /// 验证错误（如果有）
        /// </summary>
        public ValidationError? Error { get; set; }
        
        /// <summary>
        /// 验证警告（如果有）
        /// </summary>
        public ValidationWarning? Warning { get; set; }
        
        /// <summary>
        /// 配置建议（如果有）
        /// </summary>
        public ConfigSuggestion? Suggestion { get; set; }
        
        /// <summary>
        /// 自动修复（如果有）
        /// </summary>
        public ConfigFix? AutoFix { get; set; }
        
        /// <summary>
        /// 创建成功结果
        /// </summary>
        public static ValidationRuleResult Success() => new() { IsValid = true };
        
        /// <summary>
        /// 创建错误结果
        /// </summary>
        public static ValidationRuleResult CreateError(ValidationError error) => new() 
        { 
            IsValid = false, 
            Error = error 
        };
        
        /// <summary>
        /// 创建警告结果
        /// </summary>
        public static ValidationRuleResult CreateWarning(ValidationWarning warning) => new() 
        { 
            IsValid = true, 
            Warning = warning 
        };
        
        /// <summary>
        /// 创建建议结果
        /// </summary>
        public static ValidationRuleResult CreateSuggestion(ConfigSuggestion suggestion) => new() 
        { 
            IsValid = true, 
            Suggestion = suggestion 
        };
    }

    /// <summary>
    /// 验证模式
    /// </summary>
    public enum ValidationMode
    {
        Strict,     // 严格模式：所有规则都必须通过
        Lenient,    // 宽松模式：允许部分警告
        Performance // 性能模式：只检查关键错误
    }

    /// <summary>
    /// 自动盯盘配置的扩展验证信息
    /// </summary>
    public static class AutoMonitorConfigValidationExtensions
    {
        /// <summary>
        /// 配置项名称常量
        /// </summary>
        public static class ConfigKeys
        {
            public const string ScanIntervalSeconds = nameof(AutoMonitorConfig.ScanIntervalSeconds);
            public const string Name = nameof(AutoMonitorConfig.Name);
            public const string IsEnabled = nameof(AutoMonitorConfig.IsEnabled);
            
            // 保本配置相关
            public const string BreakEvenConfig = nameof(AutoMonitorConfig.BreakEvenConfig);
            public const string IsBreakEvenEnabled = "BreakEvenConfig.IsEnabled";
            public const string BreakEvenTriggerAmount = "BreakEvenConfig.TriggerProfitAmount";
            
            // 推仓配置相关
            public const string AddPositionConfig = nameof(AutoMonitorConfig.AddPositionConfig);
            public const string IsAddPositionEnabled = "AddPositionConfig.IsEnabled";
            public const string AddPositionTiers = "AddPositionConfig.Tiers";
            
            // 保盈止损配置相关
            public const string ProfitProtectionConfig = nameof(AutoMonitorConfig.ProfitProtectionConfig);
            public const string IsProfitProtectionEnabled = "ProfitProtectionConfig.IsEnabled";
            public const string ProfitProtectionTiers = "ProfitProtectionConfig.Tiers";
        }

        /// <summary>
        /// 错误代码常量
        /// </summary>
        public static class ErrorCodes
        {
            public const string InvalidScanInterval = "CONFIG_E001";
            public const string InvalidBreakEvenThreshold = "CONFIG_E002";
            public const string InvalidAddPositionTiers = "CONFIG_E003";
            public const string InvalidProfitProtectionTiers = "CONFIG_E004";
            public const string InvalidMaxContracts = "CONFIG_E005";
            public const string EmptyConfigName = "CONFIG_E006";
            public const string ConflictingSettings = "CONFIG_E007";
            public const string InsufficientRiskManagement = "CONFIG_E008";
        }

        /// <summary>
        /// 警告代码常量
        /// </summary>
        public static class WarningCodes
        {
            public const string HighScanFrequency = "CONFIG_W001";
            public const string LowBreakEvenThreshold = "CONFIG_W002";
            public const string AggressiveAddPosition = "CONFIG_W003";
            public const string ConservativeProfitProtection = "CONFIG_W004";
            public const string TooManyContracts = "CONFIG_W005";
            public const string NoRiskManagement = "CONFIG_W006";
        }

        /// <summary>
        /// 建议代码常量
        /// </summary>
        public static class SuggestionCodes
        {
            public const string OptimizeScanInterval = "CONFIG_S001";
            public const string BalanceRiskReward = "CONFIG_S002";
            public const string EnableRiskManagement = "CONFIG_S003";
            public const string OptimizeTierStructure = "CONFIG_S004";
        }
    }
} 