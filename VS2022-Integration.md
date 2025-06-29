# 🎯 Visual Studio 2022 集成自动版本管理

为 **Visual Studio 2022** 提供完整的自动版本管理集成方案，让你在IDE中直接使用版本管理功能。

## 🚀 集成方式

### 方式一：外部工具集成（推荐）

#### 📋 设置步骤

1. **打开外部工具配置**
   - 菜单：`工具` → `外部工具...`

2. **添加版本管理工具**
   - 点击 `添加` 按钮
   - 配置以下信息：

   ```
   标题: 📦 版本升级 (Patch)
   命令: powershell.exe
   参数: -ExecutionPolicy Bypass -File "$(ProjectDir)AutoVersion.ps1" -VersionType patch -Interactive
   初始目录: $(ProjectDir)
   使用输出窗口: ✓
   提示输入参数: ✓
   ```

3. **添加多个版本类型工具**

   **工具2 - Minor版本升级：**
   ```
   标题: 📈 版本升级 (Minor)
   命令: powershell.exe
   参数: -ExecutionPolicy Bypass -File "$(ProjectDir)AutoVersion.ps1" -VersionType minor -Interactive
   初始目录: $(ProjectDir)
   使用输出窗口: ✓
   提示输入参数: ✓
   ```

   **工具3 - Major版本升级：**
   ```
   标题: 🎯 版本升级 (Major)
   命令: powershell.exe
   参数: -ExecutionPolicy Bypass -File "$(ProjectDir)AutoVersion.ps1" -VersionType major -Interactive
   初始目录: $(ProjectDir)
   使用输出窗口: ✓
   提示输入参数: ✓
   ```

   **工具4 - 编译并升级版本：**
   ```
   标题: 🔨 编译并升级版本
   命令: $(ProjectDir)build-with-version.bat
   参数: patch
   初始目录: $(ProjectDir)
   使用输出窗口: ✓
   提示输入参数: ✓
   ```

#### 🎮 使用方法
配置完成后，在 `工具` 菜单中会出现新的选项：
- 📦 版本升级 (Patch)
- 📈 版本升级 (Minor)  
- 🎯 版本升级 (Major)
- 🔨 编译并升级版本

### 方式二：构建后事件自动升级

#### 📋 设置步骤

1. **右键项目** → `属性`
2. **构建事件** → `生成后事件命令行`
3. **添加以下命令**：

```bash
REM 构建成功后询问是否升级版本
echo ✅ 构建成功！
set /p upgrade="🚀 是否升级版本号？ [Y/n]: "
if /i not "%upgrade%"=="n" (
    powershell -ExecutionPolicy Bypass -File "$(ProjectDir)AutoVersion.ps1" -VersionType patch -UpdateMessage "构建后自动升级"
)
```

#### ⚠️ 注意事项
- 这种方式会在每次成功构建后提示升级版本
- 适合频繁开发测试的场景
- 如果不想每次都提示，可以修改脚本逻辑

### 方式三：自定义快捷键

#### 📋 设置步骤

1. **菜单：** `工具` → `选项`
2. **环境** → `键盘`
3. **在"显示命令包含"中搜索：** `Tools.ExternalCommand`
4. **为外部工具分配快捷键：**
   - `Tools.ExternalCommand1` (版本升级 Patch): `Ctrl+Shift+V, P`
   - `Tools.ExternalCommand2` (版本升级 Minor): `Ctrl+Shift+V, M`  
   - `Tools.ExternalCommand3` (版本升级 Major): `Ctrl+Shift+V, J`
   - `Tools.ExternalCommand4` (编译并升级): `Ctrl+Shift+B, V`

#### 🎮 使用方法
设置完成后可以使用快捷键：
- `Ctrl+Shift+V, P` - Patch版本升级
- `Ctrl+Shift+V, M` - Minor版本升级
- `Ctrl+Shift+V, J` - Major版本升级
- `Ctrl+Shift+B, V` - 编译并升级版本

### 方式四：解决方案资源管理器右键菜单

#### 📋 创建批处理启动器

创建简化的批处理文件，让右键菜单更简洁：

1. **创建快捷启动文件：**

**vs-version-patch.bat**
```batch
@echo off
cd /d "%~dp0"
powershell -ExecutionPolicy Bypass -File "AutoVersion.ps1" -VersionType patch -Interactive
pause
```

**vs-version-minor.bat**
```batch
@echo off
cd /d "%~dp0"
powershell -ExecutionPolicy Bypass -File "AutoVersion.ps1" -VersionType minor -Interactive
pause
```

2. **在解决方案资源管理器中右键项目** → `在文件资源管理器中打开文件夹`
3. **双击对应的批处理文件**即可启动版本管理

### 方式五：Visual Studio 扩展集成

#### 📋 自定义命令配置

创建 `.vs` 文件夹配置（Visual Studio 2022支持）：

**创建 `.vs/launch.vs.json`：**
```json
{
  "version": "0.2.1",
  "configurations": [
    {
      "type": "default",
      "project": "BinanceFuturesTrader.csproj",
      "name": "版本管理 - Patch升级",
      "program": "powershell.exe",
      "args": [
        "-ExecutionPolicy", "Bypass",
        "-File", "AutoVersion.ps1",
        "-VersionType", "patch",
        "-Interactive"
      ],
      "currentDir": "${workspaceFolder}"
    }
  ]
}
```

