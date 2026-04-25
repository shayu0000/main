using System;
using System.IO;
using Microsoft.Data.Sqlite;

// ============================================================
// 命名空间: ChargeDischargeSystem.Data.Database
// 功能描述: 数据库管理器——封装SQLite数据库的连接管理、备份和状态查询
// 说明: 采用线程安全的单例模式，提供统一的数据库连接访问入口。
//       支持数据库初始化、在线备份、文件大小查询以及连接生命周期管理。
//       所有数据库操作均通过 Microsoft.Data.Sqlite 包实现。
// ============================================================
namespace ChargeDischargeSystem.Data.Database
{
    /// <summary>
    /// SQLite数据库管理器（单例模式）
    /// 负责数据库连接的全生命周期管理，包括：
    ///   - 数据库连接创建与维护
    ///   - 数据库文件备份
    ///   - 数据库文件大小查询
    ///   - 连接关闭与资源释放
    /// 
    /// 使用说明：
    ///   首次使用前需调用 Initialize(string dbPath) 进行初始化。
    ///   通过 Instance 属性获取单例实例。
    ///   通过 Connection 属性获取当前数据库连接。
    ///   程序退出前应调用 Close() 释放连接资源。
    /// </summary>
    public sealed class DatabaseManager : IDisposable
    {
        #region 单例实现

        /// <summary>
        /// 线程安全的懒加载单例实例
        /// 使用 Lazy&lt;T&gt; 确保在多线程环境下只创建一个实例
        /// </summary>
        private static readonly Lazy<DatabaseManager> _instance =
            new Lazy<DatabaseManager>(() => new DatabaseManager(), System.Threading.LazyThreadSafetyMode.ExecutionAndPublication);

        /// <summary>
        /// 获取 DatabaseManager 的单例实例
        /// </summary>
        public static DatabaseManager Instance => _instance.Value;

        /// <summary>
        /// 私有构造函数，防止外部直接实例化
        /// </summary>
        private DatabaseManager()
        {
            // 实例创建时不执行数据库操作，等待显式调用 Initialize()
        }

        #endregion

        #region 私有字段

        /// <summary>数据库文件路径</summary>
        private string _dbPath;

        /// <summary>数据库连接字符串（基于 dbPath 构建）</summary>
        private string _connectionString;

        /// <summary>SQLite数据库连接实例</summary>
        private SqliteConnection _connection;

        /// <summary>用于线程同步的锁对象，保护连接状态</summary>
        private readonly object _lockObject = new object();

        /// <summary>标记实例是否已释放</summary>
        private bool _disposed;

        #endregion

        #region 公开属性

        /// <summary>
        /// 获取当前数据库连接实例
        /// 如果未初始化或连接已关闭，将抛出 InvalidOperationException。
        /// 
        /// 注意：SQLite连接本身不是线程安全的。在多线程环境下，应确保对连接的操作
        /// 在同一个线程上串行执行，或者每个线程创建独立的连接。对于高并发场景，
        /// 建议通过依赖注入或连接工厂模式为每个工作单元创建新的连接。
        /// </summary>
        /// <exception cref="InvalidOperationException">当数据库尚未初始化或连接已关闭时抛出</exception>
        public SqliteConnection Connection
        {
            get
            {
                lock (_lockObject)
                {
                    if (_connection == null)
                        throw new InvalidOperationException("数据库尚未初始化，请先调用 Initialize() 方法。");
                    if (_connection.State != System.Data.ConnectionState.Open)
                        throw new InvalidOperationException("数据库连接已关闭，请重新调用 Initialize() 方法。");
                    return _connection;
                }
            }
        }

        /// <summary>
        /// 获取数据库连接字符串
        /// 格式: "Data Source={dbPath}"
        /// </summary>
        /// <exception cref="InvalidOperationException">当数据库尚未初始化时抛出</exception>
        public string ConnectionString
        {
            get
            {
                lock (_lockObject)
                {
                    if (string.IsNullOrEmpty(_connectionString))
                        throw new InvalidOperationException("数据库尚未初始化，连接字符串不可用。");
                    return _connectionString;
                }
            }
        }

