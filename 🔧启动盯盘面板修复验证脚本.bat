@echo off
chcp 65001 >nul
echo.
echo ================================================================
echo 🔧 启动盯盘面板修复验证脚本
echo ================================================================
echo.
echo 📋 问题描述：
echo   1. 点击启动盯盘后，打开的面板是旧的废弃面板，而不是最新的面板
echo   2. 确认对话框点击后，应该直接弹出自动盯盘管理面板，以及在此上弹出编辑基础配置的管理面板
echo.
echo 🔧 修复内容：
echo   1. 将旧的废弃面板AutoMonitorConfigWindowSimple替换为最新的AutoMonitorDashboard_Refactored
echo   2. 实现正确的面板弹出顺序：自动盯盘管理面板 + 基础配置编辑面板
echo   3. 清理了旧面板的引用，确保使用模块化重构架构
echo.
echo ================================================================
echo 🧪 验证测试
echo ================================================================
echo.
echo **测试1：基础配置存在时的启动流程**
echo 1. 确保已有基础配置文件
echo 2. 点击主界面的【启动盯盘】按钮
echo 3. 观察面板打开顺序和内容
echo.
echo **预期结果1：正确的面板打开**
echo ✅ 首先打开：自动盯盘管理面板（AutoMonitorDashboard_Refactored）
echo ✅ 面板特征：
echo    • 使用模块化重构架构
echo    • 界面清晰、功能完整
echo    • 包含监控状态、合约列表、控制按钮等
echo ✅ 然后自动弹出：基础配置编辑面板
echo ✅ 面板层次：基础配置编辑面板在自动盯盘管理面板之上
echo.
echo **测试2：无基础配置时的启动流程**
echo 1. 删除或重命名所有基础配置文件
echo 2. 点击主界面的【启动盯盘】按钮
echo 3. 观察提示和后续流程
echo.
echo **预期结果2：基础配置检查和引导**
echo ✅ 显示基础配置缺失提示对话框
echo ✅ 点击【确定】后：
echo    • 跳转到基础配置创建界面
echo    • 创建配置后可正常启动
echo ✅ 点击【取消】后：
echo    • 不打开任何面板
echo    • 流程正确终止
echo.
echo **测试3：面板架构验证**
echo 1. 观察打开的自动盯盘管理面板
echo 2. 检查面板类型和功能
echo 3. 确认不是旧的废弃面板
echo.
echo **预期结果3：新架构面板特征**
echo ✅ 面板类型：AutoMonitorDashboard_Refactored
echo ✅ 架构特征：
echo    • 模块化设计，职责清晰
echo    • 响应速度快，界面流畅
echo    • 功能完整，无缺失
echo ✅ 非旧面板特征：
echo    • 不是AutoMonitorConfigWindowSimple
echo    • 不是简单的配置窗口
echo    • 不是已废弃的旧界面
echo.
echo ================================================================
echo 📊 详细验证指南
echo ================================================================
echo.
echo **验证点1：面板类型识别**
echo □ 打开的面板标题包含"自动盯盘"或"监控面板"字样
echo □ 面板布局合理，包含多个功能区域
echo □ 面板性能良好，无明显卡顿
echo □ 面板功能完整，操作响应正常
echo.
echo **验证点2：弹出顺序正确**
echo □ 首先显示自动盯盘管理面板
echo □ 管理面板完全加载后
echo □ 自动弹出基础配置编辑面板
echo □ 两个面板层次关系正确
echo.
echo **验证点3：功能集成验证**
echo □ 自动盯盘管理面板可以正常操作
echo □ 基础配置编辑面板可以正常编辑和保存
echo □ 两个面板之间的数据同步正常
echo □ 关闭面板的操作逻辑正确
echo.
echo **验证点4：日志记录检查**
echo □ 查看应用日志，确认以下信息：
echo    • "🏗️ 创建重构后的自动盯盘管理面板..."
echo    • "🖥️ 自动盯盘管理面板创建成功，使用模块化重构架构"
echo    • "🔧 在自动盯盘管理面板上弹出基础配置编辑面板..."
echo □ 无旧面板相关的日志（如AutoMonitorConfigWindowSimple）
echo.
echo ================================================================
echo 🔍 问题排查
echo ================================================================
echo.
echo **如果仍然打开旧面板**：
echo.
echo **排查1：检查代码修复**
echo 1. 确认ViewModels/MainViewModel.AutoMonitor.cs已更新
echo 2. 检查OpenAutoMonitorDashboardAsync方法
echo 3. 确认使用的是AutoMonitorDashboard_Refactored
echo.
echo **排查2：检查编译状态**
echo 1. 重新编译整个项目
echo 2. 确认无编译错误
echo 3. 重启应用程序
echo.
echo **排查3：检查文件清理**
echo 1. 确认旧的废弃文件已清理
echo 2. 检查是否有缓存文件干扰
echo 3. 清理bin和obj目录后重新编译
echo.
echo **如果面板弹出顺序错误**：
echo.
echo **排查1：检查异步调用**
echo 1. 确认dashboardWindow.Show()正确执行
echo 2. 检查await OpenAutoMonitorConfigAsync()调用
echo 3. 验证异步方法的执行顺序
echo.
echo **排查2：检查窗口Owner设置**
echo 1. 确认Owner设置正确
echo 2. 检查窗口层次关系
echo 3. 验证窗口显示模式
echo.
echo **如果基础配置检查有问题**：
echo.
echo **排查1：检查配置检查逻辑**
echo 1. 确认HasAnyBaseConfigs()方法工作正常
echo 2. 检查基础配置文件路径
echo 3. 验证配置文件格式
echo.
echo **排查2：检查流程分支**
echo 1. 确认有配置时的分支逻辑
echo 2. 检查无配置时的提示逻辑
echo 3. 验证用户选择后的处理
echo.
echo ================================================================
echo 💡 常见问题解决方案
echo ================================================================
echo.
echo **问题1：面板类型错误**
echo 解决方案：
echo ✅ 确认引用的是Views.AutoMonitor.AutoMonitorDashboard_Refactored
echo ✅ 检查using语句是否正确
echo ✅ 重新编译并重启应用
echo ✅ 清理旧的引用和缓存
echo.
echo **问题2：面板弹出顺序错误**
echo 解决方案：
echo ✅ 确认Show()方法在await之前调用
echo ✅ 检查异步方法的执行顺序
echo ✅ 验证窗口Owner和层次设置
echo ✅ 测试窗口显示的时间间隔
echo.
echo **问题3：功能不完整**
echo 解决方案：
echo ✅ 确认AutoMonitorDashboard_Refactored完整可用
echo ✅ 检查模块化组件是否正确加载
echo ✅ 验证依赖注入和服务注册
echo ✅ 测试各个功能模块的工作状态
echo.
echo **问题4：性能或稳定性问题**
echo 解决方案：
echo ✅ 检查内存使用情况
echo ✅ 验证资源释放逻辑
echo ✅ 测试长时间运行的稳定性
echo ✅ 优化初始化和加载过程
echo.
echo ================================================================
echo 🚨 注意事项
echo ================================================================
echo.
echo 1. **面板识别**：确保打开的是AutoMonitorDashboard_Refactored，不是旧的配置窗口
echo 2. **功能验证**：测试自动盯盘管理面板的各项功能是否正常工作
echo 3. **性能检查**：确认新面板的响应速度和稳定性
echo 4. **数据同步**：验证两个面板之间的数据同步是否正常
echo 5. **用户体验**：确保面板弹出顺序符合用户操作习惯
echo.
echo 按任意键继续...
pause >nul 