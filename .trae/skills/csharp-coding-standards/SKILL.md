---
name: "csharp-coding-standards"
description: "Defines C# coding standards based on professional software company best practices. Invoke when writing new C# code, reviewing existing code, or asked to follow coding conventions."
---

# C# 编程规范（专业软件公司标准）

本规范参照国际专业软件公司的 C# 编码最佳实践制定，适用于所有 C# 项目开发。

---

## 一、强制性要求

### 1.1 每行代码必须添加中文注释

> **所有代码行都必须附带中文注释**，确保代码的可读性和可维护性。

**正确示例：**

```csharp
/// <summary>
/// 计算两个数值的加权平均值
/// </summary>
/// <param name="values">数值集合</param>
/// <param name="weights">权重集合</param>
/// <returns>加权平均值</returns>
public double CalculateWeightedAverage(double[] values, double[] weights)
{
    // 检查输入参数是否为空
    if (values == null || weights == null)
    {
        // 输入为空时抛出异常
        throw new ArgumentNullException("输入参数不能为空");
    }

    // 检查两个数组长度是否一致
    if (values.Length != weights.Length)
    {
        // 长度不一致时抛出异常
        throw new ArgumentException("数值数组和权重数组长度必须相同");
    }

    // 初始化分子累加器
    double numerator = 0;
    // 初始化分母累加器
    double denominator = 0;

    // 遍历所有数值进行计算
    for (int i = 0; i < values.Length; i++)
    {
        // 累加加权值到分子
        numerator += values[i] * weights[i];
        // 累加权重到分母
        denominator += weights[i];
    }

    // 检查分母是否为零
    if (denominator == 0)
    {
        // 分母为零时返回零
        return 0;
    }

    // 返回加权平均值
    return numerator / denominator;
}
```

### 1.2 命名规范

#### 1.2.1 PascalCase（大驼峰命名法）

| 类别 | 规范 | 示例 |
|------|------|------|
| 类名 | PascalCase | `UserAccount`, `DeviceManager` |
| 方法名 | PascalCase | `GetUserById()`, `SaveChanges()` |
| 属性名 | PascalCase | `UserName`, `DeviceStatus` |
| 接口名 | 以 `I` 开头 | `IUserService`, `IRepository` |
| 枚举名 | PascalCase | `DeviceType`, `UserRole` |
| 命名空间 | PascalCase | `ChargeDischargeSystem.Core` |

#### 1.2.2 camelCase（小驼峰命名法）

| 类别 | 规范 | 示例 |
|------|------|------|
| 局部变量 | camelCase | `userName`, `deviceList` |
| 方法参数 | camelCase | `string userId`, `int pageIndex` |
| 私有字段 | _camelCase | `_userRepository`, `_connectionString` |

#### 1.2.3 命名约定

```csharp
// 使用有意义的名称，不要使用缩写
private string _userDisplayName;  // 正确：完整且有意义的名称
private string _udn;              // 错误：难以理解的缩写

// 布尔属性使用肯定句式
public bool IsActive { get; set; }       // 正确
public bool IsNotActive { get; set; }    // 错误：避免否定形式

// 集合属性使用复数形式
public List<UserAccount> Users { get; set; }         // 正确
public List<UserAccount> UserList { get; set; }      // 正确
public List<UserAccount> User { get; set; }           // 错误：单数形式
```

### 1.3 代码组织

```csharp
// 文件顶部：声明文件用途
// ============================================================
// 文件名: UserService.cs
// 用途: 提供用户管理相关的业务逻辑服务
// ============================================================

// 区域 1：外部依赖引用
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

// 区域 2：命名空间声明
namespace ChargeDischargeSystem.Core.Services
{
    // 区域 3：类注释
    /// <summary>
    /// 用户管理服务，处理用户认证、授权和账户管理
    /// </summary>
    public class UserService : IUserService
    {
        // 区域 4：私有字段
        // 用户仓储接口实例
        private readonly IUserRepository _userRepository;
        // 日志记录器实例
        private readonly ILogger _logger;

        // 区域 5：构造函数
        /// <summary>
        /// 初始化用户服务实例
        /// </summary>
        /// <param name="userRepository">用户仓储</param>
        /// <param name="logger">日志记录器</param>
        public UserService(IUserRepository userRepository, ILogger logger)
        {
            // 仓储实例赋值
            _userRepository = userRepository ?? throw new ArgumentNullException(nameof(userRepository));
            // 日志器实例赋值
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        // 区域 6：公开方法
        // (方法实现...)

        // 区域 7：私有方法
        // (辅助方法实现...)
    }
}
```

