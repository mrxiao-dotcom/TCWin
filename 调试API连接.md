# API连接调试指南

## 问题诊断

如果您看到的是模拟数据而不是真实数据，请按以下步骤进行诊断：

## 步骤1：检查控制台输出

运行程序后，观察控制台窗口的输出信息：

### 正常API连接应该显示：
```
=== Setting Account Configuration ===
Account Name: 我的账户
API Key: 12345678...abcd
Secret Key: ***SET***
Is Test Net: False
Using Production Network: https://fapi.binance.com
=== Account Configuration Complete ===

--- Get Account Info ---
✅ Account: 我的账户
✅ API Key: Configured
✅ Secret Key: Configured
✅ Network: Production
✅ Base URL: https://fapi.binance.com
🚀 Attempting to get real account info from Binance API...
✅ Successfully retrieved real account data from API
   📊 Wallet Balance: 1000.50
   📊 Margin Balance: 200.30
   📊 Unrealized PnL: 15.25
```

### 如果API配置有问题会显示：
```
=== Setting Account Configuration ===
Account Name: 我的账户
API Key: NOT SET
Secret Key: NOT SET
Is Test Net: False
=== Account Configuration Complete ===

--- Get Account Info ---
❌ No account configured
❌ Using mock data: No API configuration
```

### 如果API调用失败会显示：
```
--- Get Account Info ---
✅ Account: 我的账户
✅ API Key: Configured
✅ Secret Key: Configured
🚀 Attempting to get real account info from Binance API...
❌ API Error: 401, {"code":-2014,"msg":"API-key format invalid."}
❌ API call failed, falling back to mock data
```

## 步骤2：检查账户配置

1. **点击"账户配置"按钮**
2. **检查现有账户配置**：
   - API Key是否已填写（应该是64字符的字符串）
   - Secret Key是否已填写（应该是64字符的字符串）
   - 测试网设置是否正确

3. **验证API密钥格式**：
   - API Key示例：`4B8F9A2E1D3C7F6E9A8B7C5D4E3F2A1B9C8D7E6F5A4B3C2D1E9F8A7B6C5D4E3F2`
   - Secret Key示例：`2A1B9C8D7E6F5A4B3C2D1E9F8A7B6C5D4E3F2A1B9C8D7E6F5A4B3C2D1E9F8A7B6`

## 步骤3：测试连接

### 方法1：选择账户测试
1. 在主界面顶部下拉框选择已配置的账户
2. 观察控制台输出
3. 查看状态栏信息

### 方法2：手动刷新测试
1. 选择账户后点击"刷新数据"按钮
2. 观察控制台输出和界面数据变化

### 方法3：价格更新测试
1. 在合约名输入框输入"BTCUSDT"
2. 点击"更新"按钮
3. 观察控制台输出：
```
--- Get Latest Price for BTCUSDT ---
🚀 Calling public API: https://fapi.binance.com/fapi/v1/ticker/price?symbol=BTCUSDT
✅ Successfully retrieved real price for BTCUSDT: 43250.50
```

## 常见问题解决

### 1. API Key显示"NOT SET"
**原因**：账户配置中没有保存API密钥
**解决**：
1. 点击"账户配置"
2. 编辑或添加账户
3. 正确填写API Key和Secret Key
4. 点击保存

### 2. API调用返回401错误
**原因**：API密钥无效或权限不足
**解决**：
1. 检查API密钥是否正确复制
2. 确认API密钥权限已开启期货交易
3. 检查API密钥是否已过期
4. 确认IP地址在白名单中（如有设置）

### 3. 网络连接失败
**原因**：网络问题或防火墙阻拦
**解决**：
1. 检查网络连接
2. 尝试关闭防火墙/VPN
3. 检查代理设置

### 4. 显示测试网数据
**原因**：账户配置为测试网络
**解决**：
1. 在账户配置中取消勾选"是否测试网络"
2. 或使用对应的测试网API密钥

## 验证真实数据

### 真实数据特征：
- **账户余额**：显示实际的USDT余额，不是固定的1000.0
- **持仓信息**：显示真实的持仓（可能为空）
- **价格数据**：BTCUSDT价格在40000-50000范围内波动，不是固定46000
- **状态信息**：控制台显示"Successfully retrieved real data"

### 模拟数据特征：
- **账户余额**：总是显示1000.0 USDT
- **持仓信息**：总是显示0.001 BTC持仓
- **价格数据**：BTCUSDT总是显示46000左右
- **状态信息**：控制台显示"Using mock data"

## 获取帮助

如果按照以上步骤仍然无法连接API，请：

1. **截图控制台输出**：显示详细的错误信息
2. **检查币安官网**：确认API服务状态
3. **验证账户状态**：确认币安账户正常且有期货权限
4. **联系技术支持**：提供完整的错误日志

---

**💡 提示**：
- 价格查询是公开API，不需要密钥，应该总是能成功
- 如果价格查询都失败，说明是网络连接问题
- 如果价格查询成功但账户信息失败，说明是API密钥问题 