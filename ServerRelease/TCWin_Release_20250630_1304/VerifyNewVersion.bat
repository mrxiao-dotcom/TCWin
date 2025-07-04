@echo off 
chcp 65001 >nul 
echo ==================================== 
echo       New Version Verification 
echo ==================================== 
echo. 
echo Starting program and verifying new features... 
echo. 
echo Please check after startup: 
echo 1. Look for "=== Super Detailed Debug ===" in log 
echo 2. Check monitor panel for "Clear State" button 
echo 3. If both exist, version update is successful! 
echo. 
pause 
start "" "BinanceFuturesTrader.exe" 
