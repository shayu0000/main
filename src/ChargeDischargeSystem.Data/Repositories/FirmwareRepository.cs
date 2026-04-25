using System;
using System.Collections.Generic;
using Dapper;
using ChargeDischargeSystem.Core.Models;
using ChargeDischargeSystem.Data.Database;

// ============================================================
// 命名空间: ChargeDischargeSystem.Data.Repositories
// 功能描述: 固件升级数据仓库——封装固件版本管理和升级任务的数据库操作
// 说明: 使用 Dapper 访问 SQLite 数据库。提供固件版本的注册查询、
//       升级任务的创建、进度更新和历史查询等功能。
// ============================================================
namespace ChargeDischargeSystem.Data.Repositories
{
    /// <summary>
    /// 固件升级数据仓库
    /// 提供对 firmware_version（固件版本）和 upgrade_task（升级任务）
    /// 两张表的完整数据访问。支持固件版本的生命周期管理和
    /// 设备固件升级任务的全程跟踪。
    /// </summary>
    public class FirmwareRepository
    {
        /// <summary>
        /// 获取指定设备的所有固件版本记录
        /// 从 firmware_version 表查询某设备的所有已注册固件版本，
        /// 按发布时间倒序排列。
        /// </summary>
        /// <param name="deviceId">设备唯一标识</param>
        /// <returns>固件版本列表</returns>
        public List<FirmwareVersion> GetFirmwareVersions(string deviceId)
        {
            try
            {
                const string sql = @"
                    SELECT version_id, device_id, firmware_version,
                           firmware_file_path, file_size_bytes, checksum,
                           release_notes, released_at
                    FROM firmware_version
                    WHERE device_id = @device_id
                    ORDER BY released_at DESC;";

                var connection = DatabaseManager.Instance.Connection;
                var versions = connection.Query<FirmwareVersion>(sql, new { device_id = deviceId }).AsList();
                return versions;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[FirmwareRepository.GetFirmwareVersions] 错误: {ex.Message}");
                return new List<FirmwareVersion>();
            }
        }

