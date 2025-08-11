@echo off
chcp 65001 >nul
echo.
echo ================================================================
echo 🔧 基础配置逻辑修复验证脚本
echo ================================================================
echo.
echo 📋 问题描述：
echo   现在的程序弄丢了原来的基础配置逻辑，需要恢复：
echo   1. 基础配置文件不存在时，启动盯盘面板要提醒用户必须先创建基础配置
echo   2. 在Global目录下生成基础配置文件，一个配置一个文件
echo   3. 打开启动盯盘面板后，先检查基础配置，再检查统一配置文件
echo.
echo 🔧 修复内容：
echo   1. 新增单个基础配置文件的存储方式（一个配置一个文件）
echo   2. 完善启动盯盘面板的检查逻辑（先基础配置，后统一配置）
echo   3. 增加基础配置文件存在性检查方法
echo   4. 保持界面样式不变
echo.
echo ================================================================
echo 🧪 验证测试
echo ================================================================
echo.
echo **测试1：无基础配置时的启动检查**
echo 1. 删除或重命名Global/BaseConfigs目录
echo 2. 删除或重命名Global/auto_monitor_configs.json文件
echo 3. 点击主界面的【启动盯盘】按钮
echo.
echo **预期结果1：提醒创建基础配置**
echo ✅ 显示提示对话框："检测到尚未创建任何基础配置！"
echo ✅ 对话框内容包含：
echo    • "启动盯盘面板需要先创建基础配置"
echo    • "基础配置包含：止盈止损参数、推仓和保护参数、监控间隔设置"
echo    • "点击【确定】跳转到配置创建界面"
echo ✅ 点击确定后，打开配置创建窗口
echo ✅ 点击取消后，不打开盯盘面板
echo.
echo **测试2：创建基础配置后的文件存储**
echo 1. 在配置创建窗口中创建一个新的基础配置
echo 2. 保存配置
echo 3. 检查文件系统中的文件结构
echo.
echo **预期结果2：单文件存储格式**
echo ✅ 在Global/BaseConfigs目录下生成单个配置文件
echo ✅ 文件名格式：配置名称.json（特殊字符已转换为下划线）
echo ✅ 每个配置都有独立的JSON文件
echo ✅ 旧格式文件（auto_monitor_configs.json）如存在仍能兼容读取
echo.
echo **测试3：有基础配置时的启动检查**
echo 1. 确保已有基础配置文件
echo 2. 点击主界面的【启动盯盘】按钮
echo 3. 观察启动检查过程
echo.
echo **预期结果3：正常启动流程**
echo ✅ 基础配置检查通过，显示发现的配置数量
echo ✅ 继续检查统一状态文件
echo ✅ 根据统一状态文件存在情况决定：
echo    • 如果存在：直接读取并显示
echo    • 如果不存在：根据基础配置和持仓生成
echo.
echo **测试4：新旧格式兼容性测试**
echo 1. 准备旧格式的基础配置文件（auto_monitor_configs.json）
echo 2. 确保新格式目录（BaseConfigs）为空或不存在
echo 3. 启动程序并打开盯盘面板
echo.
echo **预期结果4：兼容性保持**
echo ✅ 能够读取旧格式的基础配置文件
echo ✅ 基础配置检查通过
echo ✅ 启动盯盘面板正常
echo ✅ 新创建或修改的配置保存为新格式（单文件）
echo.
echo ================================================================
echo 📊 详细验证指南
echo ================================================================
echo.
echo **验证点1：基础配置文件检查逻辑**
echo □ BaseConfigManager.Instance.HasAnyBaseConfigs() 方法工作正常
echo □ 能够检测新格式配置文件（BaseConfigs目录中的*.json文件）
echo □ 能够检测旧格式配置文件（auto_monitor_configs.json）
echo □ 无配置时返回false，有配置时返回true
echo.
echo **验证点2：文件存储格式**
echo □ 新配置保存到 Global/BaseConfigs/配置名称.json
echo □ 配置名称中的特殊字符正确转换为下划线
echo □ JSON格式正确，包含完整的配置信息
echo □ 文件目录自动创建
echo.
echo **验证点3：启动检查流程**
echo □ 启动盯盘面板时首先检查基础配置
echo □ 无基础配置时显示正确的提示对话框
echo □ 有基础配置时继续检查统一状态文件
echo □ 整个检查流程的日志记录清晰
echo.
echo **验证点4：用户体验**
echo □ 提示对话框内容清晰、易懂
echo □ 界面样式保持不变
echo □ 操作流程符合用户习惯
echo □ 错误处理友好
echo.
echo ================================================================
echo 🔍 问题排查
echo ================================================================
echo.
echo **如果基础配置检查不生效**：
echo.
echo **排查1：检查BaseConfigManager初始化**
echo 1. 确认BaseConfigManager.Instance正常工作
echo 2. 检查HasAnyBaseConfigs()方法是否正确实现
echo 3. 验证文件路径生成是否正确
echo.
echo **排查2：检查文件存储路径**
echo 1. 确认Global目录路径正确
echo 2. 检查BaseConfigs子目录是否能正常创建
echo 3. 验证文件名处理逻辑
echo.
echo **排查3：检查启动流程**
echo 1. 确认OpenAutoMonitorDashboardAsync方法调用正确
echo 2. 检查基础配置检查代码是否执行
echo 3. 验证MessageBox显示逻辑
echo.
echo **如果文件存储不正确**：
echo.
echo **排查1：检查SaveSingleConfigFile方法**
echo 1. 确认方法调用路径正确
echo 2. 检查JSON序列化设置
echo 3. 验证文件写入权限
echo.
echo **排查2：检查路径管理**
echo 1. 确认GetSingleBaseConfigFilePath方法工作正常
echo 2. 检查文件名清理逻辑
echo 3. 验证目录创建逻辑
echo.
echo **如果兼容性有问题**：
echo.
echo **排查1：检查加载顺序**
echo 1. 确认优先加载新格式配置
echo 2. 检查旧格式配置加载逻辑
echo 3. 验证加载结果合并
echo.
echo **排查2：检查格式识别**
echo 1. 确认新旧格式识别正确
echo 2. 检查文件存在性判断
echo 3. 验证错误处理逻辑
echo.
echo ================================================================
echo 💡 常见问题解决方案
echo ================================================================
echo.
echo **问题1：基础配置检查失败**
echo 解决方案：
echo ✅ 检查BaseConfigManager单例初始化
echo ✅ 验证文件路径配置
echo ✅ 确认文件系统权限
echo ✅ 检查异常处理逻辑
echo.
echo **问题2：文件存储路径错误**
echo 解决方案：
echo ✅ 确认GetGlobalConfigDirectory()返回正确路径
echo ✅ 检查BaseConfigs子目录创建
echo ✅ 验证文件名清理逻辑
echo ✅ 测试目录写入权限
echo.
echo **问题3：提示对话框不显示**
echo 解决方案：
echo ✅ 确认HasAnyBaseConfigs()返回值正确
echo ✅ 检查if条件判断逻辑
echo ✅ 验证MessageBox调用
echo ✅ 检查UI线程执行
echo.
echo **问题4：新旧格式混乱**
echo 解决方案：
echo ✅ 明确加载优先级（新格式优先）
echo ✅ 确认保存格式（新配置用新格式）
echo ✅ 测试混合环境下的行为
echo ✅ 提供迁移工具或说明
echo.
echo ================================================================
echo 🚨 注意事项
echo ================================================================
echo.
echo 1. **文件路径**：确保Global/BaseConfigs目录可以正常创建和访问
echo 2. **文件命名**：配置名称中的特殊字符会被转换为下划线
echo 3. **兼容性**：旧格式文件仍能读取，但新配置会保存为新格式
echo 4. **权限**：确保程序对配置目录有读写权限
echo 5. **界面**：验证过程中注意界面样式应保持不变
echo.
echo 按任意键继续...
pause >nul 