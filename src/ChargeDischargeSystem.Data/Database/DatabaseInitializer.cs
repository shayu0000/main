using System;
using System.Collections.Generic;
using Microsoft.Data.Sqlite;
using System.Data;
using ChargeDischargeSystem.Common.Helpers;

// ============================================================
// 命名空间: ChargeDischargeSystem.Data.Database
// 功能描述: 数据库初始化——负责创建所有数据库表、索引和默认数据
// 说明: 提供静态方法完成SQLite数据库的首次初始化，包括PRAGMA配置、
//       建表、索引创建、默认角色和权限数据的插入，所有操作在事务内执行。
//       参照 MW级充放电系统开发文档 第5节 SQLite数据库设计。
// ============================================================
namespace ChargeDischargeSystem.Data.Database
{
    /// <summary>
    /// 数据库初始化器
    /// 负责SQLite数据库的创建、表结构初始化、索引建立以及默认角色/权限数据的插入。
    /// 所有DDL和DML操作均在一个事务内完成，确保数据库初始化的原子性。
    /// </summary>
    public static class DatabaseInitializer
    {
        #region 常量定义

        /// <summary>默认管理员角色ID</summary>
        private const string RoleAdmin = "ROLE_ADMIN";
        /// <summary>默认工程师角色ID</summary>
        private const string RoleEngineer = "ROLE_ENGINEER";
        /// <summary>默认操作员角色ID</summary>
        private const string RoleOperator = "ROLE_OPERATOR";
        /// <summary>默认查看者角色ID</summary>
        private const string RoleViewer = "ROLE_VIEWER";

        #endregion

        #region 公开方法

        /// <summary>
        /// 初始化数据库：配置PRAGMA、创建所有表、建立索引、插入默认角色和权限数据。
        /// 所有操作在同一事务中执行，如果中途失败则全部回滚，保证数据库状态一致。
        /// </summary>
        /// <param name="dbPath">数据库文件完整路径（含文件名），例如: "data/mw_scada.db"</param>
        /// <exception cref="ArgumentException">当 dbPath 为 null 或空字符串时抛出</exception>
        /// <exception cref="SqliteException">当数据库操作失败时抛出</exception>
        public static void InitializeDatabase(string dbPath)
        {
            // ---- 参数校验 ----
            if (string.IsNullOrWhiteSpace(dbPath))
                throw new ArgumentException("数据库路径不能为 null 或空字符串。", nameof(dbPath));

            // 构建连接字符串（使用 Data Source 指定文件路径）
            string connectionString = $"Data Source={dbPath}";

            // ---- 执行数据库初始化 ----
            using (SqliteConnection connection = new SqliteConnection(connectionString))
            {
                // 打开数据库连接（如果文件不存在，SQLite会自动创建）
                connection.Open();

                // ========== 第一步：配置全局PRAGMA参数（必须在事务外执行） ==========
                ConfigurePragma(connection);

                // ---- 开启事务：保证所有初始化操作原子性 ----
                using (SqliteTransaction transaction = connection.BeginTransaction())
                {
                    try
                    {
                        // ========== 第二步：创建所有数据表 ==========
                        CreateAllTables(connection);

                        // ========== 第三步：创建常用索引 ==========
                        CreateAllIndexes(connection);

                        // ========== 第四步：插入默认角色数据 ==========
                        InsertDefaultRoles(connection);

                        // ========== 第五步：插入默认权限数据 ==========
                        InsertDefaultPermissions(connection);

                        // ========== 第六步：建立角色-权限关联 ==========
                        InsertRolePermissions(connection);

                        // ========== 第七步：插入默认管理员用户 ==========
                        InsertDefaultAdminUser(connection);

                        // 提交事务（所有操作成功则持久化）
                        transaction.Commit();
                    }
                    catch
                    {
                        // 任何步骤失败都回滚事务，保证数据库不会处于半初始化状态
                        transaction.Rollback();
                        throw;
                    }
                }
            }
        }

        #endregion

        #region 私有方法

        /// <summary>
        /// 配置SQLite PRAGMA运行参数
        /// 设置WAL模式、同步级别、缓存大小、页面大小、临时表存储和外键约束。
        /// 这些参数在每次连接时都需要设置（非持久化PRAGMA），因此放在初始化最前面。
        /// </summary>
        /// <param name="connection">已打开的SQLite连接</param>
        private static void ConfigurePragma(SqliteConnection connection)
        {
            ExecutePragma(connection, "journal_mode = WAL");        // WAL模式：提高读写并发性能，写操作不阻塞读操作
            ExecutePragma(connection, "synchronous = NORMAL");      // NORMAL同步：平衡数据安全性与写入性能
            ExecutePragma(connection, "cache_size = -65536");       // 64MB缓存（负值表示KB单位），确保单次事务所需的页面能全部加载到内存
            ExecutePragma(connection, "page_size = 4096");          // 4KB页面大小，与操作系统页面大小一致，减少I/O碎片
            ExecutePragma(connection, "temp_store = MEMORY");       // 临时表和临时索引存储在内存中，减少磁盘I/O
            ExecutePragma(connection, "foreign_keys = ON");         // 启用外键约束，确保数据引用完整性
        }

        /// <summary>
        /// 执行单条PRAGMA语句（内部辅助方法）
        /// </summary>
        /// <param name="connection">已打开的SQLite连接</param>
        /// <param name="pragma">PRAGMA语句（不含PRAGMA关键字），例如: "journal_mode = WAL"</param>
        private static void ExecutePragma(SqliteConnection connection, string pragma)
        {
            using (SqliteCommand command = connection.CreateCommand())
            {
                command.CommandText = $"PRAGMA {pragma};";
                command.ExecuteNonQuery();
            }
        }