        /// <summary>
        /// 插入新的固件版本记录
        /// 向 firmware_version 表注册一个新固件版本。使用事务保证原子性。
        /// </summary>
        /// <param name="version">固件版本实体</param>
        /// <returns>受影响的行数，失败时返回 -1</returns>
        public int InsertFirmwareVersion(FirmwareVersion version)
        {
            try
            {
                const string sql = @"
                    INSERT INTO firmware_version (
                        version_id, device_id, firmware_version,
                        firmware_file_path, file_size_bytes, checksum,
                        release_notes, released_at
                    ) VALUES (
                        @version_id, @device_id, @firmware_version,
                        @firmware_file_path, @file_size_bytes, @checksum,
                        @release_notes, @released_at
                    );";

                var connection = DatabaseManager.Instance.Connection;
                using (var transaction = connection.BeginTransaction())
                {
                    int result = connection.Execute(sql, new
                    {
                        version_id = version.VersionId,
                        device_id = version.DeviceId,
                        firmware_version = version.FirmwareVersionNumber,
                        firmware_file_path = version.FirmwareFilePath,
                        file_size_bytes = version.FileSizeBytes,
                        checksum = version.Checksum,
                        release_notes = version.ReleaseNotes,
                        released_at = version.ReleasedAt ?? DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss")
                    }, transaction);
                    transaction.Commit();
                    return result;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[FirmwareRepository.InsertFirmwareVersion] 错误: {ex.Message}");
                return -1;
            }
        }

        /// <summary>
        /// 创建固件升级任务
        /// 向 upgrade_task 表创建一条新的升级任务记录。
        /// 初始状态为 pending，进度为 0%。
        /// </summary>
        /// <param name="task">升级任务实体</param>
        /// <returns>受影响的行数，失败时返回 -1</returns>
        public int CreateUpgradeTask(UpgradeTask task)
        {
            try
            {
                const string sql = @"
                    INSERT INTO upgrade_task (
                        task_id, device_id, from_version, to_version,
                        firmware_file, status, progress_percent,
                        started_at, completed_at, initiated_by, error_message
                    ) VALUES (
                        @task_id, @device_id, @from_version, @to_version,
                        @firmware_file, @status, @progress_percent,
                        @started_at, @completed_at, @initiated_by, @error_message
                    );";

                var connection = DatabaseManager.Instance.Connection;
                using (var transaction = connection.BeginTransaction())
                {
                    int result = connection.Execute(sql, new
                    {
                        task_id = task.TaskId,
                        device_id = task.DeviceId,
                        from_version = task.FromVersion,
                        to_version = task.ToVersion,
                        firmware_file = task.FirmwareFile,
                        status = task.Status ?? "pending",
                        progress_percent = task.ProgressPercent,
                        started_at = task.StartedAt,
                        completed_at = task.CompletedAt,
                        initiated_by = task.InitiatedBy,
                        error_message = task.ErrorMessage
                    }, transaction);
                    transaction.Commit();
                    return result;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[FirmwareRepository.CreateUpgradeTask] 错误: {ex.Message}");
                return -1;
            }
        }

        /// <summary>
        /// 更新升级任务的进度和状态
        /// 在固件升级过程中实时更新任务的进度百分比、状态和可能的错误信息。
        /// 当状态为 completed 或 failed 时自动记录完成时间。
        /// </summary>
        /// <param name="taskId">升级任务ID</param>
        /// <param name="progress">升级进度百分比(0~100)</param>
        /// <param name="status">任务状态: in_progress / completed / failed / cancelled</param>
        /// <param name="errorMessage">错误信息，仅在失败时填写</param>
        /// <returns>受影响的行数，失败时返回 -1</returns>
        public int UpdateUpgradeTaskProgress(string taskId, double progress, string status,
            string errorMessage = null)
        {
            try
            {
                var connection = DatabaseManager.Instance.Connection;
                using (var transaction = connection.BeginTransaction())
                {
                    // 根据状态决定是否记录完成时间
                    long? completedAt = null;
                    if (status == "completed" || status == "failed" || status == "cancelled")
                    {
                        completedAt = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                    }

                    // 当状态变为 in_progress 时记录开始时间
                    long? startedAt = null;
                    if (status == "in_progress")
                    {
                        startedAt = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                    }

                    const string sql = @"
                        UPDATE upgrade_task SET
                            progress_percent = @progress_percent,
                            status = @status,
                            error_message = @error_message
                        WHERE task_id = @task_id;";

                    int result = connection.Execute(sql, new
                    {
                        task_id = taskId,
                        progress_percent = progress,
                        status = status,
                        error_message = errorMessage
                    }, transaction);

                    // 如果有开始时间或完成时间需要单独更新
                    if (startedAt.HasValue)
                    {
                        const string startSql = @"
                            UPDATE upgrade_task SET started_at = @started_at
                            WHERE task_id = @task_id AND started_at IS NULL;";
                        connection.Execute(startSql, new { task_id = taskId, started_at = startedAt }, transaction);
                    }
                    if (completedAt.HasValue)
                    {
                        const string endSql = @"
                            UPDATE upgrade_task SET completed_at = @completed_at
                            WHERE task_id = @task_id AND completed_at IS NULL;";
                        connection.Execute(endSql, new { task_id = taskId, completed_at = completedAt }, transaction);
                    }

                    transaction.Commit();
                    return result;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[FirmwareRepository.UpdateUpgradeTaskProgress] 错误: {ex.Message}");
                return -1;
            }
        }

        /// <summary>
        /// 获取指定升级任务详情
        /// </summary>
        /// <param name="taskId">升级任务ID</param>
        /// <returns>升级任务对象，未找到返回 null</returns>
        public UpgradeTask GetUpgradeTask(string taskId)
        {
            try
            {
                const string sql = @"
                    SELECT task_id, device_id, from_version, to_version,
                           firmware_file, status, progress_percent,
                           started_at, completed_at, initiated_by, error_message
                    FROM upgrade_task
                    WHERE task_id = @task_id;";

                var connection = DatabaseManager.Instance.Connection;
                var task = connection.QuerySingleOrDefault<UpgradeTask>(sql, new { task_id = taskId });
                return task;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[FirmwareRepository.GetUpgradeTask] 错误: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// 获取所有活跃的升级任务
        /// 查询所有状态为 pending 或 in_progress 的升级任务，
        /// 按开始时间倒序排列。
        /// </summary>
        /// <returns>活跃升级任务列表</returns>
        public List<UpgradeTask> GetActiveUpgradeTasks()
        {
            try
            {
                const string sql = @"
                    SELECT task_id, device_id, from_version, to_version,
                           firmware_file, status, progress_percent,
                           started_at, completed_at, initiated_by, error_message
                    FROM upgrade_task
                    WHERE status IN ('pending', 'in_progress')
                    ORDER BY 
                        CASE WHEN started_at IS NOT NULL THEN started_at ELSE 0 END DESC;";

                var connection = DatabaseManager.Instance.Connection;
                var tasks = connection.Query<UpgradeTask>(sql).AsList();
                return tasks;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[FirmwareRepository.GetActiveUpgradeTasks] 错误: {ex.Message}");
                return new List<UpgradeTask>();
            }
        }

        /// <summary>
        /// 获取指定设备的升级历史
        /// 查询该设备的所有升级任务记录（包括已完成、失败和取消的任务），
        /// 按开始时间倒序排列。
        /// </summary>
        /// <param name="deviceId">设备唯一标识</param>
        /// <returns>升级任务列表（历史记录）</returns>
        public List<UpgradeTask> GetUpgradeHistory(string deviceId)
        {
            try
            {
                const string sql = @"
                    SELECT task_id, device_id, from_version, to_version,
                           firmware_file, status, progress_percent,
                           started_at, completed_at, initiated_by, error_message
                    FROM upgrade_task
                    WHERE device_id = @device_id
                    ORDER BY 
                        CASE WHEN started_at IS NOT NULL THEN started_at ELSE 0 END DESC;";

                var connection = DatabaseManager.Instance.Connection;
                var tasks = connection.Query<UpgradeTask>(sql, new { device_id = deviceId }).AsList();
                return tasks;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[FirmwareRepository.GetUpgradeHistory] 错误: {ex.Message}");
                return new List<UpgradeTask>();
            }
        }
    }
}
