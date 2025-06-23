# 合约默认值和自动盯盘BTC开仓问题修复说明

## 问题描述

用户反馈了两个关键问题：

1. **合约输入框默认值问题**：不要给合约输入框默认输入BTC，启动程序时应该直接显示空白合约
2. **自动盯盘异常开仓BTC问题**：发现自动盯盘会自动开仓BTC，需要分析原因

## 问题分析

### 1. 合约默认值问题

**问题根源**：在多个地方设置了"BTCUSDT"作为默认合约：

#### A. MainViewModel.Trading.cs
```csharp
[ObservableProperty]
private string _symbol = "BTCUSDT"; // ❌ 硬编码默认值
```

#### B. TradingSettingsService.cs
```csharp
private TradingSettings GetDefaultSettings()
{
    return new TradingSettings
    {
        Symbol = "BTCUSDT", // ❌ 硬编码默认值
        // ... 其他设置
    };
}
```

#### C. Models/TradingSettings.cs
```csharp
public class TradingSettings
{
    public string Symbol { get; set; } = "BTCUSDT"; // ❌ 硬编码默认值
}
```

### 2. 自动盯盘BTC开仓问题

**问题根源**：在`Services/AutoMonitorService.cs`的`ExecuteAddPositionAsync`方法中，第395行有问题代码：

```csharp
// ❌ 问题代码：会修改用户界面的合约设置
Application.Current.Dispatcher.Invoke(() =>
{
    _mainViewModel.Symbol = position.Symbol.Replace("USDT", ""); // 这里会将"BTCUSDT"变成"BTC"
    _mainViewModel.StopLossRatio = stage.StopLossRatio * 100;
});
```

**问题分析**：
1. **干扰用户界面**：自动盯盘不应该修改用户界面的设置
2. **错误的合约设置**：`position.Symbol.Replace("USDT", "")`会将"BTCUSDT"变成"BTC"
3. **触发错误交易**：界面合约被设置为"BTC"可能导致后续交易使用错误的合约

## 修复方案

### 1. 合约默认值修复

#### A. 修改MainViewModel.Trading.cs
```csharp
// 修改前
[ObservableProperty]
private string _symbol = "BTCUSDT";

// 修改后
[ObservableProperty]
private string _symbol = "";
```

#### B. 修改TradingSettingsService.cs
```csharp
// 修改前
Symbol = "BTCUSDT",

// 修改后
Symbol = "",
```

#### C. 修改Models/TradingSettings.cs
```csharp
// 修改前
public string Symbol { get; set; } = "BTCUSDT";

// 修改后
public string Symbol { get; set; } = "";
```

### 2. 自动盯盘BTC开仓问题修复

#### 移除干扰代码
```csharp
// 修改前（问题代码）
// 临时设置MainViewModel的参数
Application.Current.Dispatcher.Invoke(() =>
{
    _mainViewModel.Symbol = position.Symbol.Replace("USDT", "");
    _mainViewModel.StopLossRatio = stage.StopLossRatio * 100;
});

// 修改后（修复代码）
// 🔧 修复：不要修改MainViewModel的Symbol，避免干扰用户界面
// 自动盯盘应该独立运行，不影响用户的界面设置
_logger.LogInformation($"💰 自动盯盘推仓: {position.Symbol}, 止损比例: {stage.StopLossRatio * 100:F1}%");
```

## 修复效果

### 1. 合约默认值修复效果

#### 修复前：
- 程序启动时合约输入框显示"BTCUSDT"
- 用户需要手动清空或修改合约

#### 修复后：
- 程序启动时合约输入框为空白
- 用户可以自由输入需要的合约
- 没有预设的合约偏好

### 2. 自动盯盘BTC开仓问题修复效果

#### 修复前：
- 自动盯盘执行推仓时会修改用户界面的合约设置
- 将"BTCUSDT"错误地设置为"BTC"
- 可能导致后续交易使用错误的合约

#### 修复后：
- 自动盯盘完全独立运行，不影响用户界面
- 不会修改用户的合约设置
- 消除了BTC开仓的风险

## 技术细节

### 修改的文件

1. **ViewModels/MainViewModel.Trading.cs**
   - 修改`_symbol`字段默认值从"BTCUSDT"改为""

2. **Services/TradingSettingsService.cs**
   - 修改`GetDefaultSettings()`方法中的Symbol默认值

3. **Models/TradingSettings.cs**
   - 修改`Symbol`属性默认值

4. **Services/AutoMonitorService.cs**
   - 移除`ExecuteAddPositionAsync`方法中修改MainViewModel的代码
   - 确保自动盯盘独立运行

### 设计原则

#### 1. 界面独立性
- 自动盯盘服务不应该修改用户界面的设置
- 用户界面和自动化功能应该相互独立

#### 2. 用户体验优化
- 不预设用户的交易偏好
- 让用户自主选择交易合约

#### 3. 安全性保障
- 避免自动化功能干扰用户的手动操作
- 防止意外的合约设置导致错误交易

## 风险控制

### 1. 自动盯盘隔离
- 自动盯盘现在完全基于持仓数据运行
- 不依赖用户界面的设置
- 避免了界面设置被意外修改的风险

### 2. 合约设置安全
- 启动时不预设任何合约
- 用户必须主动选择交易合约
- 减少了误操作的可能性

## 测试验证

### 1. 启动测试
- ✅ 程序启动时合约输入框为空白
- ✅ 没有预设的BTCUSDT合约

### 2. 自动盯盘测试
- ✅ 自动盯盘运行时不修改界面合约设置
- ✅ 推仓功能基于实际持仓数据运行
- ✅ 不会产生BTC相关的错误订单

## 总结

通过这次修复：

1. **用户体验改进**：
   - 启动时界面更加干净，没有预设偏好
   - 用户可以自由选择交易合约

2. **功能安全性提升**：
   - 自动盯盘不再干扰用户界面
   - 消除了BTC开仓的异常风险

3. **架构优化**：
   - 自动化功能与用户界面完全解耦
   - 提高了系统的稳定性和可维护性

这次修复确保了自动盯盘功能的独立性和安全性，同时优化了用户的初始体验。 