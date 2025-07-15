# ============================================================================
# Auto Version Upgrade Tool (AutoVersion.ps1)
# Function: Automatically upgrade BinanceFuturesTrader project version numbers
# Author: Trading Tools Team
# Created: 2025-01-25
# ============================================================================

param(
    [string]$VersionType = "patch",  # major, minor, patch
    [string]$ProjectFile = "BinanceFuturesTrader.csproj",
    [switch]$Preview,                # Preview mode, don't actually modify files
    [switch]$Help                    # Show help information
)

# Color output function
function Write-ColorText {
    param(
        [string]$Text,
        [string]$Color = "White"
    )
    
    $originalColor = $Host.UI.RawUI.ForegroundColor
    try {
        $Host.UI.RawUI.ForegroundColor = $Color
        Write-Host $Text
    } finally {
        $Host.UI.RawUI.ForegroundColor = $originalColor
    }
}

# Show help information
function Show-Help {
    Write-ColorText "Binance Futures Trader Version Upgrade Tool" "Cyan"
    Write-Host ""
    Write-Host "Usage:"
    Write-Host "  .\AutoVersion.ps1 [Options]"
    Write-Host ""
    Write-Host "Parameters:"
    Write-Host "  -VersionType <type>    Version upgrade type:"
    Write-Host "                         * major  - Major version (x.0.0)"
    Write-Host "                         * minor  - Minor version (x.y.0)"
    Write-Host "                         * patch  - Patch version (x.y.z) [default]"
    Write-Host "  -ProjectFile <file>    Project file path [default: BinanceFuturesTrader.csproj]"
    Write-Host "  -Preview              Preview mode, don't actually modify files"
    Write-Host "  -Help                 Show this help information"
    Write-Host ""
    Write-Host "Examples:"
    Write-Host "  .\AutoVersion.ps1                    # Upgrade patch version"
    Write-Host "  .\AutoVersion.ps1 -VersionType minor # Upgrade minor version"
    Write-Host "  .\AutoVersion.ps1 -Preview           # Preview upgrade results"
    Write-Host ""
}

# Version class
class Version {
    [int]$Major
    [int]$Minor
    [int]$Patch
    [int]$Build
    
    Version([string]$versionString) {
        $parts = $versionString.Split('.')
        $this.Major = [int]$parts[0]
        $this.Minor = if ($parts.Length -gt 1) { [int]$parts[1] } else { 0 }
        $this.Patch = if ($parts.Length -gt 2) { [int]$parts[2] } else { 0 }
        $this.Build = if ($parts.Length -gt 3) { [int]$parts[3] } else { 0 }
    }
    
    [string] ToString() {
        return "$($this.Major).$($this.Minor).$($this.Patch)"
    }
    
    [string] ToAssemblyString() {
        return "$($this.Major).$($this.Minor).$($this.Patch).$($this.Build)"
    }
    
    [void] UpgradeMajor() {
        $this.Major++
        $this.Minor = 0
        $this.Patch = 0
    }
    
    [void] UpgradeMinor() {
        $this.Minor++
        $this.Patch = 0
    }
    
    [void] UpgradePatch() {
        $this.Patch++
    }
}

# Read current version number
function Get-CurrentVersion {
    param([string]$ProjectPath)
    
    if (-not (Test-Path $ProjectPath)) {
        throw "Project file does not exist: $ProjectPath"
    }
    
    $content = Get-Content $ProjectPath -Raw -Encoding UTF8
    
    # Extract version information
    $versionMatch = [regex]::Match($content, '<Version>([\d\.]+)</Version>')
    $assemblyVersionMatch = [regex]::Match($content, '<AssemblyVersion>([\d\.]+)</AssemblyVersion>')
    $fileVersionMatch = [regex]::Match($content, '<FileVersion>([\d\.]+)</FileVersion>')
    
    if (-not $versionMatch.Success) {
        throw "Cannot find version information in project file"
    }
    
    return @{
        Version = $versionMatch.Groups[1].Value
        AssemblyVersion = if ($assemblyVersionMatch.Success) { $assemblyVersionMatch.Groups[1].Value } else { $versionMatch.Groups[1].Value + ".0" }
        FileVersion = if ($fileVersionMatch.Success) { $fileVersionMatch.Groups[1].Value } else { $versionMatch.Groups[1].Value + ".0" }
    }
}

