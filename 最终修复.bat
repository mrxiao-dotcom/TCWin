@echo off
echo 最终修复编译错误...

powershell -Command "$f='Views/AutoMonitorDashboard.xaml.cs'; $c=[IO.File]::ReadAllText($f); $c=$c.TrimEnd(); while($c.EndsWith('}')) { $c=$c.Substring(0,$c.Length-1).TrimEnd() }; $c=$c+\"`n}\"; [IO.File]::WriteAllText($f, $c)"

echo 修复完成，重新编译...
dotnet build BinanceFuturesTrader.csproj --verbosity quiet

echo 任务完成！ 