---

## 二、代码格式规范

### 2.1 缩进与空格

```csharp
// 使用 4 个空格缩进，不使用 Tab
public class SampleClass
{
    // 方法体和周围代码之间留一个空行
    public void SampleMethod()
    {
        // 二元运算符两侧加空格
        int result = a + b;

        // 逗号后加空格
        Method(a, b, c);

        // 强制转换后加空格
        int value = (int)obj;
    }
}
```

### 2.2 大括号风格

```csharp
// Allman 风格：左大括号换行
public class DeviceManager               // 左大括号换行
{                                        // 左大括号换行
    public void ProcessData()            // 左大括号换行
    {                                    // 左大括号换行
        if (condition)                   // 左大括号换行
        {                                // 左大括号换行
            // 执行操作
        }                                // 右大括号独立一行
        else                             // 左大括号换行
        {                                // 左大括号换行
            // 执行其他操作
        }                                // 右大括号独立一行
    }                                    // 右大括号独立一行
}                                        // 右大括号独立一行
```

---

## 三、最佳实践规范

### 3.1 异常处理

```csharp
/// <summary>
/// 根据用户标识获取用户信息
/// </summary>
/// <param name="userId">用户唯一标识</param>
/// <returns>用户账户信息</returns>
public async Task<UserAccount> GetUserByIdAsync(string userId)
{
    // 检查输入参数是否为空或空白
    if (string.IsNullOrWhiteSpace(userId))
    {
        // 参数无效时抛出异常
        throw new ArgumentException("用户ID不能为空", nameof(userId));
    }

    try
    {
        // 调用仓储层查询用户
        UserAccount user = await _userRepository.GetByIdAsync(userId);

        // 检查查询结果是否为空
        if (user == null)
        {
            // 用户不存在时记录警告日志
            _logger.LogWarning("用户未找到: {UserId}", userId);
            // 返回空结果
            return null;
        }

        // 返回查询到的用户
        return user;
    }
    catch (Exception ex)
    {
        // 记录异常日志
        _logger.LogError(ex, "获取用户信息失败: {UserId}", userId);
        // 重新抛出异常，保留原始堆栈信息
        throw;
    }
}
```

### 3.2 异步编程

```csharp
/// <summary>
/// 异步方法命名以 Async 结尾
/// </summary>
/// <param name="deviceId">设备标识</param>
/// <returns>设备运行状态</returns>
public async Task<DeviceStatus> GetDeviceStatusAsync(string deviceId)
{
    // 使用 ConfigureAwait(false) 避免上下文捕获
    DeviceData data = await _deviceRepository.GetDataAsync(deviceId).ConfigureAwait(false);

    // 解析设备状态
    DeviceStatus status = ParseDeviceStatus(data);

    // 返回设备状态
    return status;
}

// 避免 async void，除事件处理器外
// 正确：返回 Task
public async Task SaveDataAsync(DeviceData data) { }

// 错误：不要使用 async void
// public async void SaveDataAsync(DeviceData data) { }

// 事件处理器可以使用 async void
public async void OnButtonClick(object sender, EventArgs e)
{
    // 按钮点击事件处理
    await SaveDataAsync(_currentData);
}
```

### 3.3 依赖注入

```csharp
/// <summary>
/// 设备监控服务，负责设备数据采集和状态监控
/// </summary>
public class DeviceMonitoringService
{
    // 设备数据仓储接口
    private readonly IDeviceRepository _deviceRepository;
    // 告警服务接口
    private readonly IAlertService _alertService;
    // 日志记录器
    private readonly ILogger<DeviceMonitoringService> _logger;

    /// <summary>
    /// 构造函数注入所有依赖
    /// </summary>
    /// <param name="deviceRepository">设备数据仓储</param>
    /// <param name="alertService">告警服务</param>
    /// <param name="logger">日志记录器</param>
    public DeviceMonitoringService(
        IDeviceRepository deviceRepository,
        IAlertService alertService,
        ILogger<DeviceMonitoringService> logger)
    {
        // 仓储赋值
        _deviceRepository = deviceRepository;
        // 告警服务赋值
        _alertService = alertService;
        // 日志器赋值
        _logger = logger;
    }
}
```

### 3.4 LINQ 使用规范