# Update version numbers in project file
function Update-ProjectVersion {
    param(
        [string]$ProjectPath,
        [Version]$NewVersion,
        [bool]$DryRun = $false
    )
    
    $content = Get-Content $ProjectPath -Raw -Encoding UTF8
    $originalContent = $content
    
    # Update various version tags
    $content = $content -replace '<Version>[\d\.]+</Version>', "<Version>$($NewVersion.ToString())</Version>"
    $content = $content -replace '<AssemblyVersion>[\d\.]+</AssemblyVersion>', "<AssemblyVersion>$($NewVersion.ToAssemblyString())</AssemblyVersion>"
    $content = $content -replace '<FileVersion>[\d\.]+</FileVersion>', "<FileVersion>$($NewVersion.ToAssemblyString())</FileVersion>"
    
    if ($DryRun) {
        Write-ColorText "Preview Mode - The following changes will be made:" "Yellow"
        Write-Host ""
        Write-ColorText "File: $ProjectPath" "Gray"
        
        # Show change differences
        $originalLines = $originalContent -split "`r?`n"
        $newLines = $content -split "`r?`n"
        
        for ($i = 0; $i -lt $originalLines.Length; $i++) {
            if ($originalLines[$i] -ne $newLines[$i]) {
                Write-ColorText "- $($originalLines[$i])" "Red"
                Write-ColorText "+ $($newLines[$i])" "Green"
            }
        }
        Write-Host ""
    } else {
        # Actually write to file
        [System.IO.File]::WriteAllText($ProjectPath, $content, [System.Text.Encoding]::UTF8)
        Write-ColorText "Updated project file: $ProjectPath" "Green"
    }
}

# Generate changelog entry
function Generate-ChangelogEntry {
    param([Version]$NewVersion, [string]$VersionType)
    
    $date = Get-Date -Format "yyyy-MM-dd"
    $changeType = switch ($VersionType) {
        "major" { "Major Update" }
        "minor" { "Feature Update" }
        "patch" { "Bug Fixes" }
        default { "Version Update" }
    }
    
    $entry = @"

## [$($NewVersion.ToString())] - $date

### $changeType
- Automatic version upgrade
- Version type: $VersionType

"@
    
    return $entry
}

# Main function
function Main {
    try {
        # Show help
        if ($Help) {
            Show-Help
            return
        }
        
        Write-ColorText "Binance Futures Trader Version Upgrade Tool" "Cyan"
        Write-Host ""
        
        # Validate parameters
        if ($VersionType -notmatch "^(major|minor|patch)$") {
            Write-ColorText "Error: Invalid version type '$VersionType'. Please use major, minor, or patch." "Red"
            Write-ColorText "Use -Help parameter for detailed help." "Yellow"
            return
        }
        
        # Check project file
        if (-not (Test-Path $ProjectFile)) {
            Write-ColorText "Error: Cannot find project file '$ProjectFile'" "Red"
            return
        }
        
        # Read current version
        Write-ColorText "Reading current version information..." "Blue"
        $currentVersionInfo = Get-CurrentVersion -ProjectPath $ProjectFile
        $currentVersion = [Version]::new($currentVersionInfo.Version)
        
        Write-Host "Current version information:"
        Write-Host "  * Version: $($currentVersionInfo.Version)"
        Write-Host "  * AssemblyVersion: $($currentVersionInfo.AssemblyVersion)"
        Write-Host "  * FileVersion: $($currentVersionInfo.FileVersion)"
        Write-Host ""
        
        # Upgrade version number
        $newVersion = [Version]::new($currentVersion.ToString())
        switch ($VersionType) {
            "major" { $newVersion.UpgradeMajor() }
            "minor" { $newVersion.UpgradeMinor() }
            "patch" { $newVersion.UpgradePatch() }
        }
        
        # Show upgrade plan
        Write-ColorText "Version upgrade plan:" "Blue"
        Write-Host "  Upgrade type: $VersionType"
        Write-ColorText "  Current version: $($currentVersion.ToString())" "Yellow"
        Write-ColorText "  New version: $($newVersion.ToString())" "Green"
        Write-Host ""
        
        if ($Preview) {
            Write-ColorText "Preview mode enabled, files will not be actually modified" "Yellow"
            Write-Host ""
        }
        
        # Update project file
        Write-ColorText "Updating project file..." "Blue"
        Update-ProjectVersion -ProjectPath $ProjectFile -NewVersion $newVersion -DryRun $Preview
        
        # Generate changelog suggestion
        if (-not $Preview) {
            $changelogEntry = Generate-ChangelogEntry -NewVersion $newVersion -VersionType $VersionType
            Write-Host ""
            Write-ColorText "Suggested changelog entry:" "Blue"
            Write-ColorText $changelogEntry "Gray"
        }
        
        # Completion message
        Write-Host ""
        if ($Preview) {
            Write-ColorText "Preview completed! Run without -Preview parameter to actually execute the upgrade." "Yellow"
        } else {
            Write-ColorText "Version upgrade completed!" "Green"
            Write-Host ""
            Write-ColorText "Next steps:" "Blue"
            Write-Host "  1. Check the updated project file"
            Write-Host "  2. Update CHANGELOG.md file"
            Write-Host "  3. Commit version changes to version control"
            Write-Host "  4. Create version tag: git tag v$($newVersion.ToString())"
        }
        
    } catch {
        Write-ColorText "Error: $($_.Exception.Message)" "Red"
        if ($_.Exception.InnerException) {
            Write-ColorText "Details: $($_.Exception.InnerException.Message)" "Red"
        }
        exit 1
    }
}

# Run main function
Main 