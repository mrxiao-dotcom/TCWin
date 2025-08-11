@echo off
chcp 65001 > nul
echo.
echo 🧪 三大问题修复验证脚本
echo ==========================================
echo.

echo 📋 验证内容概述
echo ==========================================
echo 1. 基础配置保存统一化验证
echo 2. 状态修改保存功能验证  
echo 3. 基础配置启用状态界面控制验证
echo.

echo 🔧 问题修复总结
echo ==========================================
echo 【问题1】基础配置被覆盖问题
echo ✅ 修复：统一使用BaseConfigManager保存配置
echo ✅ 位置：AutoMonitorConfigWindowSimple.xaml.cs、SimpleConfigEditorWindow.xaml.cs
echo.
echo 【问题2】状态修改保存失败问题  
echo ✅ 修复：增强状态修改保存逻辑，确保同步到统一状态文件
echo ✅ 位置：ContractConfigEditDialog.xaml.cs
echo.
echo 【问题3】基础配置启用状态控制缺失问题
echo ✅ 修复：实现基于基础配置的界面显示和编辑权限控制
echo ✅ 位置：多个视图文件和数据模型
echo.

echo 🧪 验证步骤
echo ==========================================
echo.

echo 【验证1】基础配置保存统一化
echo ----------------------------------------
echo 步骤：
echo 1. 启动程序
echo 2. 打开基础配置编辑窗口
echo 3. 创建或修改一个基础配置（如 "测试配置"）
echo 4. 保存配置
echo 5. 重启程序
echo 6. 检查配置是否还存在且内容正确
echo.
echo 预期结果：
echo ✅ 配置保存后不会被覆盖
echo ✅ 重启程序后配置依然存在
echo ✅ 日志显示使用BaseConfigManager保存
echo.

echo 【验证2】状态修改保存功能
echo ----------------------------------------
echo 步骤：
echo 1. 启动程序并加载持仓配置
echo 2. 双击一个合约行打开编辑窗口
echo 3. 修改推仓状态（如推仓1档从 "-" 改为 "√"）
echo 4. 保存并关闭窗口
echo 5. 重新打开相同合约的编辑窗口
echo 6. 检查推仓状态是否保持修改后的值
echo.
echo 预期结果：
echo ✅ 状态修改能正确保存
echo ✅ 重新打开时状态保持修改后的值
echo ✅ 日志显示状态同步到统一状态文件
echo.

echo 【验证3】基础配置启用状态界面控制
echo ----------------------------------------
echo 步骤：
echo 1. 创建一个测试基础配置：
echo    - 保本：启用
echo    - 推仓1档：启用
echo    - 推仓2档：启用  
echo    - 推仓3档：禁用
echo    - 推仓4档：禁用
echo    - 保盈：禁用
echo 2. 使用此配置加载合约配置
echo 3. 检查合约配置列表的列显示
echo 4. 尝试双击编辑禁用功能的列
echo.
echo 预期结果：
echo ✅ 只显示启用功能的列（保本、推仓1、推仓2）
echo ✅ 不显示禁用功能的列（推仓3、推仓4、保盈）
echo ✅ 双击禁用功能列时显示提示信息，阻止编辑
echo ✅ 编辑窗口中禁用功能的控件不可编辑或不显示
echo.

echo 🔍 详细验证检查项
echo ==========================================
echo.

echo 【基础配置文件检查】
echo ----------------------------------------
echo 配置文件位置：
echo %APPDATA%\BinanceFuturesTrader\Global\auto_monitor_configs.json
echo.
echo 检查要点：
echo □ 文件存在且不为空
echo □ 文件格式为数组格式：[{配置1}, {配置2}]
echo □ 不包含accountConfigs结构
echo □ 不包含运行时状态信息（如isTriggered）
echo.

echo 【状态文件检查】
echo ----------------------------------------
echo 状态文件位置：
echo %APPDATA%\BinanceFuturesTrader\Accounts\Test\contract_monitoring_states.json
echo.
echo 检查要点：
echo □ 状态修改后文件被更新
echo □ ExecutionState正确反映修改的状态
echo □ ExecutionTime在状态变为executed时被设置
echo.

echo 【日志关键信息】
echo ----------------------------------------
echo 查找以下日志信息：
echo ✅ "基础配置已更新: 配置名" 或 "基础配置已添加: 配置名"
echo ✅ "状态修改已同步到统一状态文件: 合约名"
echo ✅ "列显示已根据基础配置更新"
echo ✅ "编辑控件已根据基础配置更新权限"
echo ❌ "❌ 基础配置保存失败" 或类似错误信息
echo.

echo 🐛 可能遇到的问题及解决方案
echo ==========================================
echo.

echo 【问题】基础配置仍被覆盖
echo ----------------------------------------
echo 可能原因：
echo - 还有其他地方使用旧的保存服务
echo - EnhancedBaseConfigManager冲突
echo 解决方案：
echo - 检查是否所有保存都使用BaseConfigManager
echo - 停用EnhancedBaseConfigManager服务
echo.

echo 【问题】状态修改保存失败
echo ----------------------------------------
echo 可能原因：
echo - ContractMonitoringStateService未正确注入
echo - 统一状态文件路径错误
echo 解决方案：
echo - 检查服务注册
echo - 确认状态文件路径
echo - 查看详细错误日志
echo.

echo 【问题】界面控制不生效
echo ----------------------------------------
echo 可能原因：
echo - 数据模型缺少启用状态标志位
echo - 配置生成逻辑未更新
echo 解决方案：
echo - 添加IsBreakEvenEnabled等标志位
echo - 更新合约配置生成逻辑
echo - 实现列显示控制方法
echo.

echo 📊 成功验证的标志
echo ==========================================
echo 1. ✅ 基础配置创建后重启程序不丢失
echo 2. ✅ 状态修改后重新打开窗口状态保持
echo 3. ✅ 禁用功能的列不显示
echo 4. ✅ 禁用功能无法编辑且有提示
echo 5. ✅ 日志显示正确的保存和同步信息
echo.

echo 🎯 如果验证失败
echo ==========================================
echo 1. 记录具体的失败现象
echo 2. 收集相关的错误日志
echo 3. 检查文件是否按预期创建/更新
echo 4. 检查代码修改是否正确应用
echo 5. 联系开发人员进一步排查
echo.

echo 验证完成后，按任意键退出...
pause > nul 