## 🛠️ MSBuild 集成（高级）

### 自定义 MSBuild Target

在 `BinanceFuturesTrader.csproj` 中添加：

```xml
<!-- 版本管理相关的MSBuild Target -->
<Target Name="VersionBumpPatch" BeforeTargets="Build">
  <Message Text="🚀 准备升级版本..." />
  <Exec Command="powershell -ExecutionPolicy Bypass -File &quot;$(ProjectDir)AutoVersion.ps1&quot; -VersionType patch -UpdateMessage &quot;自动构建升级&quot;" 
        ContinueOnError="false" 
        Condition="'$(AutoVersion)' == 'true'" />
</Target>

<Target Name="VersionBumpMinor">
  <Exec Command="powershell -ExecutionPolicy Bypass -File &quot;$(ProjectDir)AutoVersion.ps1&quot; -VersionType minor -Interactive" />
</Target>

<Target Name="VersionBumpMajor">
  <Exec Command="powershell -ExecutionPolicy Bypass -File &quot;$(ProjectDir)AutoVersion.ps1&quot; -VersionType major -Interactive" />
</Target>
```

#### 🎮 使用方法
```bash
# 在包管理器控制台中运行
dotnet msbuild -t:VersionBumpMinor
dotnet msbuild -t:VersionBumpMajor

# 构建时自动升级版本
dotnet build -p:AutoVersion=true
```

## 📊 推荐的开发工作流程

### 🔥 日常开发流程（推荐）

1. **开发阶段**：正常编写代码
2. **测试阶段**：本地测试功能
3. **版本升级**：使用 `工具` → `📦 版本升级 (Patch)` 
4. **提交代码**：Git commit 和 push

### 🚀 功能发布流程

1. **功能开发完成**
2. **全面测试**
3. **版本升级**：使用 `工具` → `📈 版本升级 (Minor)`
4. **构建发布版本**：使用 `🔨 编译并升级版本`
5. **发布部署**

### 📈 重大更新流程

1. **架构重构/重大功能**
2. **完整测试**
3. **版本升级**：使用 `工具` → `🎯 版本升级 (Major)`
4. **发布说明**：详细记录更新内容
5. **正式发布**

## 🎯 快捷键速查表

| 快捷键 | 功能 | 版本类型 |
|--------|------|----------|
| `Ctrl+Shift+V, P` | Patch升级 | 0.32.1 → 0.32.2 |
| `Ctrl+Shift+V, M` | Minor升级 | 0.32.1 → 0.33.0 |
| `Ctrl+Shift+V, J` | Major升级 | 0.32.1 → 1.0.0 |
| `Ctrl+Shift+B, V` | 编译并升级 | 自动选择类型 |

## 🔧 高级配置

### 自动检测更新内容

创建智能版本升级脚本，根据Git提交信息自动判断版本类型：

**smart-version.ps1**
```powershell
# 分析Git提交信息自动判断版本类型
$gitLog = git log --oneline -n 5
$hasMajor = $gitLog | Select-String "BREAKING|major|重构|架构"
$hasMinor = $gitLog | Select-String "feat|feature|新增|功能"
$hasPatch = $gitLog | Select-String "fix|bug|修复|优化"

if ($hasMajor) { $versionType = "major" }
elseif ($hasMinor) { $versionType = "minor" }
else { $versionType = "patch" }

Write-Host "🤖 自动检测版本类型: $versionType"
& .\AutoVersion.ps1 -VersionType $versionType -Interactive
```

### 团队共享配置

将外部工具配置导出为团队共享：

1. **导出配置**：`工具` → `导入和导出设置`
2. **选择性导出**：只导出外部工具设置
3. **团队共享**：将 `.vssettings` 文件加入版本控制

## 🎉 快速设置指南

### ⚡ 5分钟快速设置

1. **打开 VS2022**
2. **菜单：工具 → 外部工具**
3. **添加工具**：
   ```
   标题: 📦 版本升级
   命令: powershell.exe
   参数: -ExecutionPolicy Bypass -File "$(ProjectDir)AutoVersion.ps1" -VersionType patch -Interactive
   初始目录: $(ProjectDir)
   使用输出窗口: ✓
   ```
4. **设置快捷键**：`Ctrl+Shift+V, P`
5. **完成！** 现在可以使用快捷键或工具菜单升级版本

### 🔍 故障排除

#### 常见问题

**问题1：PowerShell执行策略错误**
```
解决方案：添加 -ExecutionPolicy Bypass 参数
```

**问题2：找不到AutoVersion.ps1文件**
```
解决方案：确保初始目录设置为 $(ProjectDir)
```

**问题3：中文乱码**
```
解决方案：在PowerShell参数中添加 -OutputEncoding UTF8
```

#### 调试技巧

1. **使用输出窗口**：勾选"使用输出窗口"查看详细信息
2. **单独测试**：在PowerShell中单独运行脚本确认功能
3. **权限检查**：确保PowerShell有写入权限

---

🎊 **恭喜！你现在拥有了完整的Visual Studio 2022版本管理集成！** 

选择适合你的工作流程的集成方式，享受高效的版本管理体验！ 