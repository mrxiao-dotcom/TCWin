@echo off
chcp 65001 > nul
echo 🧪 动态推仓保盈状态保存验证脚本
echo ===========================================
echo.

:: 设置时间戳
for /f "tokens=2 delims==" %%a in ('wmic OS Get localdatetime /value') do set "dt=%%a"
set "YY=%dt:~2,2%" & set "YYYY=%dt:~0,4%" & set "MM=%dt:~4,2%" & set "DD=%dt:~6,2%"
set "HH=%dt:~8,2%" & set "Min=%dt:~10,2%" & set "Secs=%dt:~12,2%"
set "datestamp=%YYYY%-%MM%-%DD%" & set "timestamp=%HH%:%Min%:%Secs%"

echo 📅 验证时间: %datestamp% %timestamp%
echo.

:: 检查项目根目录
if not exist "BinanceFuturesTrader.csproj" (
    echo ❌ 错误：请在项目根目录运行此脚本
    pause
    exit /b 1
)

echo 🎉 动态推仓保盈状态保存功能验证
echo.

echo 📋 已完成的动态修复:
echo   ✅ 修复1: 添加动态状态字典存储
echo   ✅ 修复2: 实现动态状态访问方法 (GetPushTierStatus/SetPushTierStatus)
echo   ✅ 修复3: 修改SyncPushTierStatusFromUI为动态版本
echo   ✅ 修复4: 修改SyncProfitTierStatusFromUI为动态版本  
echo   ✅ 修复5: 修改推仓保存逻辑为动态版本
echo   🔄 修复6: 保盈保存逻辑动态化（进行中）
echo.

echo 🧪 测试场景:
echo.
echo 场景1: 推仓5-7状态保存测试
echo   1. 修改推仓第5阶梯状态: - → √
echo   2. 修改推仓第6阶梯状态: - → √  
echo   3. 修改推仓第7阶梯状态: - → √
echo   4. 点击保存按钮
echo   5. 观察日志输出
echo.

echo 场景2: 保盈4-10状态保存测试
echo   1. 修改保盈第4阶梯状态: - → √
echo   2. 修改保盈第8阶梯状态: - → √
echo   3. 修改保盈第10阶梯状态: - → √
echo   4. 点击保存按钮
echo   5. 观察日志输出
echo.

echo 📊 预期日志输出:
echo.
echo 动态同步阶段:
echo 🔧【推仓同步】开始动态同步推仓状态，UI控件数量: 7
echo 🔧【推仓同步】T5: - → √
echo 🔧【推仓同步】T6: - → √
echo 🔧【推仓同步】T7: - → √
echo 🔧【推仓同步】推仓状态同步完成，共同步 7 个阶梯
echo.
echo 动态保存阶段:
echo 🔧【动态保存】文件中推仓阶梯数量: 7
echo 🔥【动态保存】推仓阶梯5状态变化: - → √，金额保持: XXX.XX
echo 🔥【动态保存】推仓阶梯6状态变化: - → √，金额保持: XXX.XX
echo 🔥【动态保存】推仓阶梯7状态变化: - → √，金额保持: XXX.XX
echo 🔥【状态更新】推仓阶梯5状态更新为executed，金额保持: XXX.XX
echo.

echo 🔍 如果测试失败，检查以下几点:
echo.
echo 问题1: 仍然显示"推仓阶梯5-7状态无变化"
echo   原因: UI控件状态同步失败
echo   解决: 检查_pushTierComboBoxes集合是否包含5-7的控件
echo.
echo 问题2: 出现NullReference异常
echo   原因: _extendedPushTierStatuses字典未正确初始化
echo   解决: 确认字段声明和初始化代码
echo.
echo 问题3: 状态同步成功但文件保存失败
echo   原因: 保存逻辑中的动态获取方法未生效
echo   解决: 检查GetPushTierStatus方法的实现
echo.

echo 📝 详细验证清单:
echo.
echo [ ] 1. 项目编译成功，无编译错误
echo [ ] 2. 推仓5-7状态修改后出现动态同步日志
echo [ ] 3. 状态同步显示正确的阶梯数量
echo [ ] 4. 动态保存显示正确的文件阶梯数量
echo [ ] 5. 推仓5-7状态变化被正确检测
echo [ ] 6. 文件中ExecutionState确实被更新
echo [ ] 7. 重新打开配置对话框，状态保持修改后的值
echo.

echo 🎯 成功标准:
echo.
echo ✅ 完全成功: 推仓1-7和保盈1-10+的状态都能正确保存
echo ⚠️ 部分成功: 推仓5-7能保存，但保盈4+不能（需要完成保盈动态保存逻辑）
echo ❌ 失败: 推仓5-7仍然不能保存（动态同步逻辑有问题）
echo.

echo 🛠️ 下一步行动:
echo   如果推仓5-7测试成功，需要：
echo   1. 完成保盈动态保存逻辑的修改
echo   2. 更新动态验证逻辑
echo   3. 测试更多阶梯数量的场景
echo.

echo ✨ 开始测试！请按照上述场景进行验证。
echo.

pause 