// 🔧 强制状态同步修复脚本
// 在 AutoMonitorConfigWindowSimple.xaml.cs 中添加以下方法

/// <summary>
/// 🔧 强制重新同步状态文件到UI显示
/// </summary>
private async void ForceStateSync_Click(object sender, RoutedEventArgs e)
{
    try
    {
        AddLog("🔧 强制重新同步状态文件到UI显示...");
        
        // 1. 直接从状态文件读取
        var filePathManager = new FilePathManager();
        var currentAccountName = _mainViewModel?.SelectedAccount?.Name ?? filePathManager.GetCurrentAccountName();
        var stateFilePath = filePathManager.GetContractMonitoringStatesFilePath(currentAccountName);
        
        AddLog($"📁 状态文件路径: {stateFilePath}");
        
        if (!File.Exists(stateFilePath))
        {
            AddLog("❌ 状态文件不存在");
            return;
        }
        
        // 2. 读取文件内容
        var json = File.ReadAllText(stateFilePath);
        AddLog($"📄 文件内容长度: {json.Length} 字符");
        
        var loggerFactory = Microsoft.Extensions.Logging.LoggerFactory.Create(builder => builder.AddConsole());
        var stateLogger = loggerFactory.CreateLogger<ContractMonitoringStateService>();
        
        var stateService = new ContractMonitoringStateService(
            stateLogger, 
            _configManager,
            filePathManager,
            currentAccountName);

        var monitoringStates = stateService.LoadMonitoringStates();
        AddLog($"📊 成功解析状态: {monitoringStates.Count} 个");
        
        // 3. 逐个检查状态
        foreach (var kvp in monitoringStates)
        {
            var contractKey = kvp.Key;
            var state = kvp.Value;
            
            AddLog($"🔍 检查合约: {contractKey}");
            AddLog($"   📊 保本ExecutionState: {state.BreakEvenConfig?.ExecutionState}");
            AddLog($"   📊 保本IsExecuted: {state.BreakEvenConfig?.IsExecuted}");
            
            if (state.AddPositionConfig?.Tiers != null)
            {
                for (int i = 0; i < state.AddPositionConfig.Tiers.Count; i++)
                {
                    var tier = state.AddPositionConfig.Tiers[i];
                    AddLog($"   📊 推仓{tier.TierIndex}执行状态: {tier.ExecutionState}");
                }
            }
        }
        
        // 4. 强制更新UI
        await Application.Current.Dispatcher.InvokeAsync(() =>
        {
            foreach (var config in ContractConfigs.ToList())
            {
                var contractKey = config.ContractName.Replace(" ", "_");
                
                if (monitoringStates.TryGetValue(contractKey, out var state))
                {
                    AddLog($"🔄 强制更新UI配置: {config.ContractName}");
                    
                    // 直接设置状态
                    if (state.BreakEvenConfig != null)
                    {
                        var oldStatus = config.BreakEvenStatus;
                        config.BreakEvenStatus = state.BreakEvenConfig.ExecutionState switch
                        {
                            ExecutionState.NotTriggered => "-",
                            ExecutionState.Executing => "⚡",
                            ExecutionState.Executed => "√",
                            _ => "-"
                        };
                        
                        AddLog($"   📊 保本状态更新: {oldStatus} → {config.BreakEvenStatus}");
                    }
                    
                    // 更新推仓状态
                    if (state.AddPositionConfig?.Tiers != null)
                    {
                        var tiers = state.AddPositionConfig.Tiers.OrderBy(t => t.TierIndex).ToArray();
                        for (int i = 0; i < Math.Min(tiers.Length, 4); i++)
                        {
                            var tier = tiers[i];
                            var newStatus = tier.ExecutionState switch
                            {
                                ExecutionState.NotTriggered => "-",
                                ExecutionState.Executing => "⚡",
                                ExecutionState.Executed => "√",
                                _ => "-"
                            };
                            
                            var oldStatus = i switch
                            {
                                0 => config.PushTier1Status,
                                1 => config.PushTier2Status,
                                2 => config.PushTier3Status,
                                3 => config.PushTier4Status,
                                _ => "-"
                            };
                            
                            switch (i)
                            {
                                case 0: config.PushTier1Status = newStatus; break;
                                case 1: config.PushTier2Status = newStatus; break;
                                case 2: config.PushTier3Status = newStatus; break;
                                case 3: config.PushTier4Status = newStatus; break;
                            }
                            
                            AddLog($"   📊 推仓{i+1}状态更新: {oldStatus} → {newStatus}");
                        }
                    }
                    
                    // 强制通知属性变更
                    config.OnPropertyChanged(nameof(config.BreakEvenStatus));
                    config.OnPropertyChanged(nameof(config.PushTier1Status));
                    config.OnPropertyChanged(nameof(config.PushTier2Status));
                    config.OnPropertyChanged(nameof(config.PushTier3Status));
                    config.OnPropertyChanged(nameof(config.PushTier4Status));
                }
            }
        });
        
        AddLog("✅ 强制状态同步完成");
        
    }
    catch (Exception ex)
    {
        AddLog($"❌ 强制状态同步失败: {ex.Message}");
    }
} 