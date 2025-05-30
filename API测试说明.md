# API测试说明

## 当前状态

✅ **API集成已完成**，程序现在支持真实币安API调用

## 测试方法

### 1. 模拟数据测试（默认）
- **无需配置**：直接运行程序即可看到模拟数据
- **控制台输出**：会显示 "Using mock data: No API configuration"
- **适用场景**：功能测试、界面测试、学习使用

### 2. 真实API测试
- **需要配置**：在账户配置中填入真实的API Key和Secret Key
- **控制台输出**：会显示 "Attempting to get real account info from Binance API..."
- **适用场景**：真实交易、实时数据

## 如何切换到真实API

### 步骤1：获取币安API密钥
1. 登录币安官网
2. 进入"API管理"
3. 创建新API密钥
4. 开启权限：
   - ✅ 现货与杠杆交易
   - ✅ 期货交易
   - ❌ 提币权限（保持关闭）

### 步骤2：配置程序
1. 点击"账户配置"按钮
2. 添加新账户或编辑现有账户
3. 填入API Key和Secret Key
4. 选择是否使用测试网
5. 保存配置

### 步骤3：选择账户
1. 在主界面顶部下拉框选择配置好的账户
2. 程序会自动尝试连接API
3. 观察控制台输出确认连接状态

## 控制台调试信息

### 成功连接API时：
```
Attempting to get real account info from Binance API...
Successfully retrieved real account data from API
Attempting to get real positions from Binance API...
Successfully retrieved 5 positions from API
Attempting to get real orders from Binance API...
Successfully retrieved 2 orders from API
```

### API连接失败时：
```
API Error: 401, {"code":-2014,"msg":"API-key format invalid."}
API call failed, falling back to mock data
```

### 无API配置时：
```
Using mock data: No API configuration
Using mock positions: No API configuration
Using mock orders: No API configuration
```

## 常见问题排查

### 1. 显示模拟数据而不是真实数据
**原因**：
- 没有配置API密钥
- API密钥格式错误
- API权限不足
- 网络连接问题

**解决方法**：
1. 检查API密钥是否正确填写
2. 确认API权限已开启期货交易
3. 检查网络连接
4. 查看控制台错误信息

### 2. API调用失败
**可能原因**：
- API密钥过期或被删除
- IP地址不在白名单中
- 请求频率过高
- 系统时间不同步

**解决方法**：
1. 重新生成API密钥
2. 设置IP白名单或使用无限制IP
3. 降低刷新频率
4. 同步系统时间

### 3. 测试网连接问题
**注意事项**：
- 测试网需要单独的API密钥
- 测试网地址：https://testnet.binancefuture.com
- 测试网数据与正式网不同步

## 安全建议

### 测试阶段
1. **优先使用测试网**：避免意外操作真实资金
2. **小额测试**：如使用正式网，先用小额资金测试
3. **只读权限**：初期可以只开启读取权限，不开启交易权限

### 生产使用
1. **权限最小化**：只开启必要的API权限
2. **IP白名单**：限制API密钥的使用IP
3. **定期更换**：定期更换API密钥
4. **监控日志**：关注API调用日志和异常

## 界面优化

### 新的布局特点
1. **杠杆按钮集成**：杠杆快捷按钮现在位于下单区域顶部
2. **紧凑布局**：移除了独立的杠杆设置区域
3. **按钮尺寸优化**：下单按钮和其他按钮尺寸更合理
4. **一致性**：所有操作按钮保持一致的尺寸

### 使用体验
- **更直观**：杠杆设置就在下单参数旁边
- **更紧凑**：减少了界面空间占用
- **更高效**：操作流程更加顺畅

## 下一步测试建议

1. **功能测试**：先在模拟数据模式下测试所有功能
2. **测试网验证**：配置测试网API进行真实连接测试
3. **小额验证**：在正式网用小额资金验证交易功能
4. **完整测试**：确认所有功能正常后进行完整测试

---

**⚠️ 重要提醒**
- 真实API调用会产生实际交易，请谨慎操作
- 建议先在测试网环境充分测试
- 确保理解所有功能后再使用真实资金 