```csharp
/// <summary>
/// 查询活跃设备列表
/// </summary>
/// <returns>活跃设备集合</returns>
public IEnumerable<Device> GetActiveDevices()
{
    // 使用 LINQ 方法链进行查询
    return _deviceRepository.GetAll()
        // 筛选状态为活跃的设备
        .Where(d => d.Status == DeviceStatus.Active)
        // 按最后通信时间降序排序
        .OrderByDescending(d => d.LastCommunicationTime)
        // 转换为列表
        .ToList();
}

// 避免在循环中使用 LINQ 查询
// 正确：先执行查询再遍历
List<Device> activeDevices = GetActiveDevices();
foreach (Device device in activeDevices)
{
    // 处理每个活跃设备
    ProcessDevice(device);
}

// 错误：不要在循环中重复查询
// foreach (Device device in GetActiveDevices())  // 每次迭代都执行查询
// {
//     ProcessDevice(device);
// }
```

---

## 四、类与接口设计

### 4.1 类的设计原则

```csharp
/// <summary>
/// 用户管理仓储，封装所有用户数据的持久化操作
/// </summary>
public class UserRepository : IUserRepository
{
    /// <summary>
    /// 类应该小而专注，遵循单一职责原则
    /// 只负责用户数据的 CRUD 操作
    /// </summary>
}

// 优先使用组合而非继承
public class DeviceProcessor
{
    // 通过组合引入依赖功能
    private readonly IDataParser _dataParser;
    private readonly IDataValidator _dataValidator;

    /// <summary>
    /// 构造函数注入所需组件
    /// </summary>
    public DeviceProcessor(IDataParser dataParser, IDataValidator dataValidator)
    {
        // 数据解析器赋值
        _dataParser = dataParser;
        // 数据验证器赋值
        _dataValidator = dataValidator;
    }
}
```

### 4.2 接口设计

```csharp
/// <summary>
/// 设备仓储接口，定义设备数据的持久化操作契约
/// </summary>
public interface IDeviceRepository
{
    /// <summary>
    /// 根据标识获取设备信息
    /// </summary>
    Task<Device> GetByIdAsync(string deviceId);

    /// <summary>
    /// 获取所有设备列表
    /// </summary>
    Task<List<Device>> GetAllAsync();

    /// <summary>
    /// 保存设备信息
    /// </summary>
    Task SaveAsync(Device device);

    /// <summary>
    /// 删除指定设备
    /// </summary>
    Task DeleteAsync(string deviceId);
}
```

---

## 五、注释规范

### 5.1 XML 文档注释

```csharp
/// <summary>
/// 所有公开的类、方法、属性和接口都必须包含 XML 文档注释
/// </summary>
public class DocumentedClass
{
    /// <summary>
    /// 获取或设置用户显示名称
    /// </summary>
    public string DisplayName { get; set; }

    /// <summary>
    /// 处理设备上报的数据
    /// </summary>
    /// <param name="deviceId">设备唯一标识</param>
    /// <param name="data">原始数据内容</param>
    /// <returns>处理后的结果对象</returns>
    /// <exception cref="ArgumentNullException">当设备标识为空时抛出</exception>
    /// <exception cref="DeviceNotFoundException">当设备不存在时抛出</exception>
    public ProcessingResult ProcessDeviceData(string deviceId, byte[] data)
    {
        // 方法实现...
    }
}
```

### 5.2 行内注释

```csharp
// 每一行代码都需要有中文注释

// 注释应该解释"为什么这样做"而非"做什么"
// 正确：注释说明意图
// 使用二分查找提高大数组的搜索性能
int index = Array.BinarySearch(sortedArray, targetValue);

// 正确：注释说明边界条件
// 当用户年龄超过 60 岁时启用退休金计算逻辑
if (user.Age > 60)
{
    // 调用退休金计算模块
    CalculatePension(user);
}
```

---

## 六、文件与目录结构

### 6.1 项目结构

```
ProjectName/
├── src/
│   ├── ProjectName.Core/          # 核心业务逻辑
│   │   ├── Interfaces/             # 接口定义
│   │   ├── Models/                 # 数据模型
│   │   ├── Services/               # 业务服务
│   │   └── Extensions/             # 扩展方法
│   ├── ProjectName.Data/           # 数据访问层
│   │   ├── Repositories/           # 仓储实现
│   │   └── Database/               # 数据库配置
│   └── ProjectName.App/            # 应用程序层
│       ├── ViewModels/             # 视图模型
│       ├── Views/                  # 视图文件
│       └── Resources/              # 资源文件
```

