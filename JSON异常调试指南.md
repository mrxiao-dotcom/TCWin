# JSON异常调试指南

## 问题描述

当选择实盘（非测试网）时出现 `System.Text.Json.JsonException` 异常。

## 可能原因

1. **API密钥问题**
   - API密钥无效或已过期
   - API密钥权限不足（需要期货交易权限）
   - Secret Key不正确

2. **网络和API问题**
   - 网络连接问题
   - 币安API服务暂时不可用
   - API返回了错误信息而不是预期的JSON数据

3. **数据格式问题**
   - 币安API返回格式与预期模型不匹配
   - JSON字段名称或类型变化

## 调试步骤

### 第1步：检查控制台输出

运行应用程序后，查看控制台输出（Visual Studio的"输出"窗口），寻找以下信息：

```
=== Setting Account Configuration ===
Account Name: [您的账户名]
API Key: [密钥前8位]...[密钥后4位]
Secret Key: ***SET***
Is Test Net: False
Using Production Network: https://fapi.binance.com
=== Account Configuration Complete ===
```

### 第2步：查看API调用日志

查找以下类型的日志消息：

#### 成功的API调用：
```
✅ Account: [账户名]
✅ API Key: Configured
✅ Secret Key: Configured
✅ Network: Production
✅ Base URL: https://fapi.binance.com
🚀 Attempting to get real account info from Binance API...
📄 Raw API Response (first 200 chars): {"totalWalletBalance":"1000.00000000"...
✅ Successfully retrieved real account data from API
```

#### 失败的API调用：
```
❌ API returned error response: {"code":-2014,"msg":"API-key format invalid."}
❌ JSON Deserialization Error: The JSON value could not be converted to...
❌ JSON Path: $.someProperty
```

### 第3步：常见错误代码

| 错误代码 | 说明 | 解决方案 |
|---------|------|----------|
| -2014 | API密钥格式无效 | 检查API Key是否正确复制 |
| -1022 | 签名无效 | 检查Secret Key是否正确 |
| -2015 | API密钥无效、IP不在白名单等 | 检查API密钥状态和IP限制 |
| -4003 | 数量精度错误 | 调整交易数量精度 |

### 第4步：验证API密钥

在币安官网验证：
1. 登录币安账户
2. 进入"API管理"
3. 检查API密钥状态是否为"启用"
4. 确认已勾选"启用期货"权限
5. 检查IP访问限制（如果设置了IP白名单）

### 第5步：测试网络连接

可以先尝试：
1. 在账户配置中勾选"测试网络"
2. 看是否在测试网环境下正常工作
3. 如果测试网正常，说明是实盘API配置问题

## 解决方案

### 方案1：重新生成API密钥
1. 在币安官网删除现有API密钥
2. 重新创建API密钥
3. 确保启用期货交易权限
4. 在应用中更新API配置

### 方案2：检查网络和防火墙
1. 确保可以访问 `https://fapi.binance.com`
2. 检查防火墙是否阻止了HTTP请求
3. 如果在公司网络，检查代理设置

### 方案3：使用测试网络调试
1. 申请币安测试网API密钥
2. 在账户配置中勾选"测试网络"
3. 使用测试网进行调试

## 改进功能

最新版本已添加以下调试功能：

1. **详细的控制台日志**：显示API请求和响应的详细信息
2. **JSON错误捕获**：专门处理JSON反序列化异常
3. **错误响应检测**：自动识别币安API错误响应
4. **优雅降级**：API失败时自动切换到模拟数据

## 获取帮助

如果问题仍然存在，请：

1. **复制完整的控制台输出**（隐藏敏感信息）
2. **说明具体的操作步骤**
3. **提供错误发生的时间点**

这将帮助快速定位问题根源。

## 安全提醒

- 🔒 永远不要在公共场所分享完整的API密钥
- 🔒 定期轮换API密钥
- 🔒 只授予必要的权限
- 🔒 使用IP白名单限制访问 