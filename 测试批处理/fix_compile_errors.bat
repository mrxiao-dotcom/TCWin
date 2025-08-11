@echo off
echo 🔧 开始修复编译错误...

REM 备份文件
copy "Views\AutoMonitorDashboard.xaml.cs" "Views\AutoMonitorDashboard_backup3.xaml.cs"
echo ✅ 已备份文件

REM 使用PowerShell删除重复代码
powershell -Command "(Get-Content 'Views\AutoMonitorDashboard.xaml.cs' -Raw) -replace '        private DateTime _monitorStartTime;\r?\n            // 🔧 立即可见的改进[\s\S]*?MessageBox\.Show\(`$`"自动盯盘控制面板初始化失败[^}]*}\r?\n        }', '        private DateTime _monitorStartTime;' | Set-Content 'Views\AutoMonitorDashboard.xaml.cs' -NoNewline"

echo ✅ 已删除重复的构造函数代码
echo 🎉 修复完成！请重新编译项目。
pause 