### 6.2 文件命名规则

- 文件名必须与其中包含的主类名保持一致
- 文件名采用 PascalCase
- 一个文件只包含一个主要类

---

## 七、禁止事项

❌ 禁止使用 `var` 关键字（除非类型在右侧显而易见，如 `new Dictionary<int, string>()`）
❌ 禁止使用魔术数字，必须定义为常量
❌ 禁止使用 `#region` 来隐藏长方法（说明方法太长需要重构）
❌ 禁止使用 `goto` 语句
❌ 禁止 catch 异常后不做任何处理（空的 catch 块）
❌ 禁止在循环中使用 `try-catch`（应放在循环外部）
❌ 禁止方法超过 50 行（超出应拆分为多个小方法）
❌ 禁止类超过 500 行（超出应拆分为多个类）
❌ 禁止使用 `Thread.Sleep()` 进行等待（应使用 `Task.Delay()`）

---

## 八、示例完整代码

```csharp
// ============================================================
// 文件名: BatteryMonitorService.cs
// 用途: 电池监控服务，负责采集和分析电池充放电数据
// ============================================================

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace ChargeDischargeSystem.Core.Services
{
    /// <summary>
    /// 电池监控服务，处理电池运行数据的采集和分析
    /// </summary>
    public class BatteryMonitorService : IBatteryMonitorService
    {
        // 电池数据采集器实例
        private readonly IBatteryDataCollector _dataCollector;
        // 系统日志记录器实例
        private readonly ILogger _logger;
        // 设备配置信息对象
        private readonly DeviceConfiguration _configuration;

        /// <summary>
        /// 初始化电池监控服务实例
        /// </summary>
        /// <param name="dataCollector">电池数据采集器</param>
        /// <param name="logger">系统日志记录器</param>
        /// <param name="configuration">设备配置信息</param>
        public BatteryMonitorService(
            IBatteryDataCollector dataCollector,
            ILogger logger,
            DeviceConfiguration configuration)
        {
            // 数据采集器赋值
            _dataCollector = dataCollector ?? throw new ArgumentNullException(nameof(dataCollector));
            // 日志记录器赋值
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            // 设备配置赋值
            _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        }

        /// <summary>
        /// 采集电池运行数据
        /// </summary>
        /// <param name="batteryId">电池设备标识</param>
        /// <returns>电池运行数据对象</returns>
        public async Task<BatteryData> CollectDataAsync(string batteryId)
        {
            // 记录开始采集日志
            _logger.LogInformation("开始采集电池数据: {BatteryId}", batteryId);

            try
            {
                // 调用采集器获取原始数据
                RawData rawData = await _dataCollector.CollectAsync(batteryId);

                // 将原始数据转换为业务数据
                BatteryData data = ConvertToBatteryData(rawData);

                // 记录采集成功日志
                _logger.LogInformation("电池数据采集完成: {BatteryId}", batteryId);

                // 返回采集结果
                return data;
            }
            catch (Exception ex)
            {
                // 记录采集失败日志
                _logger.LogError(ex, "电池数据采集失败: {BatteryId}", batteryId);
                // 重新抛出异常
                throw;
            }
        }

        /// <summary>
        /// 分析电池运行状态
        /// </summary>
        /// <param name="batteryData">采集到的电池数据</param>
        /// <returns>电池状态分析结果</returns>
        public BatteryStatus AnalyzeBatteryStatus(BatteryData batteryData)
        {
            // 检查输入数据是否为空
            if (batteryData == null)
            {
                // 参数无效时抛出异常
                throw new ArgumentNullException(nameof(batteryData));
            }

            // 初始化新的状态分析结果对象
            BatteryStatus status = new BatteryStatus();

            // 检查电压是否超出正常范围
            if (batteryData.Voltage > _configuration.MaxVoltage)
            {
                // 电压过高时设置过压告警
                status.OverVoltageAlert = true;
                // 记录电压异常日志
                _logger.LogWarning("电池电压过高: {Voltage}V", batteryData.Voltage);
            }

            // 检查温度是否超出正常范围
            if (batteryData.Temperature > _configuration.MaxTemperature)
            {
                // 温度过高时设置过热告警
                status.OverTemperatureAlert = true;
                // 记录温度异常日志
                _logger.LogWarning("电池温度过高: {Temperature}°C", batteryData.Temperature);
            }

            // 计算电池剩余电量百分比
            status.SocPercentage = (batteryData.CurrentCharge / batteryData.FullCapacity) * 100;

            // 分析充放电状态
            status.IsCharging = batteryData.Current > 0;

            // 返回分析结果
            return status;
        }

        /// <summary>
        /// 将采集器原始数据转换为业务层数据模型
        /// </summary>
        /// <param name="rawData">采集器返回的原始数据</param>
        /// <returns>转换后的业务数据对象</returns>
        private BatteryData ConvertToBatteryData(RawData rawData)
        {
            // 创建新的业务数据对象
            BatteryData data = new BatteryData();

            // 复制原始数据中的电压值
            data.Voltage = rawData.VoltageValue;
            // 复制原始数据中的电流值
            data.Current = rawData.CurrentValue;
            // 复制原始数据中的温度值
            data.Temperature = rawData.TemperatureValue;
            // 复制当前电量值
            data.CurrentCharge = rawData.ChargeAmount;
            // 复制额定容量值
            data.FullCapacity = rawData.RatedCapacity;
            // 设置数据采集时间戳
            data.CollectionTime = DateTime.UtcNow;

            // 返回转换后的数据
            return data;
        }
    }
}
```