        #endregion

        #region 公开方法

        /// <summary>
        /// 初始化数据库管理器
        /// 创建SQLite数据库连接并打开。如果数据库文件不存在，SQLite将自动创建。
        /// 此方法应在程序启动时调用一次，通常在 DatabaseInitializer.InitializeDatabase() 之后调用。
        /// 
        /// 注意：此方法不会创建数据库表结构，仅负责建立连接。
        /// 如需创建表结构，请先调用 DatabaseInitializer.InitializeDatabase(dbPath)。
        /// </summary>
        /// <param name="dbPath">数据库文件完整路径，例如: "data/mw_scada.db"</param>
        /// <exception cref="ArgumentException">当 dbPath 为 null 或空字符串时抛出</exception>
        /// <exception cref="SqliteException">当数据库文件无法打开时抛出</exception>
        public void Initialize(string dbPath)
        {
            // ---- 参数校验 ----
            if (string.IsNullOrWhiteSpace(dbPath))
                throw new ArgumentException("数据库路径不能为 null 或空字符串。", nameof(dbPath));

            lock (_lockObject)
            {
                // 如果已有打开的连接，先关闭再重新打开
                CloseConnectionLocked();

                // 保存数据库路径
                _dbPath = dbPath;

                // 构建连接字符串
                _connectionString = $"Data Source={_dbPath}";

                // 确保数据库文件所在的目录存在
                string directory = System.IO.Path.GetDirectoryName(_dbPath);
                if (!string.IsNullOrEmpty(directory) && !System.IO.Directory.Exists(directory))
                {
                    System.IO.Directory.CreateDirectory(directory);
                }

                // 创建并打开数据库连接
                _connection = new SqliteConnection(_connectionString);
                _connection.Open();

                // 设置数据库PRAGMA（每次连接都需要设置，因为PRAGMA不是持久化的）
                ConfigureConnectionPragma(_connection);
            }
        }

        /// <summary>
        /// 备份当前数据库到指定路径
        /// 使用SQLite内置的 Backup API 进行在线热备份，备份期间主数据库仍可正常读写。
        /// 备份目标文件会被完全覆盖（如果已存在）。
        /// </summary>
        /// <param name="backupPath">备份文件完整路径，例如: "data/backup/mw_scada_20260424.db"</param>
        /// <exception cref="InvalidOperationException">当数据库尚未初始化时抛出</exception>
        /// <exception cref="ArgumentException">当 backupPath 为 null 或空字符串时抛出</exception>
        /// <exception cref="SqliteException">当备份操作失败时抛出</exception>
        public void BackupDatabase(string backupPath)
        {
            // ---- 参数校验 ----
            if (string.IsNullOrWhiteSpace(backupPath))
                throw new ArgumentException("备份路径不能为 null 或空字符串。", nameof(backupPath));

            lock (_lockObject)
            {
                // 确保数据库已初始化
                EnsureInitialized();

                // 确保备份目录存在
                string backupDir = System.IO.Path.GetDirectoryName(backupPath);
                if (!string.IsNullOrEmpty(backupDir) && !System.IO.Directory.Exists(backupDir))
                {
                    System.IO.Directory.CreateDirectory(backupDir);
                }

                // 如果备份文件已存在，先删除再创建（BackupDatabase 要求目标为空或不存在）
                if (System.IO.File.Exists(backupPath))
                {
                    System.IO.File.Delete(backupPath);
                }

                // 打开目标数据库连接（SQLite会自动创建空数据库文件）
                string destConnectionString = $"Data Source={backupPath}";
                using (SqliteConnection destConnection = new SqliteConnection(destConnectionString))
                {
                    destConnection.Open();

                    // 使用SQLite内置的 BackupDatabase API 执行在线备份
                    // 该操作会完整复制源数据库的所有页面到目标数据库
                    _connection.BackupDatabase(destConnection);
                }
            }
        }

