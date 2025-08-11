# 🚀 自动版本管理系统

为 **币安期货交易管理器** 提供自动化的版本号升级和版本历史记录功能。

## 📦 系统组成

### 核心文件
- **`AutoVersion.ps1`** - 主要的版本管理PowerShell脚本
- **`version-bump.bat`** - Windows快捷批处理文件
- **`build-with-version.bat`** - 编译并版本管理的集成脚本
- **`VERSION_HISTORY.md`** - 版本历史记录文件
- **`version.json`** - 当前版本信息JSON文件（自动生成）

### 自动生成文件
- `version.json` - 每次版本升级后自动生成，包含完整版本信息
- `VERSION_HISTORY.md` - 自动维护版本历史记录

## 🎯 使用方法

### 方法一：快捷批处理（推荐）

#### 基本用法
```bash
# 默认patch版本升级（最常用）
version-bump

# 指定版本类型
version-bump patch    # 0.32.0 → 0.32.1
version-bump minor    # 0.32.0 → 0.33.0  
version-bump major    # 0.32.0 → 1.0.0

# 带更新说明
version-bump patch "修复自动盯盘bug"
version-bump minor "新增监控面板功能"

# 交互式模式（可输入多行更新内容）
version-bump -i

# 显示帮助
version-bump -h
```

### 方法二：直接调用PowerShell脚本

```powershell
# 基本升级
.\AutoVersion.ps1 -VersionType patch

# 带更新说明
.\AutoVersion.ps1 -VersionType minor -UpdateMessage "新增功能"

# 交互式模式
.\AutoVersion.ps1 -Interactive

# 指定项目文件
.\AutoVersion.ps1 -ProjectFile "CustomProject.csproj" -VersionType patch
```

### 方法三：编译集成模式

```bash
# 编译成功后提示升级版本
build-with-version

# 指定版本类型
build-with-version minor

# 带更新说明
build-with-version patch "修复关键bug"
```

## 📋 版本类型说明

| 类型 | 说明 | 示例 | 使用场景 |
|------|------|------|----------|
| **patch** | 修订版本 | 0.32.0 → 0.32.1 | Bug修复、小优化 |
| **minor** | 次版本 | 0.32.0 → 0.33.0 | 新功能、功能改进 |
| **major** | 主版本 | 0.32.0 → 1.0.0 | 重大更新、架构改变 |

## 🔧 系统功能

### ✅ 自动功能
- **版本号自动升级** - 自动更新项目文件中的版本信息
- **历史记录维护** - 自动更新 `VERSION_HISTORY.md`
- **版本信息导出** - 生成 `version.json` 供其他工具使用
- **编译集成** - 编译成功后可选择升级版本
- **多格式支持** - 同时更新 Version、AssemblyVersion、FileVersion

### 🎨 用户体验
- **彩色输出** - 清晰的状态显示和错误提示
- **交互式输入** - 支持多行更新说明输入
- **智能默认** - 合理的默认行为和参数
- **错误处理** - 完善的错误处理和回滚机制

### 📊 版本信息追踪
每次版本升级都会记录：
- 版本号变化（前后对比）
- 更新时间戳
- 更新类型（PATCH/MINOR/MAJOR）
- 详细更新内容
- 技术信息（AssemblyVersion、FileVersion等）
- 编译信息

## 📁 文件说明

### VERSION_HISTORY.md
自动维护的版本历史文件，包含：
- 📋 完整的版本历史记录
- 🚀 每个版本的更新内容
- 📊 技术信息和构建信息
- 🔧 架构变更说明
- 🐛 问题修复记录

### version.json
自动生成的版本信息文件，包含：
```json
{
  "version": "0.32.0",
  "assemblyVersion": "0.32.0.0", 
  "fileVersion": "0.32.0.0",
  "releaseDate": "2025-06-28 21:48:22",
  "updateType": "patch",
  "updateMessage": "修复自动盯盘bug",
  "previousVersion": "0.31.0"
}
```

## 🛡️ 安全特性

### 错误处理
- ✅ 项目文件备份（自动恢复）
- ✅ 版本格式验证
- ✅ 编译前验证
- ✅ 权限检查

### 数据保护
- ✅ UTF-8编码保证中文支持
- ✅ 文件锁定检测
- ✅ 历史记录完整性
- ✅ 回滚机制

## 🎮 使用示例

### 日常开发场景

```bash
# 1. 修复了一个小bug
version-bump patch "修复自动盯盘重复触发问题"

# 2. 添加了新功能
version-bump minor "新增移动止损配置对话框"

# 3. 重大架构改进
version-bump major "完成事件驱动架构重构"

# 4. 交互式详细记录
version-bump -i
# 然后输入多行更新内容：
# 修复自动盯盘系统多个问题
# 优化监控面板显示
# 改进错误处理机制
# （按两次回车结束）
```

### 集成开发流程

```bash
# 开发完成后，一键编译和版本管理
build-with-version minor "完成监控系统重构"

# 或者分步进行
dotnet build                    # 先编译
version-bump patch              # 编译成功后升级版本
```

## 📈 版本规划建议

### 版本升级策略
- **Daily Fixes** → `patch` (0.32.0 → 0.32.1)
- **Weekly Features** → `minor` (0.32.0 → 0.33.0)  
- **Monthly Major** → `major` (0.32.0 → 1.0.0)

### 更新内容建议
- **功能性更新**：具体描述新增功能
- **修复性更新**：说明修复的问题和影响
- **性能优化**：量化改进效果
- **架构改进**：说明技术优势

## 🎯 高级用法

### 自定义项目文件
```powershell
.\AutoVersion.ps1 -ProjectFile "MyProject.csproj" -VersionType minor
```

### 批量项目管理
```bash
# 为多个项目同时升级版本（需要自定义脚本）
for /d %%i in (*) do (
    if exist "%%i\*.csproj" (
        cd "%%i"
        ..\AutoVersion.ps1 -VersionType patch
        cd ..
    )
)
```

### CI/CD集成
可以在持续集成流水线中集成自动版本管理：
```yaml
# 示例：在成功构建后自动升级版本
- name: Auto Version Bump
  run: |
    .\AutoVersion.ps1 -VersionType patch -UpdateMessage "自动构建版本"
```

---

🎉 **享受自动化的版本管理吧！** 这个系统将帮助你轻松维护项目版本历史，让开发更加高效和规范。 