---

## 九、常用代码片段模板

### 9.1 ViewModel 模板（MVVM）

```csharp
/// <summary>
/// 设备管理视图模型，处理设备列表显示和操作逻辑
/// </summary>
public class DeviceManagementViewModel : ObservableObject
{
    // 设备数据仓储接口实例
    private readonly IDeviceRepository _deviceRepository;
    // 当前选中的设备对象
    private Device _selectedDevice;

    /// <summary>
    /// 获取或设置当前选中的设备
    /// </summary>
    public Device SelectedDevice
    {
        // 返回当前选中的设备
        get => _selectedDevice;
        // 设置选中的设备并触发属性变更通知
        set => SetProperty(ref _selectedDevice, value);
    }

    /// <summary>
    /// 构造函数注入设备仓储依赖
    /// </summary>
    /// <param name="deviceRepository">设备数据仓储</param>
    public DeviceManagementViewModel(IDeviceRepository deviceRepository)
    {
        // 仓储实例赋值
        _deviceRepository = deviceRepository;
    }
}
```

### 9.2 单元测试模板

```csharp
/// <summary>
/// 用户服务单元测试
/// </summary>
[TestClass]
public class UserServiceTests
{
    // 被测试的用户服务实例
    private UserService _userService;
    // 模拟的用户仓储对象
    private Mock<IUserRepository> _mockRepository;

    /// <summary>
    /// 每个测试方法执行前的初始化操作
    /// </summary>
    [TestInitialize]
    public void Initialize()
    {
        // 创建模拟仓储对象
        _mockRepository = new Mock<IUserRepository>();
        // 使用模拟仓储创建被测试的服务
        _userService = new UserService(_mockRepository.Object);
    }

    /// <summary>
    /// 验证根据ID查询用户返回正确结果
    /// </summary>
    [TestMethod]
    public async Task GetUserByIdAsync_ValidUserId_ReturnsUser()
    {
        // 准备测试数据
        string testUserId = "test-user-001";

        // 模拟仓储返回假数据
        _mockRepository
            .Setup(repo => repo.GetByIdAsync(testUserId))
            .ReturnsAsync(new UserAccount { UserId = testUserId, Username = "testuser" });

        // 执行被测试的方法
        UserAccount result = await _userService.GetUserByIdAsync(testUserId);

        // 验证结果不为空
        Assert.IsNotNull(result);
        // 验证用户ID匹配
        Assert.AreEqual(testUserId, result.UserId);
    }
}
```

---

## 十、规范执行检查清单

- [ ] 每行代码都有中文注释
- [ ] 命名符合 PascalCase / camelCase 规则
- [ ] 私有字段使用 `_camelCase` 前缀
- [ ] 接口以 `I` 开头
- [ ] 异步方法以 `Async` 结尾
- [ ] XML 文档注释覆盖所有公开成员
- [ ] 方法不超过 50 行
- [ ] 类不超过 500 行
- [ ] 大括号使用 Allman 风格
- [ ] 使用 4 空格缩进
- [ ] 异常处理完整（没有空的 catch 块）
- [ ] 依赖注入通过构造函数注入
- [ ] 没有魔术数字，使用常量代替
- [ ] 不滥用 `var` 关键字
- [ ] 文件名与主类名一致
- [ ] 一个文件只包含一个主要类