        /// <summary>
        /// 获取数据库文件大小（单位：MB）
        /// 返回当前数据库文件在磁盘上的实际大小，保留两位小数。
        /// 如果数据库文件尚未创建（纯内存模式等场景），返回 0。
        /// </summary>
        /// <returns>数据库文件大小（MB），保留两位小数</returns>
        /// <exception cref="InvalidOperationException">当数据库尚未初始化时抛出</exception>
        public double GetDatabaseSizeMB()
        {
            lock (_lockObject)
            {
                EnsureInitialized();

                if (System.IO.File.Exists(_dbPath))
                {
                    System.IO.FileInfo fileInfo = new System.IO.FileInfo(_dbPath);
                    long bytes = fileInfo.Length;
                    // 字节转MB：除以 (1024 * 1024)，保留两位小数
                    double sizeMB = Math.Round(bytes / (1024.0 * 1024.0), 2);
                    return sizeMB;
                }

                return 0.0;
            }
        }

        /// <summary>
        /// 关闭数据库连接并释放相关资源
        /// 调用此方法后，Connection 属性将不可用，需要重新调用 Initialize() 才能再次使用。
        /// 此方法是幂等的——多次调用不会产生副作用。
        /// </summary>
        public void Close()
        {
            lock (_lockObject)
            {
                CloseConnectionLocked();
            }
        }

        /// <summary>
        /// 释放 DatabaseManager 实例占用的所有资源
        /// 实现 IDisposable 接口，支持 using 语句和手动释放。
        /// </summary>
        public void Dispose()
        {
            if (_disposed) return;

            lock (_lockObject)
            {
                CloseConnectionLocked();
                _disposed = true;
            }
        }

        #endregion

        #region 私有方法

        /// <summary>
        /// 为数据库连接设置PRAGMA运行参数
        /// 这些PRAGMA设置不会持久化到数据库文件中，每次新建连接都需要重新设置。
        /// 设置内容与 DatabaseInitializer 中的PRAGMA配置保持一致。
        /// </summary>
        /// <param name="connection">已打开的SQLite连接</param>
        private void ConfigureConnectionPragma(SqliteConnection connection)
        {
            // WAL模式：提升读写并发性能
            ExecutePragma(connection, "journal_mode = WAL");

            // NORMAL同步：平衡数据安全性与写入性能
            ExecutePragma(connection, "synchronous = NORMAL");

            // 64MB缓存：确保大事务的高效执行
            ExecutePragma(connection, "cache_size = -65536");

            // 4KB页面：与操作系统页面大小对齐
            ExecutePragma(connection, "page_size = 4096");

            // 临时表存储在内存中：减少磁盘I/O
            ExecutePragma(connection, "temp_store = MEMORY");

            // 启用外键约束
            ExecutePragma(connection, "foreign_keys = ON");
        }

        /// <summary>
        /// 执行单条PRAGMA语句
        /// </summary>
        /// <param name="connection">已打开的SQLite连接</param>
        /// <param name="pragma">PRAGMA语句内容（不含PRAGMA关键字）</param>
        private void ExecutePragma(SqliteConnection connection, string pragma)
        {
            using (SqliteCommand command = connection.CreateCommand())
            {
                command.CommandText = $"PRAGMA {pragma};";
                command.ExecuteNonQuery();
            }
        }

        /// <summary>
        /// 在持有锁的情况下关闭连接（内部方法，调用方需先获取 _lockObject 锁）
        /// </summary>
        private void CloseConnectionLocked()
        {
            if (_connection != null)
            {
                // 关闭连接（如果已打开）
                if (_connection.State == System.Data.ConnectionState.Open)
                {
                    _connection.Close();
                }

                // 释放连接资源
                _connection.Dispose();
                _connection = null;
            }

            _connectionString = null;
            _dbPath = null;
        }

        /// <summary>
        /// 确保数据库已初始化（在持有锁的情况下调用）
        /// </summary>
        /// <exception cref="InvalidOperationException">当数据库未初始化时抛出</exception>
        private void EnsureInitialized()
        {
            if (_connection == null || _connection.State != System.Data.ConnectionState.Open)
                throw new InvalidOperationException("数据库尚未初始化或连接已关闭，请先调用 Initialize() 方法。");
        }

        #endregion
    }
}