        /// <summary>
        /// 创建所有数据库表（共23个表）
        /// 严格按照开发文档第5节SQLite数据库设计中的表结构定义。
        /// 使用 IF NOT EXISTS 避免重复创建时出错。
        /// 按功能模块分组：用户与权限、设备管理、充放电会话、测量数据、校准数据、
        /// 协议管理、固件升级、故障录波、测试报告、系统日志。
        /// </summary>
        /// <param name="connection">已打开的SQLite连接</param>
        private static void CreateAllTables(SqliteConnection connection)
        {
            // ============================================================
            // 1. 用户与权限管理 —— user_account, user_role, permission,
            //    role_permission, user_session
            // ============================================================

            CreateTable(connection, "user_account", @"
                CREATE TABLE IF NOT EXISTS user_account (
                    user_id TEXT PRIMARY KEY,                  -- 用户ID（UUID）
                    username TEXT NOT NULL UNIQUE,             -- 用户名
                    password_hash TEXT NOT NULL,               -- 密码哈希（SHA-256 + Salt）
                    salt TEXT NOT NULL,                        -- 密码盐值
                    display_name TEXT,                         -- 显示名称
                    email TEXT,                                -- 邮箱
                    phone TEXT,                                -- 电话
                    role_id TEXT NOT NULL,                     -- 角色ID（外键关联 user_role）
                    status TEXT DEFAULT 'active',              -- 账户状态: active(活跃) / disabled(禁用) / locked(锁定)
                    last_login_time TEXT,                      -- 最后登录时间
                    login_fail_count INTEGER DEFAULT 0,       -- 登录失败次数
                    created_at TEXT DEFAULT (datetime('now')), -- 创建时间
                    updated_at TEXT DEFAULT (datetime('now'))  -- 更新时间
                )");

            CreateTable(connection, "user_role", @"
                CREATE TABLE IF NOT EXISTS user_role (
                    role_id TEXT PRIMARY KEY,                  -- 角色ID，如: ROLE_ADMIN / ROLE_ENGINEER / ROLE_OPERATOR / ROLE_VIEWER
                    role_name TEXT NOT NULL UNIQUE,            -- 角色名称: admin / engineer / operator / viewer
                    description TEXT,                          -- 角色描述
                    created_at TEXT DEFAULT (datetime('now'))  -- 创建时间
                )");

            CreateTable(connection, "permission", @"
                CREATE TABLE IF NOT EXISTS permission (
                    permission_id TEXT PRIMARY KEY,            -- 权限ID，如: PERM_DEVICE_READ
                    permission_name TEXT NOT NULL UNIQUE,      -- 权限名称，如: device:read / calibration:execute
                    resource TEXT NOT NULL,                    -- 资源类型: device / calibration / firmware / protocol / fault / report / user / data / system
                    action TEXT NOT NULL,                      -- 操作类型: read / write / execute / delete / register / configure / manage / upgrade / analyze / generate / export
                    description TEXT                           -- 权限描述
                )");

            CreateTable(connection, "role_permission", @"
                CREATE TABLE IF NOT EXISTS role_permission (
                    role_id TEXT NOT NULL,                     -- 角色ID（外键关联 user_role）
                    permission_id TEXT NOT NULL,               -- 权限ID（外键关联 permission）
                    PRIMARY KEY (role_id, permission_id),      -- 联合主键
                    FOREIGN KEY (role_id) REFERENCES user_role(role_id),
                    FOREIGN KEY (permission_id) REFERENCES permission(permission_id)
                )");

            CreateTable(connection, "user_session", @"
                CREATE TABLE IF NOT EXISTS user_session (
                    session_id TEXT PRIMARY KEY,               -- 会话ID（UUID）
                    user_id TEXT NOT NULL,                     -- 用户ID（外键关联 user_account）
                    login_time TEXT DEFAULT (datetime('now')), -- 登录时间
                    logout_time TEXT,                          -- 登出时间
                    ip_address TEXT,                           -- 登录IP地址
                    token_hash TEXT,                           -- 令牌哈希值
                    is_active INTEGER DEFAULT 1,              -- 是否活跃: 1=活跃, 0=已注销
                    FOREIGN KEY (user_id) REFERENCES user_account(user_id)
                )");

            // ============================================================
            // 2. 设备管理 —— device_info, device_config, device_status, device_alarm
            // ============================================================

            CreateTable(connection, "device_info", @"
                CREATE TABLE IF NOT EXISTS device_info (
                    device_id TEXT PRIMARY KEY,                -- 设备唯一标识
                    device_type TEXT NOT NULL,                 -- 设备类型: PCS(储能变流器) / BMS(电池管理系统) / INVERTER / METER / CHARGER
                    device_name TEXT,                          -- 设备名称
                    manufacturer TEXT,                         -- 制造商
                    model TEXT,                                -- 型号
                    serial_number TEXT,                        -- 序列号
                    rated_power_kw REAL,                       -- 额定功率(kW)
                    rated_voltage_v REAL,                      -- 额定电压(V)
                    rated_current_a REAL,                      -- 额定电流(A)
                    can_address INTEGER,                       -- CAN总线地址
                    protocol_name TEXT,                        -- 通信协议名称
                    firmware_version TEXT,                     -- 当前固件版本
                    status TEXT DEFAULT 'offline',             -- 设备状态: online / offline / fault / maintenance
                    registered_at TEXT DEFAULT (datetime('now')), -- 注册时间
                    last_online_time TEXT,                     -- 最近在线时间
                    notes TEXT                                 -- 备注
                )");

            CreateTable(connection, "device_config", @"
                CREATE TABLE IF NOT EXISTS device_config (
                    config_id TEXT PRIMARY KEY,                -- 配置ID（UUID）
                    device_id TEXT NOT NULL,                   -- 设备ID（外键关联 device_info）
                    config_name TEXT NOT NULL,                 -- 配置项名称
                    config_value TEXT,                         -- 配置值（JSON格式存储）
                    config_type TEXT,                          -- 配置类型: string / number / boolean / json
                    description TEXT,                          -- 配置描述
                    updated_by TEXT,                           -- 修改人ID（外键关联 user_account）
                    updated_at TEXT DEFAULT (datetime('now')), -- 更新时间
                    FOREIGN KEY (device_id) REFERENCES device_info(device_id),
                    FOREIGN KEY (updated_by) REFERENCES user_account(user_id)
                )");

            CreateTable(connection, "device_status", @"
                CREATE TABLE IF NOT EXISTS device_status (
                    record_id INTEGER PRIMARY KEY AUTOINCREMENT, -- 记录ID（自增主键）
                    device_id TEXT NOT NULL,                     -- 设备ID（外键关联 device_info）
                    parameter_name TEXT NOT NULL,                -- 参数名称: voltage / current / temperature / soc / power
                    parameter_value REAL,                        -- 参数值
                    unit TEXT,                                   -- 单位: V / A / ℃ / % / kW
                    quality TEXT DEFAULT 'GOOD',                 -- 数据质量: GOOD / UNCERTAIN / BAD
                    timestamp INTEGER NOT NULL,                  -- Unix毫秒时间戳（遵循时序优化建议）
                    FOREIGN KEY (device_id) REFERENCES device_info(device_id)
                )");

            CreateTable(connection, "device_alarm", @"
                CREATE TABLE IF NOT EXISTS device_alarm (
                    alarm_id TEXT PRIMARY KEY,                  -- 告警ID（UUID）
                    device_id TEXT NOT NULL,                     -- 设备ID（外键关联 device_info）
                    alarm_code TEXT NOT NULL,                   -- 告警编码
                    alarm_level TEXT NOT NULL,                  -- 告警级别: CRITICAL(严重) / MAJOR(主要) / MINOR(次要) / WARNING(警告)
                    alarm_message TEXT,                         -- 告警描述
                    parameter_name TEXT,                       -- 触发告警的参数名
                    threshold_value REAL,                      -- 阈值
                    actual_value REAL,                         -- 实际值
                    is_active INTEGER DEFAULT 1,               -- 是否仍活跃: 1=活跃, 0=已清除
                    raised_at INTEGER NOT NULL,                -- 告警触发时间（Unix毫秒时间戳）
                    acknowledged_at INTEGER,                   -- 确认时间
                    acknowledged_by TEXT,                      -- 确认人ID（外键关联 user_account）
                    cleared_at INTEGER,                        -- 清除时间
                    FOREIGN KEY (device_id) REFERENCES device_info(device_id),
                    FOREIGN KEY (acknowledged_by) REFERENCES user_account(user_id)
                )");

            // ============================================================
            // 3. 充放电会话 —— charge_session
            // ============================================================

            CreateTable(connection, "charge_session", @"
                CREATE TABLE IF NOT EXISTS charge_session (
                    session_id TEXT PRIMARY KEY,                -- 会话ID（UUID）
                    device_id TEXT NOT NULL,                    -- 设备ID（外键关联 device_info）
                    battery_protocol TEXT,                     -- 电池协议名称
                    session_type TEXT NOT NULL,                -- 会话类型: charge(充电) / discharge(放电) / cycle(循环) / formation(化成)
                    status TEXT DEFAULT 'running',             -- 会话状态: running / paused / completed / aborted / fault
                    start_time INTEGER NOT NULL,               -- 开始时间（Unix毫秒时间戳）
                    end_time INTEGER,                          -- 结束时间
                    target_voltage_v REAL,                     -- 目标电压(V)
                    target_current_a REAL,                     -- 目标电流(A)
                    target_power_kw REAL,                      -- 目标功率(kW)
                    target_soc_percent REAL,                   -- 目标SOC百分比
                    target_duration_s INTEGER,                 -- 目标时长(秒)
                    cutoff_voltage_v REAL,                     -- 截止电压(V)
                    total_energy_kwh REAL DEFAULT 0,          -- 累计能量(kWh)
                    total_charge_ah REAL DEFAULT 0,           -- 累计安时(Ah)
                    max_voltage_v REAL,                        -- 最高电压(V)
                    min_voltage_v REAL,                        -- 最低电压(V)
                    max_current_a REAL,                        -- 最大电流(A)
                    max_temperature_c REAL,                    -- 最高温度(℃)
                    created_by TEXT,                           -- 创建人ID（外键关联 user_account）
                    notes TEXT,                                -- 备注
                    FOREIGN KEY (device_id) REFERENCES device_info(device_id),
                    FOREIGN KEY (created_by) REFERENCES user_account(user_id)
                )");

            // ============================================================
            // 4. 测量数据 —— measurement_data
            //    核心时序数据表，存储所有设备参数的时间序列数据。
            //    参考SQLite论坛最佳实践：使用INTEGER毫秒时间戳减少存储空间。
            // ============================================================

            CreateTable(connection, "measurement_data", @"
                CREATE TABLE IF NOT EXISTS measurement_data (
                    record_id INTEGER PRIMARY KEY AUTOINCREMENT, -- 记录ID（自增主键）
                    timestamp INTEGER NOT NULL,                  -- Unix毫秒时间戳
                    device_id TEXT NOT NULL,                     -- 设备ID（外键关联 device_info）
                    session_id TEXT,                             -- 会话ID（外键关联 charge_session，可为空）
                    parameter_name TEXT NOT NULL,                -- 参数名称: voltage / current / power / temperature / soc
                    value REAL NOT NULL,                         -- 测量值
                    unit TEXT,                                   -- 单位
                    quality TEXT DEFAULT 'GOOD',                -- 数据质量: GOOD / UNCERTAIN / BAD
                    FOREIGN KEY (device_id) REFERENCES device_info(device_id),
                    FOREIGN KEY (session_id) REFERENCES charge_session(session_id)
                )");

            // ============================================================
            // 5. 校准数据 —— calibration_record, calibration_point
            // ============================================================

            CreateTable(connection, "calibration_record", @"
                CREATE TABLE IF NOT EXISTS calibration_record (
                    calibration_id TEXT PRIMARY KEY,            -- 校准记录ID（UUID）
                    device_id TEXT NOT NULL,                    -- 设备ID（外键关联 device_info）
                    calibration_type TEXT NOT NULL,            -- 校准类型: ZERO(零点) / SPAN(跨度) / LINEAR(线性) / VI_CURRENT(VI电流) / VI_VOLTAGE(VI电压)
                    calibration_status TEXT NOT NULL,          -- 校准状态: in_progress / completed / failed
                    started_at INTEGER NOT NULL,               -- 开始时间（Unix毫秒时间戳）
                    completed_at INTEGER,                      -- 完成时间
                    performed_by TEXT,                         -- 执行人ID（外键关联 user_account）
                    approved_by TEXT,                          -- 批准人ID（外键关联 user_account）
                    is_valid INTEGER DEFAULT 1,                -- 是否有效: 1=有效, 0=无效
                    valid_until INTEGER,                       -- 有效期至（Unix毫秒时间戳）
                    notes TEXT,                                -- 备注
                    FOREIGN KEY (device_id) REFERENCES device_info(device_id),
                    FOREIGN KEY (performed_by) REFERENCES user_account(user_id),
                    FOREIGN KEY (approved_by) REFERENCES user_account(user_id)
                )");

            CreateTable(connection, "calibration_point", @"
                CREATE TABLE IF NOT EXISTS calibration_point (
                    point_id INTEGER PRIMARY KEY AUTOINCREMENT, -- 校准点ID（自增主键）
                    calibration_id TEXT NOT NULL,              -- 校准记录ID（外键关联 calibration_record）
                    parameter_name TEXT NOT NULL,              -- 参数名称: voltage / current / power
                    point_index INTEGER NOT NULL,              -- 校准点序号
                    reference_value REAL NOT NULL,             -- 标准参考值（从高精度仪表读取）
                    measured_value REAL NOT NULL,              -- 设备测量值（校准前）
                    corrected_value REAL,                      -- 校准后值
                    deviation_percent REAL,                    -- 偏差百分比
                    is_pass INTEGER,                           -- 该点是否合格: 1=合格, 0=不合格
                    FOREIGN KEY (calibration_id) REFERENCES calibration_record(calibration_id)
                )");

            // ============================================================
            // 6. 协议管理 —— protocol_config, protocol_command, battery_protocol_config
            // ============================================================

            CreateTable(connection, "protocol_config", @"
                CREATE TABLE IF NOT EXISTS protocol_config (
                    protocol_id TEXT PRIMARY KEY,              -- 协议ID（UUID）
                    protocol_name TEXT NOT NULL UNIQUE,        -- 协议名称（唯一）
                    protocol_type TEXT NOT NULL,               -- 协议类型: device(设备协议) / battery(电池协议)
                    protocol_version TEXT,                     -- 协议版本
                    config_content TEXT NOT NULL,              -- 协议配置内容（JSON格式）
                    is_active INTEGER DEFAULT 1,              -- 是否启用: 1=启用, 0=禁用
                    loaded_at TEXT DEFAULT (datetime('now')),  -- 加载时间
                    updated_at TEXT                            -- 更新时间
                )");

            CreateTable(connection, "protocol_command", @"
                CREATE TABLE IF NOT EXISTS protocol_command (
                    command_id TEXT PRIMARY KEY,               -- 命令ID（UUID）
                    protocol_id TEXT NOT NULL,                 -- 协议ID（外键关联 protocol_config）
                    command_name TEXT NOT NULL,                -- 命令名称
                    command_code INTEGER NOT NULL,             -- CAN命令码
                    request_params TEXT,                       -- 请求参数定义（JSON格式）
                    response_params TEXT,                      -- 响应参数定义（JSON格式）
                    timeout_ms INTEGER DEFAULT 1000,          -- 超时时间(毫秒)
                    retry_count INTEGER DEFAULT 3,            -- 重试次数
                    description TEXT,                          -- 命令描述
                    FOREIGN KEY (protocol_id) REFERENCES protocol_config(protocol_id)
                )");

            CreateTable(connection, "battery_protocol_config", @"
                CREATE TABLE IF NOT EXISTS battery_protocol_config (
                    config_id TEXT PRIMARY KEY,                -- 配置ID（UUID）
                    protocol_name TEXT NOT NULL,               -- 协议名称
                    bms_vendor TEXT NOT NULL,                  -- BMS供应商名称
                    protocol_version TEXT,                     -- 协议版本
                    pgn_definition TEXT NOT NULL,              -- PGN定义（JSON格式）
                    fault_code_map TEXT,                       -- 故障码映射（JSON格式）
                    is_active INTEGER DEFAULT 1,              -- 是否启用: 1=启用, 0=禁用
                    created_at TEXT DEFAULT (datetime('now'))  -- 创建时间
                )");

            // ============================================================
            // 7. 固件升级 —— firmware_version, upgrade_task
            // ============================================================

            CreateTable(connection, "firmware_version", @"
                CREATE TABLE IF NOT EXISTS firmware_version (
                    version_id TEXT PRIMARY KEY,               -- 版本ID（UUID）
                    device_id TEXT NOT NULL,                   -- 设备ID（外键关联 device_info）
                    firmware_version TEXT NOT NULL,            -- 固件版本号
                    firmware_file_path TEXT,                   -- 固件文件存储路径
                    file_size_bytes INTEGER,                   -- 文件大小(字节)
                    checksum TEXT,                              -- SHA-256校验和
                    release_notes TEXT,                        -- 发布说明
                    released_at TEXT DEFAULT (datetime('now')), -- 发布日期
                    FOREIGN KEY (device_id) REFERENCES device_info(device_id)
                )");

            CreateTable(connection, "upgrade_task", @"
                CREATE TABLE IF NOT EXISTS upgrade_task (
                    task_id TEXT PRIMARY KEY,                  -- 任务ID（UUID）
                    device_id TEXT NOT NULL,                   -- 设备ID（外键关联 device_info）
                    from_version TEXT NOT NULL,                -- 当前固件版本
                    to_version TEXT NOT NULL,                  -- 目标固件版本
                    firmware_file TEXT NOT NULL,               -- 固件文件
                    status TEXT NOT NULL,                      -- 任务状态: pending / in_progress / completed / failed / cancelled
                    progress_percent REAL DEFAULT 0,          -- 升级进度百分比
                    started_at INTEGER,                        -- 开始时间（Unix毫秒时间戳）
                    completed_at INTEGER,                      -- 完成时间
                    initiated_by TEXT,                         -- 发起人ID（外键关联 user_account）
                    error_message TEXT,                        -- 失败原因
                    FOREIGN KEY (device_id) REFERENCES device_info(device_id),
                    FOREIGN KEY (initiated_by) REFERENCES user_account(user_id)
                )");

            // ============================================================
            // 8. 故障录波 —— fault_event, fault_waveform
            // ============================================================

            CreateTable(connection, "fault_event", @"
                CREATE TABLE IF NOT EXISTS fault_event (
                    event_id TEXT PRIMARY KEY,                 -- 事件ID（UUID）
                    device_id TEXT NOT NULL,                   -- 设备ID（外键关联 device_info）
                    session_id TEXT,                           -- 会话ID（外键关联 charge_session）
                    fault_code TEXT NOT NULL,                  -- 故障码
                    fault_level TEXT NOT NULL,                -- 故障级别: CRITICAL(严重) / MAJOR(主要) / MINOR(次要)
                    fault_description TEXT,                   -- 故障描述
                    triggered_at INTEGER NOT NULL,            -- 触发时间（Unix毫秒时间戳）
                    trigger_channel TEXT,                     -- 触发通道名称
                    trigger_value REAL,                       -- 触发值
                    pre_fault_samples INTEGER,                -- 故障前采样点数
                    post_fault_samples INTEGER,               -- 故障后采样点数
                    sample_rate_hz REAL,                      -- 采样率(Hz)
                    waveform_data_path TEXT,                   -- 波形数据文件路径（大Blob存储于文件中）
                    is_exported INTEGER DEFAULT 0,            -- 是否已导出: 1=已导出, 0=未导出
                    analyzed_by TEXT,                         -- 分析人ID
                    analysis_notes TEXT,                      -- 分析备注
                    FOREIGN KEY (device_id) REFERENCES device_info(device_id),
                    FOREIGN KEY (session_id) REFERENCES charge_session(session_id)
                )");

            CreateTable(connection, "fault_waveform", @"
                CREATE TABLE IF NOT EXISTS fault_waveform (
                    waveform_id TEXT PRIMARY KEY,              -- 波形ID（UUID）
                    event_id TEXT NOT NULL,                    -- 事件ID（外键关联 fault_event）
                    channel_index INTEGER NOT NULL,            -- 通道索引
                    channel_name TEXT NOT NULL,                -- 通道名称
                    data_blob BLOB,                            -- 波形数据（压缩存储）
                    data_size INTEGER,                         -- 数据点数量
                    unit TEXT,                                 -- 单位
                    FOREIGN KEY (event_id) REFERENCES fault_event(event_id)
                )");

            // ============================================================
            // 9. 测试报告 —— test_report, report_template
            // ============================================================

            CreateTable(connection, "test_report", @"
                CREATE TABLE IF NOT EXISTS test_report (
                    report_id TEXT PRIMARY KEY,                -- 报告ID（UUID）
                    report_type TEXT NOT NULL,                -- 报告类型: charge_test / discharge_test / cycle_test / calibration
                    title TEXT NOT NULL,                      -- 报告标题
                    device_id TEXT,                           -- 设备ID（外键关联 device_info）
                    session_id TEXT,                          -- 会话ID（外键关联 charge_session）
                    generated_by TEXT,                        -- 生成人ID（外键关联 user_account）
                    generated_at TEXT DEFAULT (datetime('now')), -- 生成时间
                    report_format TEXT DEFAULT 'pdf',         -- 报告格式: pdf / html / csv
                    report_file_path TEXT,                    -- 报告文件路径
                    report_data TEXT,                         -- 报告数据（JSON格式）
                    status TEXT DEFAULT 'draft',              -- 报告状态: draft / published / archived
                    FOREIGN KEY (device_id) REFERENCES device_info(device_id),
                    FOREIGN KEY (session_id) REFERENCES charge_session(session_id),
                    FOREIGN KEY (generated_by) REFERENCES user_account(user_id)
                )");

            CreateTable(connection, "report_template", @"
                CREATE TABLE IF NOT EXISTS report_template (
                    template_id TEXT PRIMARY KEY,             -- 模板ID（UUID）
                    template_name TEXT NOT NULL,              -- 模板名称
                    report_type TEXT NOT NULL,                -- 报告类型: charge_test / discharge_test / cycle_test / calibration
                    template_content TEXT NOT NULL,           -- Jinja2模板内容
                    is_default INTEGER DEFAULT 0,            -- 是否默认模板: 1=默认, 0=非默认
                    created_by TEXT,                         -- 创建人ID（外键关联 user_account）
                    updated_at TEXT DEFAULT (datetime('now')), -- 更新时间
                    FOREIGN KEY (created_by) REFERENCES user_account(user_id)
                )");

            // ============================================================
            // 10. 系统日志 —— system_log
            // ============================================================

            CreateTable(connection, "system_log", @"
                CREATE TABLE IF NOT EXISTS system_log (
                    log_id INTEGER PRIMARY KEY AUTOINCREMENT, -- 日志ID（自增主键）
                    timestamp INTEGER NOT NULL,               -- 时间戳（Unix毫秒时间戳）
                    log_level TEXT NOT NULL,                  -- 日志级别: DEBUG / INFO / WARNING / ERROR / CRITICAL
                    module_name TEXT NOT NULL,                -- 来源模块名称，如: EvseMgr / DevMonitor / DataLogger
                    message TEXT NOT NULL,                    -- 日志消息
                    details TEXT,                             -- 详细信息（JSON格式）
                    user_id TEXT,                             -- 用户ID
                    device_id TEXT                            -- 设备ID
                )");
        }

        /// <summary>
        /// 创建所有常用索引（共10个索引）
        /// 根据核心查询模式设计复合索引：按设备ID+时间戳的查询是最常见的场景。
        /// 这些索引能显著加速时序数据查询、设备告警查询、系统日志检索等操作。
        /// </summary>
        /// <param name="connection">已打开的SQLite连接</param>
        private static void CreateAllIndexes(SqliteConnection connection)
        {
            // 设备状态索引：按设备和时间查询设备实时状态
            CreateIndex(connection, "idx_device_status_device_time",
                "CREATE INDEX IF NOT EXISTS idx_device_status_device_time ON device_status(device_id, timestamp)");

            // 设备告警索引：按设备和触发时间查询告警历史
            CreateIndex(connection, "idx_device_alarm_device_time",
                "CREATE INDEX IF NOT EXISTS idx_device_alarm_device_time ON device_alarm(device_id, raised_at)");

            // 充放电会话索引：按起止时间查询历史会话
            CreateIndex(connection, "idx_charge_session_time",
                "CREATE INDEX IF NOT EXISTS idx_charge_session_time ON charge_session(start_time, end_time)");

            // 测量数据索引 - 按设备和时间查询（最常用查询模式）
            CreateIndex(connection, "idx_meas_device_time",
                "CREATE INDEX IF NOT EXISTS idx_meas_device_time ON measurement_data(device_id, timestamp)");

            // 测量数据索引 - 按会话和时间查询（会话内数据查询）
            CreateIndex(connection, "idx_meas_session_time",
                "CREATE INDEX IF NOT EXISTS idx_meas_session_time ON measurement_data(session_id, timestamp)");

            // 测量数据索引 - 按参数名和时间查询（跨设备参数趋势分析）
            CreateIndex(connection, "idx_meas_param_time",
                "CREATE INDEX IF NOT EXISTS idx_meas_param_time ON measurement_data(parameter_name, timestamp)");

            // 校准记录索引：按设备和开始时间查询校准历史
            CreateIndex(connection, "idx_calibration_device",
                "CREATE INDEX IF NOT EXISTS idx_calibration_device ON calibration_record(device_id, started_at)");

            // 故障事件索引：按设备和触发时间查询故障历史
            CreateIndex(connection, "idx_fault_event_device",
                "CREATE INDEX IF NOT EXISTS idx_fault_event_device ON fault_event(device_id, triggered_at)");

            // 系统日志索引 - 按日志级别和时间查询（错误日志快速定位）
            CreateIndex(connection, "idx_system_log_level_time",
                "CREATE INDEX IF NOT EXISTS idx_system_log_level_time ON system_log(log_level, timestamp)");

            // 系统日志索引 - 按模块和时间查询（模块日志排查）
            CreateIndex(connection, "idx_system_log_module_time",
                "CREATE INDEX IF NOT EXISTS idx_system_log_module_time ON system_log(module_name, timestamp)");
        }

        /// <summary>
        /// 插入默认角色数据（4个默认角色）
        /// 角色定义参照开发文档第6.2节角色与权限定义：
        ///   - admin: 系统管理员，拥有所有权限
        ///   - engineer: 工程师，可执行校准和升级操作
        ///   - operator: 操作员，日常监控和操作
        ///   - viewer: 查看者，只读权限
        /// </summary>
        /// <param name="connection">已打开的SQLite连接</param>
        private static void InsertDefaultRoles(SqliteConnection connection)
        {
            // 使用 INSERT OR IGNORE 避免重复插入时出错
            const string insertRoleSql = @"
                INSERT OR IGNORE INTO user_role (role_id, role_name, description)
                VALUES (@role_id, @role_name, @description);";

            // 定义4个默认角色
            var roles = new (string Id, string Name, string Description)[]
            {
                (RoleAdmin, "admin", "系统管理员——拥有所有操作权限"),
                (RoleEngineer, "engineer", "工程师——可执行设备配置、校准、升级和故障分析操作"),
                (RoleOperator, "operator", "操作员——日常监控、报告生成和数据导出"),
                (RoleViewer, "viewer", "查看者——仅具有只读查看权限"),
            };

            foreach (var (id, name, desc) in roles)
            {
                using (SqliteCommand command = connection.CreateCommand())
                {
                    command.CommandText = insertRoleSql;
                    command.Parameters.AddWithValue("@role_id", id);
                    command.Parameters.AddWithValue("@role_name", name);
                    command.Parameters.AddWithValue("@description", desc);
                    command.ExecuteNonQuery();
                }
            }
        }

        /// <summary>
        /// 插入默认权限数据（共15个权限）
        /// 所有权限按 资源:操作 的格式命名，涵盖系统所有功能模块。
        /// 使用 INSERT OR IGNORE 避免重复插入。
        /// </summary>
        /// <param name="connection">已打开的SQLite连接</param>
        private static void InsertDefaultPermissions(SqliteConnection connection)
        {
            const string insertPermSql = @"
                INSERT OR IGNORE INTO permission (permission_id, permission_name, resource, action, description)
                VALUES (@permission_id, @permission_name, @resource, @action, @description);";

            // 定义15个默认权限（覆盖所有功能模块）
            var permissions = new (string Id, string Name, string Resource, string Action, string Description)[]
            {
                // ---- 用户管理权限 ----
                ("PERM_USER_CREATE",          "user:create",          "user",         "create",    "创建新用户"),
                ("PERM_USER_UPDATE",          "user:update",          "user",         "update",    "修改用户信息"),
                ("PERM_USER_DELETE",          "user:delete",          "user",         "delete",    "删除用户"),

                // ---- 设备管理权限 ----
                ("PERM_DEVICE_REGISTER",      "device:register",      "device",       "register",  "注册新设备"),
                ("PERM_DEVICE_CONFIGURE",     "device:configure",     "device",       "configure", "配置设备参数"),
                ("PERM_DEVICE_READ",          "device:read",          "device",       "read",      "读取设备信息和状态"),

                // ---- 校准权限 ----
                ("PERM_CALIBRATION_EXECUTE",  "calibration:execute",  "calibration",  "execute",   "执行校准操作"),
                ("PERM_CALIBRATION_APPROVE",  "calibration:approve",  "calibration",  "approve",   "批准校准结果"),

                // ---- 协议管理权限 ----
                ("PERM_PROTOCOL_MANAGE",      "protocol:manage",      "protocol",     "manage",    "管理通信协议配置"),

                // ---- 固件升级权限 ----
                ("PERM_FIRMWARE_UPGRADE",     "firmware:upgrade",     "firmware",     "upgrade",   "执行固件升级操作"),

                // ---- 故障分析权限 ----
                ("PERM_FAULT_ANALYZE",        "fault:analyze",        "fault",        "analyze",   "分析故障事件和波形数据"),

                // ---- 报告权限 ----
                ("PERM_REPORT_GENERATE",      "report:generate",      "report",       "generate",  "生成测试报告"),

                // ---- 系统配置权限 ----
                ("PERM_SYSTEM_CONFIGURE",     "system:configure",     "system",       "configure", "配置系统参数"),

                // ---- 数据操作权限 ----
                ("PERM_DATA_EXPORT",          "data:export",          "data",         "export",    "导出测量数据"),
                ("PERM_DATA_DELETE",          "data:delete",          "data",         "delete",    "删除历史数据"),
            };

            foreach (var (id, name, resource, action, desc) in permissions)
            {
                using (SqliteCommand command = connection.CreateCommand())
                {
                    command.CommandText = insertPermSql;
                    command.Parameters.AddWithValue("@permission_id", id);
                    command.Parameters.AddWithValue("@permission_name", name);
                    command.Parameters.AddWithValue("@resource", resource);
                    command.Parameters.AddWithValue("@action", action);
                    command.Parameters.AddWithValue("@description", desc);
                    command.ExecuteNonQuery();
                }
            }
        }

        /// <summary>
        /// 建立角色-权限关联关系
        /// 根据开发文档第6.2节定义的角色权限矩阵，为每个角色分配对应的权限。
        /// 权限分配原则：
        ///   - admin: 全部15个权限
        ///   - engineer: 8个权限（设备注册/配置、校准执行、协议管理、固件升级、故障分析、报告生成、数据导出）
        ///   - operator: 3个权限（设备读取、报告生成、数据导出）
        ///   - viewer: 1个权限（设备读取）
        /// </summary>
        /// <param name="connection">已打开的SQLite连接</param>
        private static void InsertRolePermissions(SqliteConnection connection)
        {
            const string insertRpSql = @"
                INSERT OR IGNORE INTO role_permission (role_id, permission_id)
                VALUES (@role_id, @permission_id);";

            // 定义每个角色对应的权限ID列表
            var rolePermissionMap = new Dictionary<string, string[]>
            {
                // admin（系统管理员）：拥有所有15个权限
                [RoleAdmin] = new[]
                {
                    "PERM_USER_CREATE", "PERM_USER_UPDATE", "PERM_USER_DELETE",
                    "PERM_DEVICE_REGISTER", "PERM_DEVICE_CONFIGURE", "PERM_DEVICE_READ",
                    "PERM_CALIBRATION_EXECUTE", "PERM_CALIBRATION_APPROVE",
                    "PERM_PROTOCOL_MANAGE",
                    "PERM_FIRMWARE_UPGRADE",
                    "PERM_FAULT_ANALYZE",
                    "PERM_REPORT_GENERATE",
                    "PERM_SYSTEM_CONFIGURE",
                    "PERM_DATA_EXPORT", "PERM_DATA_DELETE"
                },

                // engineer（工程师）：可执行校准和升级操作
                [RoleEngineer] = new[]
                {
                    "PERM_DEVICE_REGISTER", "PERM_DEVICE_CONFIGURE",
                    "PERM_CALIBRATION_EXECUTE",
                    "PERM_PROTOCOL_MANAGE",
                    "PERM_FIRMWARE_UPGRADE",
                    "PERM_FAULT_ANALYZE",
                    "PERM_REPORT_GENERATE",
                    "PERM_DATA_EXPORT"
                },

                // operator（操作员）：日常监控和操作
                [RoleOperator] = new[]
                {
                    "PERM_DEVICE_READ",
                    "PERM_REPORT_GENERATE",
                    "PERM_DATA_EXPORT"
                },

                // viewer（查看者）：仅只读权限
                [RoleViewer] = new[]
                {
                    "PERM_DEVICE_READ"
                }
            };

            foreach (var (roleId, permIds) in rolePermissionMap)
            {
                foreach (string permId in permIds)
                {
                    using (SqliteCommand command = connection.CreateCommand())
                    {
                        command.CommandText = insertRpSql;
                        command.Parameters.AddWithValue("@role_id", roleId);
                        command.Parameters.AddWithValue("@permission_id", permId);
                        command.ExecuteNonQuery();
                    }
                }
            }
        }

        /// <summary>
        /// 插入默认管理员用户
        /// 系统首次初始化时创建默认管理员账号，用于初始登录和系统配置。
        /// 密码：Admin@123456，使用 SHA-256 + Salt 加密。
        /// </summary>
        /// <param name="connection">已打开的SQLite连接</param>
        private static void InsertDefaultAdminUser(SqliteConnection connection)
        {
            // 生成默认管理员的密码哈希和盐值
            string password = "Admin@123456";
            var (passwordHash, salt) = CryptoHelper.HashPassword(password);

            // 使用 INSERT OR IGNORE 避免重复插入时出错
            const string insertUserSql = @"
                INSERT OR IGNORE INTO user_account (
                    user_id, username, password_hash, salt, display_name, 
                    email, phone, role_id, status, login_fail_count, 
                    created_at, updated_at
                ) VALUES (
                    @user_id, @username, @password_hash, @salt, @display_name, 
                    @email, @phone, @role_id, @status, @login_fail_count, 
                    datetime('now'), datetime('now')
                );
            ";

            using (SqliteCommand command = connection.CreateCommand())
            {
                command.CommandText = insertUserSql;
                command.Parameters.AddWithValue("@user_id", "admin-001");
                command.Parameters.AddWithValue("@username", "admin");
                command.Parameters.AddWithValue("@password_hash", passwordHash);
                command.Parameters.AddWithValue("@salt", salt);
                command.Parameters.AddWithValue("@display_name", "管理员");
                command.Parameters.AddWithValue("@email", (object)DBNull.Value);
                command.Parameters.AddWithValue("@phone", (object)DBNull.Value);
                command.Parameters.AddWithValue("@role_id", RoleAdmin);
                command.Parameters.AddWithValue("@status", "active");
                command.Parameters.AddWithValue("@login_fail_count", 0);
                command.ExecuteNonQuery();
            }
        }



        #endregion

        #region 内部辅助方法

        /// <summary>
        /// 执行 CREATE TABLE 语句（内部辅助方法）
        /// 在事务中创建单张表，失败时由外层事务回滚。
        /// </summary>
        /// <param name="connection">已打开的SQLite连接</param>
        /// <param name="tableName">表名（用于日志标识）</param>
        /// <param name="createTableSql">完整的 CREATE TABLE IF NOT EXISTS 语句</param>
        private static void CreateTable(SqliteConnection connection, string tableName, string createTableSql)
        {
            _ = tableName; // 保留参数用于调试/日志标识（当前未使用）
            using (SqliteCommand command = connection.CreateCommand())
            {
                command.CommandText = createTableSql;
                command.ExecuteNonQuery();
            }
        }

        /// <summary>
        /// 执行 CREATE INDEX 语句（内部辅助方法）
        /// </summary>
        /// <param name="connection">已打开的SQLite连接</param>
        /// <param name="indexName">索引名称（用于日志标识）</param>
        /// <param name="createIndexSql">完整的 CREATE INDEX IF NOT EXISTS 语句</param>
        private static void CreateIndex(SqliteConnection connection, string indexName, string createIndexSql)
        {
            _ = indexName; // 保留参数用于调试/日志标识（当前未使用）
            using (SqliteCommand command = connection.CreateCommand())
            {
                command.CommandText = createIndexSql;
                command.ExecuteNonQuery();
            }
        }

        #endregion
    }
}
