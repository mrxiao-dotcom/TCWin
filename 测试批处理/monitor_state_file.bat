@echo off 
echo 开始监控状态文件变�?.. 
for /f "tokens=*" %%a in ('dir "C:\Users\Administrator\AppData\Roaming\BinanceFuturesTrader\Accounts\*\contract_monitoring_states.json" /s /b 2^>nul') do ( 
  echo 监控文件: %%a 
  :MONITOR_LOOP 
  timeout /t 2 /nobreak >nul 
  echo [%%TIME%%] 检查文件变�?.. 
  findstr /n "executionState.*:" "%%a" 2^>nul | findstr /v "null" 
  goto MONITOR_LOOP 
) 
