$configPath = "$env:APPDATA\BinanceFuturesTrader\Global\auto_monitor_configs.json"
$backupPath = "$env:APPDATA\BinanceFuturesTrader\Global\auto_monitor_configs_backup_fix.json"

Write-Host "Converting config format..." -ForegroundColor Yellow

# Backup original file
Copy-Item $configPath $backupPath -Force
Write-Host "Backup created" -ForegroundColor Green

# Read and convert
$jsonContent = Get-Content $configPath -Raw -Encoding UTF8
$oldConfig = $jsonContent | ConvertFrom-Json

if ($oldConfig.accountConfigs) {
    Write-Host "Old format detected" -ForegroundColor Yellow
    
    $newConfigs = @()
    foreach ($accountName in $oldConfig.accountConfigs.PSObject.Properties.Name) {
        $accountConfig = $oldConfig.accountConfigs.$accountName
        Write-Host "Converting: $accountName -> $($accountConfig.name)" -ForegroundColor Cyan
        $newConfigs += $accountConfig
    }
    
    $newJson = $newConfigs | ConvertTo-Json -Depth 10
    $newJson | Out-File $configPath -Encoding UTF8
    Write-Host "Conversion completed!" -ForegroundColor Green
} else {
    Write-Host "Already new format" -ForegroundColor Green
} 