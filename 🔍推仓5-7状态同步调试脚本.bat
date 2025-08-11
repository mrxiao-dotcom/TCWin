@echo off
chcp 65001 > nul
echo 🔍 推仓5-7状态同步调试脚本
echo =====================================
echo.

:: 设置时间戳
for /f "tokens=2 delims==" %%a in ('wmic OS Get localdatetime /value') do set "dt=%%a"
set "YY=%dt:~2,2%" & set "YYYY=%dt:~0,4%" & set "MM=%dt:~4,2%" & set "DD=%dt:~6,2%"
set "HH=%dt:~8,2%" & set "Min=%dt:~10,2%" & set "Secs=%dt:~12,2%"
set "datestamp=%YYYY%-%MM%-%DD%" & set "timestamp=%HH%:%Min%:%Secs%"

echo 📅 调试时间: %datestamp% %timestamp%
echo.

echo 🔧 问题分析和修复状态
echo.

echo 📋 从日志分析出的问题:
echo   ✅ 推仓6保存成功: "推仓阶梯6状态变化: - → √"
echo   ❌ 推仓5状态: "推仓阶梯5状态无变化，跳过更新"  
echo   ❌ 推仓7状态: "推仓阶梯7状态无变化，跳过更新"
echo.

echo 🎯 根本原因:
echo   问题1: UI同步日志只显示T1-T4
echo          "🔥【状态同步】  推仓: T1=√, T2=√, T3=√, T4=√"
echo          推仓T5、T6、T7的状态没有显示
echo.
echo   问题2: SyncPushTierStatusFromUI()动态同步有问题
echo          推仓5、7的ComboBox状态没有被正确读取到动态字典
echo.

echo ✅ 已完成的修复:
echo   修复1: 动态状态同步日志
echo          - 状态同步日志现在会显示所有阶梯T1-T7的状态
echo          - 使用GetPushTierStatus()从动态字典获取状态
echo.
echo   修复2: 将进行更详细的UI同步调试
echo          - 需要确认ComboBox[4]和[6]的SelectedItem状态
echo          - 确认SetPushTierStatus()是否正确执行
echo.

echo 🧪 测试步骤:
echo.
echo 第一步: 修改推仓5、7的状态
echo   1. 打开合约配置编辑对话框
echo   2. 将推仓第5阶梯从"-"改为"√"  
echo   3. 将推仓第7阶梯从"-"改为"√"
echo   4. 点击保存按钮
echo.
echo 第二步: 观察新的详细日志
echo   现在应该看到:
echo   🔧【推仓同步】开始动态同步推仓状态，UI控件数量: 7
echo   🔧【推仓同步】T5: - → √
echo   🔧【推仓同步】T7: - → √
echo   🔧【推仓同步】推仓状态同步完成，共同步 7 个阶梯
echo.
echo   🔥【状态同步】  推仓: T1=√, T2=√, T3=√, T4=√, T5=√, T6=√, T7=√
echo.
echo   🔧【动态保存】文件中推仓阶梯数量: 7
echo   🔥【动态保存】推仓阶梯5状态变化: - → √
echo   🔥【动态保存】推仓阶梯7状态变化: - → √
echo.

echo 🔍 如果仍然有问题，检查:
echo.
echo 问题1: 状态同步日志仍然只显示T1-T4
echo   原因: 动态日志修复没有生效
echo   解决: 重新编译项目，确认修改正确应用
echo.
echo 问题2: UI控件数量不是7
echo   原因: _pushTierComboBoxes.Count != 7
echo   解决: 检查基础配置中是否确实有7个推仓阶梯
echo.
echo 问题3: ComboBox状态读取失败
echo   原因: _pushTierComboBoxes[4]或[6]的SelectedItem为null
echo   解决: 确认ComboBox是否正确选择了"√"选项
echo.
echo 问题4: SetPushTierStatus()没有执行
echo   原因: 动态设置逻辑有bug
echo   解决: 添加更详细的SetPushTierStatus调试日志
echo.

echo 🎯 预期成功结果:
echo   ✅ 推仓T5、T7状态变化被正确检测
echo   ✅ 文件中ExecutionState被正确更新
echo   ✅ 重新打开对话框时显示正确状态
echo.

echo 🚀 开始测试！
echo   请按照上述步骤测试，观察新的详细日志输出。
echo.

pause 