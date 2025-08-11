@echo off
echo 多分支干扰修复验证脚本
echo =============================
echo.

echo 步骤1: 检查数据保护机制
findstr /n "_FromStateFile.*true" "Views\AutoMonitorConfigWindowSimple.xaml.cs" >nul
if %errorlevel% equ 0 (
    echo [成功] 数据保护机制已实施
) else (
    echo [失败] 数据保护机制未找到
)
echo.

echo 步骤2: 检查清空操作保护
findstr /n "跳过清空操作.*配置来自状态文件" "Views\AutoMonitorConfigWindowSimple.xaml.cs" >nul
if %errorlevel% equ 0 (
    echo [成功] 清空操作保护已实施
) else (
    echo [失败] 清空操作保护未找到
)
echo.

echo 步骤3: 检查推仓默认处理
findstr /n "推仓配置为null或空.*设置默认状态" "Views\AutoMonitorConfigWindowSimple.xaml.cs" >nul
if %errorlevel% equ 0 (
    echo [成功] 推仓默认处理已实施
) else (
    echo [失败] 推仓默认处理未找到
)
echo.

echo 步骤4: 检查保盈默认处理
findstr /n "保盈配置为null或空.*设置默认状态" "Views\AutoMonitorConfigWindowSimple.xaml.cs" >nul
if %errorlevel% equ 0 (
    echo [成功] 保盈默认处理已实施
) else (
    echo [失败] 保盈默认处理未找到
)
echo.

echo 步骤5: 编译检查
dotnet build TCWin.sln --verbosity quiet
if %errorlevel% equ 0 (
    echo [成功] 项目编译成功
) else (
    echo [失败] 项目编译失败
)
echo.

echo =============================
echo 修复总结
echo =============================
echo.
echo 核心问题: 多分支处理干扰导致数据被意外清空
echo.
echo 实施的修复:
echo 1. 数据保护标记 - 防止状态文件数据被清空
echo 2. 清空操作保护 - 跳过对受保护数据的清空
echo 3. 推仓默认处理 - 配置缺失时设置合理默认值
echo 4. 保盈默认处理 - 配置缺失时设置合理默认值
echo 5. 增强调试追踪 - 详细记录数据处理过程
echo.
echo 期望结果: 稳定且完整的配置显示，不再出现"-"
echo.
echo 多分支干扰修复验证完成！

pause 