EVerest MW级充放电上位机系统 - 完整开发文档
文档编号	EVT-MW-SCADA-DEV-2026-001
项目名称	基于EVerest的500kW~MW级动力电池/储能充放电系统上位机软件
文档版本	v1.0
编制日期	2026-04-24
目标平台	EVerest（LF Energy开源充电软件栈）
数据库引擎	SQLite 3.x
通讯硬件	周立功USBCAN系列（CAN 2.0B / CAN FD）
目标系统	500kW ~ MW级动力电池/储能系统充放电设备
适用范围	AI编程智能体自动开发；测试人员可修改配置文件后立即投入使用
目录
项目概述与架构总览

开发环境准备与项目结构

EVerest模块开发通用模板

功能模块开发规范

SQLite数据库设计

用户管理与权限控制

测试报告生成模块

系统集成与配置指南

自动化测试规范

附录：生产环境配置示例

1. 项目概述与架构总览
1.1 项目目标与设计原则
基于EVerest——LF Energy基金会旗下的开源EV充电软件栈——开发一套适用于500kW至MW级动力电池及储能系统充放电设备的上位机监控系统。EVerest采用模块化软件架构，各模块之间通过MQTT进行集成与协调。项目已包含对ISO 15118、OCPP、IEC 61851等协议的支持，确保广泛的兼容性和系统前瞻性。

核心设计原则：

配置驱动：所有功能通过YAML配置文件定义，测试人员修改配置即可投入使用，无需修改源代码

模块独立：每个功能模块独立开发、独立测试、独立部署

协议解耦：设备协议与电池协议抽象为独立模块，新增协议只需添加配置项

数据完整：采用WAL模式与事务批量提交，确保充放电数据不丢失

接口标准：所有模块通过EVerest标准接口通信，遵循tier-module映射规范

1.2 系统架构图
text
┌─────────────────────────────────────────────────────────────────┐
│                    前端展示层 (PySide6 Web UI)                      │
├─────────────────────────────────────────────────────────────────┤
│                           API 网关层                               │
│  ┌──────────┐ ┌──────────┐ ┌──────────┐ ┌──────────────────────┐ │
│  │ REST API │ │ WebSocket│ │ MQTT API │ │ Admin Panel(:8849)   │ │
│  └──────────┘ └──────────┘ └──────────┘ └──────────────────────┘ │
├─────────────────────────────────────────────────────────────────┤
│                        EVerest 核心框架                            │
│  ┌───────────────────────────────────────────────────────────┐  │
│  │                      MQTT 消息总线                          │  │
│  └───────────────────────────────────────────────────────────┘  │
│  ┌──────────┐ ┌──────────┐ ┌──────────┐ ┌────────────────────┐  │
│  │ EvseMgr  │ │ EnergyMgr│ │  Auth    │ │  自定义业务模块     │  │
│  │(充放电控)│ │(能源管理)│ │ (认证)   │ │                    │  │
│  └──────────┘ └──────────┘ └──────────┘ └────────────────────┘  │
├─────────────────────────────────────────────────────────────────┤
│                       自定义业务模块层 (12个新模块)                  │
│  ┌──────────┐ ┌──────────┐ ┌──────────┐ ┌────────────────────┐  │
│  │DevMonitor│ │DevManager│ │ Calibrat │ │ProtocolManager      │  │
│  │(设备监控)│ │(设备管理)│ │ (校准)   │ │(协议管理)           │  │
│  │0x0101    │ │0x0102    │ │0x0103/04 │ │0x0105/06            │  │
│  ├──────────┼ ┼──────────┼ ┼──────────┼ ┼────────────────────┤  │
│  │DataLogger│ │FirmwareUp│ │FaultRec  │ │UserManager /        │  │
│  │(数据记录)│ │(程序升级)│ │(故障录波)│ │ReportGenerator      │  │
│  │0x0201    │ │0x0202    │ │0x0203    │ │0x0301 / 0x0302      │  │
│  └──────────┘ └──────────┘ └──────────┘ └────────────────────┘  │
├─────────────────────────────────────────────────────────────────┤
│                        硬件抽象层 (HAL)                            │
│  ┌───────────────────────────────────────────────────────────┐  │
│  │           ZLG CAN Driver Module (0x0001)                   │  │
│  │   ┌──────────┐  ┌──────────┐  ┌──────────────────────┐    │  │
│  │   │SocketCAN │  │python-can│  │ ZLG 原生驱动(备用)    │    │  │
│  │   └──────────┘  └──────────┘  └──────────────────────┘    │  │
│  └───────────────────────────────────────────────────────────┘  │
├─────────────────────────────────────────────────────────────────┤
│                      物理层 / 现场设备                             │
│  ┌──────────┐  ┌──────────┐  ┌──────────┐  ┌──────────────┐    │
│  │充放电设备│  │  BMS系统  │  │ 储能变流器│  │  周立功CAN卡  │    │
│  │(MW级)    │  │(动力电池) │  │  (PCS)    │  │  (USBCAN-II) │    │
│  └──────────┘  └──────────┘  └──────────┘  └──────────────┘    │
└─────────────────────────────────────────────────────────────────┘
1.3 模块tier映射表
模块按照EVerest框架规范分为三个tier（层级）：

Tier	层级名称	说明	本系统模块
Tier 1	硬件驱动层	直接与物理设备交互，封装硬件差异	ZLG CAN Driver、系统工具
Tier 2	业务逻辑层	核心业务逻辑，协议解析，数据处理	设备监控、校准、协议管理、数据记录、程序升级、故障录波
Tier 3	应用服务层	面向用户的服务，API，报告生成	用户管理、测试报告生成、Web UI
1.4 需求-模块-接口对应总表
需求项	对应模块	模块ID	Tier	核心接口	数据库表
设备监控	DevMonitor	0x0101	2	zlg_can → dev_monitor	device_status, device_alarm
设备管理	DevManager	0x0102	2	dev_manager → config	device_info, device_config
设备校准	DeviceCalibration	0x0103	2	calibration → zlg_can	calibration_record
电压电流校准	VICalibration	0x0104	2	calibration → zlg_can	calibration_point
设备协议管理	ProtocolManager	0x0105	2	protocol → zlg_can	protocol_config, protocol_command
电池协议管理	BatteryProtocolMgr	0x0106	2	battery_proto → zlg_can	battery_protocol_config
数据记录	DataLogger	0x0201	2	data_logger → sqlite	measurement_data, session_record
程序自动升级	FirmwareUpgrader	0x0202	2	firmware → zlg_can	firmware_version, upgrade_task
故障数据录波	FaultRecorder	0x0203	2	fault_rec → zlg_can	fault_event, fault_waveform
用户管理	UserManager	0x0301	3	user_mgr → sqlite	user_account, user_role, permission
报告生成	ReportGenerator	0x0302	3	report → sqlite	test_report, report_template
ZLG CAN驱动	ZlgCanDriver	0x0001	1	zlg_can (核心基础接口)	-
1.5 配置文件驱动设计
系统采用配置文件驱动模式，测试人员只需修改YAML配置文件即可适配不同现场环境。所有配置文件存放于config/目录：

config/devices.yaml — 充放电设备参数定义

config/battery_protocols.yaml — 动力电池协议定义

config/calibration_params.yaml — 校准参数默认值

config/logging_config.yaml — 数据记录配置

config/alarm_thresholds.yaml — 告警阈值配置

config/users.yaml — 用户与权限初始配置

config/report_templates.yaml — 报告模板配置

2. 开发环境准备与项目结构
2.1 EVerest开发环境搭建
按照EVerest官方文档准备开发环境：

bash
# 1. 安装依赖（Ubuntu 22.04 LTS）
sudo apt update
sudo apt install -y build-essential cmake git wget \
    libboost-all-dev libssl-dev libsqlite3-dev \
    libmosquitto-dev mosquitto mosquitto-clients \
    python3 python3-pip python3-venv \
    nodejs npm

# 2. 克隆EVerest核心仓库
git clone https://github.com/EVerest/everest-core.git
cd everest-core

# 3. 按照Quick Start Guide构建
# 参考: https://everest.github.io/nightly/general/03_quick_start_guide.html
mkdir build && cd build
cmake ..
make -j$(nproc)
sudo make install
依赖清单：

操作系统：Ubuntu 22.04 LTS（推荐）或Debian 11+

编译工具：CMake ≥ 3.16, GCC ≥ 9.0

MQTT Broker：Mosquitto ≥ 2.0

数据库：SQLite ≥ 3.35

Python：Python 3.9+，依赖包：python-can≥4.0, pyside6≥6.5, pytest≥7.0, jinja2, weasyprint

硬件驱动：周立功USBCAN Linux驱动（内核4.4+）

** CAN工具**：can-utils (可选，用于调试)

2.2 项目目录结构
text
mw-charge-discharge-scada/
├── CMakeLists.txt                    # 顶层构建文件
├── module-dependencies.cmake         # 模块依赖关系配置
├── README.md                         # 项目说明
│
├── config/                           # 【核心】配置文件目录
│   ├── config.yaml                   # EVerest主配置文件
│   ├── devices.yaml                  # 充放电设备参数
│   ├── battery_protocols.yaml        # 动力电池协议定义
│   ├── calibration_params.yaml       # 校准参数
│   ├── logging_config.yaml           # 数据记录配置
│   ├── alarm_thresholds.yaml         # 告警阈值
│   ├── users.yaml                    # 用户权限
│   └── report_templates.yaml         # 报告模板
│
├── interfaces/                       # 接口定义（.yaml格式）
│   ├── zlg_can.yaml                  # 周立功CAN总线接口
│   ├── dev_monitor.yaml              # 设备监控接口
│   ├── dev_manager.yaml              # 设备管理接口
│   ├── calibration.yaml              # 校准接口
│   ├── protocol_manager.yaml         # 协议管理接口
│   ├── battery_protocol.yaml         # 电池协议接口
│   ├── data_logger.yaml              # 数据记录接口
│   ├── firmware_upgrader.yaml        # 固件升级接口
│   ├── fault_recorder.yaml           # 故障录波接口
│   ├── user_manager.yaml             # 用户管理接口
│   └── report_generator.yaml         # 报告生成接口
│
├── modules/                          # 业务模块实现
│   ├── ZlgCanDriver/                 # ZLG CAN驱动模块 (Tier 1)
│   │   ├── CMakeLists.txt
│   │   ├── manifest.yaml
│   │   ├── zlg_can_driver.cpp
│   │   ├── zlg_can_driver.hpp
│   │   ├── can_message_parser.cpp
│   │   └── can_message_parser.hpp
│   │
│   ├── DevMonitor/                   # 设备监控模块
│   ├── DevManager/                   # 设备管理模块
│   ├── DeviceCalibration/            # 设备校准模块
│   ├── VICalibration/                # 电压电流校准模块
│   ├── ProtocolManager/              # 设备协议管理模块
│   ├── BatteryProtocolMgr/           # 电池协议管理模块
│   ├── DataLogger/                   # 数据记录模块
│   ├── FirmwareUpgrader/             # 固件升级模块
│   ├── FaultRecorder/                # 故障录波模块
│   ├── UserManager/                  # 用户管理模块
│   └── ReportGenerator/              # 报告生成模块
│
├── lib/                              # 共享库
│   ├── sqlite_helper/                # SQLite操作封装
│   │   ├── CMakeLists.txt
│   │   ├── sqlite_manager.cpp
│   │   └── sqlite_manager.hpp
│   ├── can_protocol/                 # CAN协议通用工具
│   │   ├── CMakeLists.txt
│   │   ├── can_utils.cpp
│   │   └── can_utils.hpp
│   └── data_processor/               # 数据处理通用工具
│       ├── CMakeLists.txt
│       ├── data_buffer.cpp
│       └── data_buffer.hpp
│
├── python/                           # Python扩展模块
│   ├── zlg_can_bridge/               # CAN数据桥接
│   │   ├── __init__.py
│   │   ├── can_receiver.py
│   │   └── can_sender.py
│   ├── data_logger_service/          # 数据记录服务
│   │   ├── __init__.py
│   │   ├── db_writer.py
│   │   └── data_aggregator.py
│   └── report_engine/                # 报告引擎
│       ├── __init__.py
│       ├── report_generator.py
│       └── templates/
│
├── ui/                               # 前端界面
│   ├── web/
│   │   ├── index.html
│   │   ├── css/
│   │   ├── js/
│   │   └── pages/
│   └── admin_panel/                  # EVerest Admin Panel扩展
│
├── tests/                            # 测试用例
│   ├── unit/                         # 单元测试
│   │   ├── test_zlg_can_driver.cpp
│   │   ├── test_sqlite_manager.cpp
│   │   └── test_can_parser.cpp
│   ├── integration/                  # 集成测试
│   │   ├── test_dev_monitor.py
│   │   ├── test_calibration.py
│   │   └── test_data_logger.py
│   ├── e2e/                          # 端到端测试
│   │   └── test_full_flow.py
│   └── fixtures/                     # 测试夹具
│       ├── mock_can_data.py
│       └── test_config.yaml
│
├── scripts/                          # 部署与维护脚本
│   ├── deploy.sh                     # 一键部署脚本
│   ├── init_database.sh              # 数据库初始化
│   ├── backup_db.sh                  # 数据库备份
│   └── log_rotate.sh                 # 日志轮转
│
└── docs/                             # 文档
    ├── api_reference.md              # API参考
    ├── protocol_spec.md              # 协议规范
    └── user_manual.md                # 用户手册
2.3 模块依赖关系
使用EVerest框架的module-dependencies.cmake配置模块间依赖：

cmake
# module-dependencies.cmake
# 定义模块依赖关系，确保构建顺序正确

# Tier 1: 硬件驱动层（无依赖）
set(ZlgCanDriver_DEPENDS "")

# Tier 2: 业务逻辑层（依赖Tier 1）
set(DevMonitor_DEPENDS "ZlgCanDriver")
set(DevManager_DEPENDS "ZlgCanDriver")
set(DeviceCalibration_DEPENDS "ZlgCanDriver")
set(VICalibration_DEPENDS "ZlgCanDriver")
set(ProtocolManager_DEPENDS "ZlgCanDriver")
set(BatteryProtocolMgr_DEPENDS "ZlgCanDriver;ProtocolManager")
set(DataLogger_DEPENDS "ZlgCanDriver;DevMonitor")
set(FirmwareUpgrader_DEPENDS "ZlgCanDriver;DevManager")
set(FaultRecorder_DEPENDS "ZlgCanDriver;DevMonitor")

# Tier 3: 应用服务层（依赖Tier 2）
set(UserManager_DEPENDS "")
set(ReportGenerator_DEPENDS "DataLogger;DevMonitor;FaultRecorder")
2.4 排除/包含模块
使用CMake参数选择性构建模块：

bash
# 仅构建指定模块
cmake .. -DEVEREST_INCLUDE_MODULES="ZlgCanDriver;DevMonitor;DevManager;DataLogger"

# 排除不需要的模块
cmake .. -DEVEREST_EXCLUDE_MODULES="OCPP;ISO15118"
3. EVerest模块开发通用模板
3.1 模块开发核心流程
EVerest模块开发的核心是通过MQTT消息总线实现模块间通信。每个模块包含三个核心文件：

文件	作用	位置
manifest.yaml	定义模块ID、名称、接口依赖、配置参数	模块根目录
接口定义文件 (interface/*.yaml)	标准化接口，定义命令(commands)和变量(vars)	interfaces/目录
实现文件 (.cpp/.hpp)	业务逻辑代码	模块src/目录
构建与注册步骤：

bash
# 1. 构建项目
cd everest-core/build
cmake .. && make -j$(nproc) && sudo make install

# 2. 注册新模块到EVerest
ev-cli module register --path ./modules/NewModule

# 3. 验证模块是否在Admin Panel可见
# 访问 http://localhost:8849 检查Available modules列表
3.2 模块标准目录结构
text
modules/<ModuleName>/
├── CMakeLists.txt          # 构建脚本
├── manifest.yaml           # 模块声明（名称、版本、依赖、配置参数）
├── doc.rst                 # 模块文档
├── src/
│   ├── <module_name>.cpp   # 主实现文件
│   └── <module_name>.hpp   # 头文件
├── include/                # 公开头文件（可选）
└── tests/                  # 模块测试
    ├── CMakeLists.txt
    └── test_<module_name>.cpp
3.3 接口定义模板（YAML格式）
以interfaces/zlg_can.yaml为例：

yaml
# interfaces/zlg_can.yaml
name: zlg_can
description: "周立功USBCAN系列CAN总线通信接口"
vars:
  can_device:
    description: "CAN设备名称，如can0"
    type: string
    default: "can0"
  bitrate:
    description: "CAN总线波特率"
    type: integer
    default: 500000
  data_bitrate:
    description: "CAN FD数据段波特率（仅CAN FD模式）"
    type: integer
    default: 2000000
  can_mode:
    description: "CAN模式：CAN20B 或 CANFD"
    type: string
    default: "CAN20B"
    enum:
      - "CAN20B"
      - "CANFD"

cmds:
  send_message:
    description: "发送CAN消息"
    arguments:
      can_id:
        description: "CAN ID (11-bit 或 29-bit)"
        type: integer
      data:
        description: "数据载荷（十六进制字节数组）"
        type: array
      is_extended:
        description: "是否为扩展帧"
        type: boolean
        default: false
    result:
      description: "发送结果状态码"
      type: integer

  send_batch_messages:
    description: "批量发送CAN消息，用于固件升级等大数据量场景"
    arguments:
      messages:
        description: "CAN消息数组"
        type: array
    result:
      description: "发送结果"
      type: object

  set_bitrate:
    description: "动态设置CAN总线波特率"
    arguments:
      bitrate:
        description: "新的波特率值"
        type: integer
    result:
      description: "设置结果状态码"
      type: integer

vars:
  bus_status:
    description: "CAN总线状态"
    type: string
    enum:
      - "OK"
      - "ERROR"
      - "BUS_OFF"
      - "UNKNOWN"
    access: read_only
  tx_counter:
    description: "发送消息计数器"
    type: integer
    access: read_only
  error_counters:
    description: "错误计数器 (rx_err, tx_err)"
    type: object
    access: read_only
3.4 manifest.yaml 模板
yaml
# modules/DevMonitor/manifest.yaml
name: DevMonitor
description: "设备监控模块—采集充放电设备的电压/电流/温度/状态数据"
provides:
  dev_monitor:
    description: "提供设备实时监控数据"
    interface: dev_monitor
requires:
  zlg_can:
    interface: zlg_can
    min_connections: 1
    max_connections: 1
metadata:
  license: "Apache-2.0"
  authors:
    - "开发团队名称"
  tier: 2
config:
  poll_interval_ms:
    description: "数据采集轮询间隔（毫秒）"
    type: integer
    default: 100
    minimum: 10
    maximum: 10000
  alarm_thresholds_file:
    description: "告警阈值配置文件路径"
    type: string
    default: "config/alarm_thresholds.yaml"
  watchdog_timeout_s:
    description: "看门狗超时时间（秒），0表示禁用"
    type: integer
    default: 30
3.5 模块C++实现骨架
cpp
// modules/DevMonitor/src/dev_monitor.hpp
#pragma once

#include <string>
#include <chrono>
#include <vector>
#include <memory>
#include <everest.hpp>
#include <framework/ModuleAdapter.hpp>

namespace module {

struct DeviceDataPoint {
    std::string device_id;        // 设备标识
    std::string parameter_name;   // 参数名称
    double value;                 // 参数值
    std::string unit;             // 单位
    std::string quality;          // 数据质量: GOOD/UNCERTAIN/BAD
    std::chrono::system_clock::time_point timestamp;
};

struct AlarmInfo {
    std::string alarm_id;         // 告警ID
    std::string alarm_level;      // 告警级别: CRITICAL/MAJOR/MINOR/WARNING
    std::string alarm_source;     // 告警来源设备
    std::string alarm_message;    // 告警描述
    bool acknowledged;            // 是否已确认
    std::chrono::system_clock::time_point timestamp;
};

class DevMonitor : public Everest::ModuleAdapter {
public:
    DevMonitor() = delete;
    explicit DevMonitor(const boost::property_tree::ptree& config,
                        const boost::property_tree::ptree& module_info);
    
    // EVerest模块生命周期回调
    void init() override;
    void ready() override;
    void run() override;
    void stop() override;
    
private:
    // 数据采集核心方法
    void startPolling();
    void stopPolling();
    DeviceDataPoint parseCANMessage(const std::vector<uint8_t>& raw_data, 
                                     uint32_t can_id);
    
    // 告警检测
    bool checkAlarmThreshold(const DeviceDataPoint& data_point);
    void raiseAlarm(const AlarmInfo& alarm);
    void clearAlarm(const std::string& alarm_id);
    
    // 配置
    int poll_interval_ms_;
    std::string alarm_thresholds_file_;
    int watchdog_timeout_s_;
    
    // 状态
    std::atomic<bool> running_{false};
    std::shared_ptr<std::thread> poll_thread_;
    
    // 统计数据
    std::atomic<uint64_t> total_messages_received_{0};
    std::atomic<uint64_t> total_alarms_raised_{0};
};

} // namespace module


// modules/DevMonitor/src/dev_monitor.cpp
#include "dev_monitor.hpp"
#include <fstream>
#include <yaml-cpp/yaml.h>

namespace module {

DevMonitor::DevMonitor(const boost::property_tree::ptree& config,
                       const boost::property_tree::ptree& module_info)
    : ModuleAdapter(config, module_info) {
    // 从配置中加载参数
    poll_interval_ms_ = config.get<int>("poll_interval_ms", 100);
    alarm_thresholds_file_ = config.get<std::string>("alarm_thresholds_file", 
                                                       "config/alarm_thresholds.yaml");
    watchdog_timeout_s_ = config.get<int>("watchdog_timeout_s", 30);
}

void DevMonitor::init() {
    EVLOG_info << "DevMonitor module initializing...";
    EVLOG_info << "  Poll interval: " << poll_interval_ms_ << " ms";
    EVLOG_info << "  Alarm thresholds: " << alarm_thresholds_file_;
}

void DevMonitor::ready() {
    EVLOG_info << "DevMonitor module ready, starting data polling...";
    startPolling();
}

void DevMonitor::run() {
    // 主循环：看门狗逻辑
    while (running_) {
        // 检查数据采集线程是否健康
        // 如果超过watchdog_timeout_s_无新数据，触发看门狗告警
        std::this_thread::sleep_for(std::chrono::seconds(1));
    }
}

void DevMonitor::stop() {
    EVLOG_info << "DevMonitor module stopping...";
    running_ = false;
    stopPolling();
}

void DevMonitor::startPolling() {
    running_ = true;
    poll_thread_ = std::make_shared<std::thread>([this]() {
        while (running_) {
            // 通过zlg_can接口读取CAN总线数据
            auto raw_data = call_cmd("zlg_can", "read_message", {});
            if (!raw_data.empty()) {
                auto data_point = parseCANMessage(raw_data["data"], raw_data["can_id"]);
                
                // 发布监控数据到MQTT
                publish_var("dev_monitor", "latest_data", data_point);
                total_messages_received_++;
                
                // 检查告警阈值
                if (checkAlarmThreshold(data_point)) {
                    total_alarms_raised_++;
                }
            }
            std::this_thread::sleep_for(std::chrono::milliseconds(poll_interval_ms_));
        }
    });
}

void DevMonitor::stopPolling() {
    if (poll_thread_ && poll_thread_->joinable()) {
        poll_thread_->join();
    }
}

DeviceDataPoint DevMonitor::parseCANMessage(const std::vector<uint8_t>& raw_data, 
                                              uint32_t can_id) {
    // CAN消息解析逻辑：根据can_id和协议定义解析数据
    // 具体实现参考第4.1节
    DeviceDataPoint dp;
    dp.timestamp = std::chrono::system_clock::now();
    // TODO: 实现协议解析
    return dp;
}

bool DevMonitor::checkAlarmThreshold(const DeviceDataPoint& data_point) {
    // 从alarm_thresholds.yaml加载阈值并比较
    // 实现细节参考第4.1.4节
    return false;
}

void DevMonitor::raiseAlarm(const AlarmInfo& alarm) {
    publish_var("dev_monitor", "active_alarm", alarm);
    EVLOG_warning << "Alarm raised: " << alarm.alarm_message;
}

void DevMonitor::clearAlarm(const std::string& alarm_id) {
    publish_var("dev_monitor", "alarm_cleared", alarm_id);
}

} // namespace module
3.6 EVerest Admin Panel 开发
利用EVerest提供的Admin Panel（默认运行于ip:8849）进行模块管理和可视化配置：

yaml
# config/config.yaml — EVerest主配置
active_modules:
  # Admin Panel — 通过浏览器管理所有模块
  admin_panel:
    module: AdminPanel
    config_module:
      port: 8849
      host: "0.0.0.0"
      enable_authentication: true
  api:
    module: API
    connections:
      admin_panel:
        - module_id: admin_panel
          implementation_id: admin_api

  # 新增：业务模块
  zlg_can_driver:
    module: ZlgCanDriver
    config_module:
      can_device: "can0"
      bitrate: 500000
  dev_monitor:
    module: DevMonitor
    config_module:
      poll_interval_ms: 100
    connections:
      zlg_can:
        - module_id: zlg_can_driver
          implementation_id: zlg_can
3.7 模块日志规范
遵循EVerest统一的日志系统：

cpp
// 使用EVerest内置日志宏（线程安全，自动包含时间戳和模块名）
EVLOG_debug << "详细调试信息，仅开发阶段启用";
EVLOG_info << "常规运行信息：数据采集正常，已采集 " << count << " 个数据点";
EVLOG_warning << "警告信息：缓存使用率超过80%";
EVLOG_error << "错误信息：CAN总线通信超时，错误码: " << error_code;
EVLOG_critical << "严重错误：数据库写入失败，系统将尝试自动恢复";

// 带结构化数据
EVLOG_info << boost::format("设备[%1%] 电压: %2% V, 电流: %3% A")
                  % device_id % voltage % current;
4. 功能模块开发规范
4.1 设备监控模块 (DevMonitor)
模块ID：0x0101

功能描述：实时采集充放电设备的电压、电流、温度、SOC、SOH等数据，进行告警阈值检测，并将数据发布到MQTT总线供其他模块消费。

4.1.1 接口定义
yaml
# interfaces/dev_monitor.yaml
name: dev_monitor
description: "设备监控接口—实时数据采集与告警"

cmds:
  start_monitoring:
    description: "启动设备监控"
    arguments:
      device_ids:
        description: "要监控的设备ID列表，空表示全部"
        type: array
    result:
      description: "启动结果"
      type: object

  stop_monitoring:
    description: "停止设备监控"
    result:
      description: "停止结果"
      type: object

  get_latest_data:
    description: "获取指定设备的最新数据快照"
    arguments:
      device_id:
        description: "设备ID"
        type: string
    result:
      description: "最新数据"
      type: object

  get_alarm_history:
    description: "获取告警历史记录"
    arguments:
      start_time:
        description: "开始时间（ISO 8601格式）"
        type: string
      end_time:
        description: "结束时间"
        type: string
      alarm_level:
        description: "告警级别过滤（可选）"
        type: string
        required: false
    result:
      description: "告警记录数组"
      type: array

vars:
  latest_data:
    description: "所有设备的最新数据（Map型，key为device_id）"
    type: object
    access: read_only
  active_alarms:
    description: "当前活跃告警列表"
    type: array
    access: read_only
  monitoring_status:
    description: "监控状态: RUNNING/STOPPED/ERROR"
    type: string
    access: read_only
4.1.2 CAN报文解析逻辑
cpp
// 报文解析规则 — 支持多种设备协议格式
DeviceDataPoint DevMonitor::parseCANMessage(const std::vector<uint8_t>& data,
                                              uint32_t can_id) {
    DeviceDataPoint dp;
    dp.timestamp = std::chrono::system_clock::now();
    
    // 根据CAN ID确定消息类型（需与config/devices.yaml对应）
    uint8_t message_type = (can_id >> 24) & 0x1F;  // 高5位为消息类型
    uint8_t device_addr = can_id & 0xFF;            // 低8位为设备地址
    
    dp.device_id = "DEV_" + std::to_string(device_addr);
    
    switch (message_type) {
        case 0x01:  // 电压数据报文
            dp.parameter_name = "voltage";
            // 解析：Byte0-1为电压值（0.1V精度，大端序）
            dp.value = ((data[0] << 8) | data[1]) * 0.1;
            dp.unit = "V";
            dp.quality = (data[2] & 0x80) ? "GOOD" : "UNCERTAIN";
            break;
        case 0x02:  // 电流数据报文
            dp.parameter_name = "current";
            // 解析：Byte0-1为电流值（0.01A精度，有符号大端序）
            int16_t raw_current = (data[0] << 8) | data[1];
            dp.value = raw_current * 0.01;
            dp.unit = "A";
            dp.quality = (data[2] & 0x80) ? "GOOD" : "UNCERTAIN";
            break;
        case 0x03:  // 温度数据报文
            dp.parameter_name = "temperature";
            dp.value = static_cast<int8_t>(data[0]) + data[1] * 0.1;
            dp.unit = "℃";
            dp.quality = (data[2] & 0x80) ? "GOOD" : "UNCERTAIN";
            break;
        case 0x04:  // SOC/SOH数据报文
            dp.parameter_name = "soc";
            dp.value = data[0] * 0.5;  // 0~200对应0%~100%
            dp.unit = "%";
            dp.quality = (data[2] & 0x80) ? "GOOD" : "UNCERTAIN";
            break;
        default:
            dp.parameter_name = "unknown";
            dp.value = 0.0;
            dp.unit = "";
            dp.quality = "BAD";
            break;
    }
    
    return dp;
}
4.1.3 设备参数配置模板
yaml
# config/devices.yaml
# 测试人员修改此文件即可适配不同设备和协议
devices:
  - device_id: "PCS_001"               # 充放电设备标识
    device_type: "PCS"                 # 设备类型: PCS/BMS/INVERTER/METER
    device_name: "储能变流器1#"
    rated_power_kw: 1000               # 额定功率(kW)
    rated_voltage_v: 800               # 额定电压(V)
    rated_current_a: 1250              # 额定电流(A)
    can_address: 0x01                  # CAN总线地址
    protocol: "Modbus_CAN"             # 通信协议: Modbus_CAN/CANopen/J1939/Custom
    polling_enabled: true              # 是否启用轮询
    data_points:
      - name: "dc_voltage"
        description: "直流侧电压"
        can_id: 0x181
        data_offset: 0
        data_length: 2
        scale_factor: 0.1
        unit: "V"
        alarm:
          high_high: 900
          high: 850
          low: 600
          low_low: 500
      
      - name: "dc_current"
        description: "直流侧电流"
        can_id: 0x182
        data_offset: 0
        data_length: 2
        scale_factor: 0.01
        unit: "A"
        alarm:
          high_high: 1300
          high: 1200
          low: -1300
          low_low: -1200
      
      - name: "igbt_temp"
        description: "IGBT模块温度"
        can_id: 0x183
        data_offset: 0
        data_length: 2
        scale_factor: 0.1
        unit: "℃"
        alarm:
          high_high: 120
          high: 100
4.1.4 告警阈值配置
yaml
# config/alarm_thresholds.yaml
alarm_levels:
  CRITICAL: { priority: 1, color: "red", action: "immediate_shutdown" }
  MAJOR:    { priority: 2, color: "orange", action: "reduce_power" }
  MINOR:    { priority: 3, color: "yellow", action: "notify_operator" }
  WARNING:  { priority: 4, color: "blue", action: "log_only" }

global_alarm_rules:
  communication_loss:
    description: "通信丢失检测"
    check_interval_s: 5
    timeout_s: 30
    level: MAJOR
  
  data_quality_degraded:
    description: "数据质量下降"
    bad_ratio_threshold: 0.3        # BAD数据占比超过30%触发告警
    window_size: 100                 # 滑动窗口大小
    level: MINOR
4.2 设备管理模块 (DevManager)
模块ID：0x0102

功能描述：管理充放电设备的注册、配置、状态查询和参数修改。

4.2.1 接口定义（核心部分）
yaml
# interfaces/dev_manager.yaml
name: dev_manager
description: "设备管理接口"

cmds:
  register_device:
    description: "注册新设备"
    arguments:
      device_info:
        description: "设备信息结构体"
        type: object
        properties:
          device_id: { type: string }
          device_type: { type: string }
          device_name: { type: string }
          can_address: { type: integer }
          protocol: { type: string }
    result:
      description: "注册结果"
      type: object

  get_device_list:
    description: "获取所有已注册设备列表"
    arguments:
      filter_type:
        description: "按设备类型过滤（可选）"
        type: string
        required: false
      filter_status:
        description: "按设备状态过滤（可选）"
        type: string
        required: false
    result:
      description: "设备列表"
      type: array

  set_device_parameter:
    description: "设置设备参数"
    arguments:
      device_id:
        description: "设备ID"
        type: string
      parameter_name:
        description: "参数名称"
        type: string
      parameter_value:
        description: "参数值"
        type: any
    result:
      description: "设置结果"
      type: object

  get_device_status:
    description: "获取设备当前状态"
    arguments:
      device_id:
        description: "设备ID"
        type: string
    result:
      description: "设备状态详情"
      type: object

vars:
  device_registry:
    description: "设备注册表"
    type: object
    access: read_only
  device_count:
    description: "设备总数统计"
    type: object
    access: read_only
4.3 设备校准模块 (DeviceCalibration/VICalibration)
模块ID：0x0103（设备校准）/ 0x0104（电压电流校准）

功能描述：实现充放电设备的参数校准，包括零点校准、量程校准、线性校准，以及电压电流的精确校准。

4.3.1 校准接口定义
yaml
# interfaces/calibration.yaml
name: calibration
description: "设备校准接口"

cmds:
  start_calibration:
    description: "启动校准流程"
    arguments:
      device_id:
        description: "待校准设备ID"
        type: string
      calibration_type:
        description: "校准类型"
        type: string
        enum:
          - "ZERO"          # 零点校准
          - "SPAN"          # 量程校准
          - "LINEAR"        # 线性校准（多点）
          - "VI_CURRENT"    # 电流校准
          - "VI_VOLTAGE"    # 电压校准
      reference_value:
        description: "标准参考值（仅ZERO和SPAN类型需要）"
        type: number
        required: false
      calibration_points:
        description: "多点校准的校准点（仅LINEAR类型需要）"
        type: array
        required: false
    result:
      description: "校准启动结果，返回校准会话ID"
      type: object

  get_calibration_status:
    description: "查询校准进度"
    arguments:
      session_id:
        description: "校准会话ID"
        type: string
    result:
      description: "校准状态详情"
      type: object

  apply_calibration:
    description: "应用校准参数到设备"
    arguments:
      session_id:
        description: "校准会话ID"
        type: string
    result:
      description: "应用结果"
      type: object

  get_calibration_history:
    description: "获取设备历史校准记录"
    arguments:
      device_id:
        description: "设备ID"
        type: string
      start_time:
        description: "开始时间"
        type: string
      end_time:
        description: "结束时间"
        type: string
    result:
      description: "校准历史记录数组"
      type: array

vars:
  calibration_in_progress:
    description: "当前是否正在执行校准"
    type: boolean
    access: read_only
4.3.2 校准参数配置
yaml
# config/calibration_params.yaml
calibration_defaults:
  zero_calibration:
    description: "零点校准—在输入端短路情况下测量零偏"
    procedure:
      - "确保设备输入端处于短路状态（电流为0，电压为0）"
      - "等待设备稳定运行至少5分钟"
      - "采样100个数据点取平均值作为零点偏移值"
    samples_count: 100
    stability_threshold_percent: 0.1  # 稳定性阈值0.1%
    max_offset_percent: 2.0           # 最大允许零偏2%
    
  span_calibration:
    description: "量程校准—在标准参考源下测量满量程值"
    procedure:
      - "连接标准参考源（精度≥0.05%）"
      - "设置参考源输出为设备额定值的80%~100%"
      - "等待设备读数稳定（波动<0.1%/分钟）"
      - "采样50个数据点计算增益系数"
    reference_source_accuracy: 0.05   # 参考源精度要求
    samples_count: 50
    stability_threshold_percent: 0.1
    gain_range:
      min: 0.95
      max: 1.05
      
  vi_calibration:
    description: "电压电流精确校准"
    voltage_points: [10, 50, 100, 200, 500, 800]      # 电压校准点(V)
    current_points: [100, 250, 500, 750, 1000, 1250]   # 电流校准点(A)
    tolerance_percent: 0.1                              # 允许误差
    require_environment_data: true                       # 是否需要环境温度

calibration_validity_period_days: 90  # 校准有效期
calibration_reminder_days: 7          # 到期前7天提醒
4.3.3 校准算法实现
cpp
// 线性校准算法实现
struct CalibrationResult {
    double offset;      // 零点偏移
    double gain;        // 增益系数
    double linearity;   // 线性度（0~1，1为完美线性）
    bool passed;        // 是否通过校准
    std::string error_message;
};

CalibrationResult performLinearCalibration(
    const std::vector<double>& reference_values,   // 标准参考值
    const std::vector<double>& measured_values      // 设备测量值
) {
    CalibrationResult result;
    size_t n = reference_values.size();
    
    if (n != measured_values.size() || n < 2) {
        result.passed = false;
        result.error_message = "校准点数不足或数据长度不匹配";
        return result;
    }
    
    // 最小二乘法线性回归：y = gain * x + offset
    double sum_x = 0, sum_y = 0, sum_xy = 0, sum_x2 = 0;
    for (size_t i = 0; i < n; i++) {
        sum_x += reference_values[i];
        sum_y += measured_values[i];
        sum_xy += reference_values[i] * measured_values[i];
        sum_x2 += reference_values[i] * reference_values[i];
    }
    
    double denominator = n * sum_x2 - sum_x * sum_x;
    if (std::abs(denominator) < 1e-10) {
        result.passed = false;
        result.error_message = "校准数据异常，无法计算回归参数";
        return result;
    }
    
    result.gain = (n * sum_xy - sum_x * sum_y) / denominator;
    result.offset = (sum_y - result.gain * sum_x) / n;
    
    // 计算线性度（R²）
    double mean_y = sum_y / n;
    double ss_res = 0, ss_tot = 0;
    for (size_t i = 0; i < n; i++) {
        double predicted = result.gain * reference_values[i] + result.offset;
        ss_res += (measured_values[i] - predicted) * (measured_values[i] - predicted);
        ss_tot += (measured_values[i] - mean_y) * (measured_values[i] - mean_y);
    }
    result.linearity = 1.0 - (ss_res / ss_tot);
    
    // 校验校准结果是否在允许范围内
    bool gain_ok = (result.gain >= 0.95 && result.gain <= 1.05);
    bool offset_ok = std::abs(result.offset) < 100;  // 偏移量阈值根据实际设备调整
    bool linearity_ok = result.linearity >= 0.999;
    result.passed = gain_ok && offset_ok && linearity_ok;
    
    if (!result.passed) {
        result.error_message = "校准结果超出允许范围: "
                               + std::string(gain_ok ? "" : "增益异常; ")
                               + std::string(offset_ok ? "" : "零偏过大; ")
                               + std::string(linearity_ok ? "" : "线性度不足");
    }
    
    return result;
}
4.4 协议管理模块 (ProtocolManager / BatteryProtocolMgr)
模块ID：0x0105（设备协议管理）/ 0x0106（电池协议管理）

功能描述：管理充放电设备和动力电池的通信协议定义，支持协议的动态加载、解析、验证和切换。

4.4.1 协议管理接口
yaml
# interfaces/protocol_manager.yaml
name: protocol_manager
description: "通信协议管理接口"

cmds:
  load_protocol:
    description: "加载协议定义文件"
    arguments:
      protocol_name:
        description: "协议名称"
        type: string
      protocol_file:
        description: "协议定义YAML文件路径"
        type: string
    result:
      description: "加载结果"
      type: object

  list_protocols:
    description: "列出所有已加载的协议"
    result:
      description: "协议列表"
      type: array

  parse_message:
    description: "根据协议解析CAN消息"
    arguments:
      protocol_name:
        description: "使用的协议名称"
        type: string
      can_id:
        description: "CAN消息ID"
        type: integer
      raw_data:
        description: "原始数据字节数组"
        type: array
    result:
      description: "解析后的结构化数据"
      type: object

  validate_message:
    description: "验证消息是否符合协议规范"
    arguments:
      protocol_name:
        description: "协议名称"
        type: string
      message:
        description: "待验证消息"
        type: object
    result:
      description: "验证结果"
      type: object

vars:
  loaded_protocols:
    description: "当前已加载的协议列表"
    type: array
    access: read_only
  protocol_parse_errors:
    description: "协议解析错误计数"
    type: object
    access: read_only
4.4.2 电池协议管理接口
yaml
# interfaces/battery_protocol.yaml
name: battery_protocol
description: "动力电池协议管理接口"

cmds:
  load_battery_protocol:
    description: "加载电池协议"
    arguments:
      protocol_name:
        description: "协议名称"
        type: string
      bms_vendor:
        description: "BMS供应商"
        type: string
      protocol_version:
        description: "协议版本"
        type: string
    result:
      description: "加载结果"
      type: object

  get_cell_data:
    description: "获取单体电池数据"
    arguments:
      cell_indices:
        description: "单体索引数组（空表示全部）"
        type: array
        required: false
    result:
      description: "单体数据数组"
      type: array

  get_pack_data:
    description: "获取电池包总数据"
    result:
      description: "电池包数据"
      type: object

  send_bms_command:
    description: "发送BMS控制命令"
    arguments:
      command_name:
        description: "命令名称"
        type: string
      parameters:
        description: "命令参数"
        type: object
    result:
      description: "命令执行结果"
      type: object

vars:
  bms_connection_status:
    description: "BMS连接状态"
    type: string
    access: read_only
  cell_count:
    description: "电池单体数量"
    type: integer
    access: read_only
4.4.3 电池协议配置模板
yaml
# config/battery_protocols.yaml
battery_protocols:
  - protocol_name: "GB_T_27930_2015"
    description: "电动汽车非车载传导式充电机与电池管理系统之间的通信协议"
    version: "2015"
    bms_vendors: ["CATL", "BYD", "Gotion", "CALB", "EVE"]
    can_baudrate: 250000
    
    pgn_definitions:
      - pgn: 0x1000
        name: "BMS_Broadcast"
        description: "BMS广播报文"
        cycle_time_ms: 100
        fields:
          - name: "pack_voltage"
            offset: 0
            length: 2
            scale: 0.1
            unit: "V"
          - name: "pack_current"
            offset: 2
            length: 2
            scale: 0.1
            unit: "A"
            signed: true
      
      - pgn: 0x1100
        name: "Cell_Voltage_Data"
        description: "单体电压数据（多帧传输）"
        cycle_time_ms: 1000
        multi_frame: true
        fields:
          - name: "frame_index"
            offset: 0
            length: 1
          - name: "cell_voltages"
            offset: 1
            length: 7
            scale: 0.001
            unit: "V"
            count: 3          # 每帧3个单体电压
      
      - pgn: 0x1200
        name: "BMS_Status"
        description: "BMS状态信息"
        cycle_time_ms: 500
        fields:
          - name: "soc"
            offset: 0
            length: 1
            scale: 0.4
            unit: "%"
          - name: "soh"
            offset: 1
            length: 1
            scale: 0.4
            unit: "%"
          - name: "max_cell_temp"
            offset: 2
            length: 1
            scale: 1.0
            unit: "℃"
            offset_value: -40
          - name: "fault_code"
            offset: 3
            length: 2
            type: "bitmap"
    
    fault_code_definitions:
      - bit: 0
        name: "OVERVOLTAGE"
        level: "CRITICAL"
        description: "单体过压"
      - bit: 1
        name: "UNDERVOLTAGE"
        level: "CRITICAL"
        description: "单体欠压"
      - bit: 5
        name: "OVERTEMP"
        level: "MAJOR"
        description: "电池过温"
      - bit: 15
        name: "COMMUNICATION_ERROR"
        level: "MAJOR"
        description: "BMS通信异常"

  - protocol_name: "Custom_Protocol"
    description: "自定义协议模板—供测试人员根据实际设备修改"
    version: "1.0"
    can_baudrate: 500000
    pgn_definitions: []
    fault_code_definitions: []
4.5 数据记录模块 (DataLogger)
模块ID：0x0201

功能描述：将充放电过程中的所有监控数据持久化存储到SQLite数据库，支持按时间、设备、数据类型等多维度查询。

4.5.1 数据记录接口
yaml
# interfaces/data_logger.yaml
name: data_logger
description: "数据记录接口—充放电数据持久化存储与查询"

cmds:
  start_recording:
    description: "开始记录数据"
    arguments:
      session_id:
        description: "充放电会话ID"
        type: string
      data_types:
        description: "需要记录的数据类型列表（空表示全部）"
        type: array
        required: false
      sample_interval_ms:
        description: "采样间隔（毫秒，默认1000ms）"
        type: integer
        required: false
    result:
      description: "开始记录结果"
      type: object

  stop_recording:
    description: "停止记录数据"
    arguments:
      session_id:
        description: "会话ID"
        type: string
    result:
      description: "停止记录结果"
      type: object

  query_data:
    description: "查询历史数据"
    arguments:
      device_id:
        description: "设备ID（可选）"
        type: string
        required: false
      parameter_names:
        description: "参数名称列表（可选）"
        type: array
        required: false
      start_time:
        description: "开始时间（ISO 8601）"
        type: string
      end_time:
        description: "结束时间"
        type: string
      aggregation:
        description: "聚合方式: none/avg/min/max"
        type: string
        default: "none"
        required: false
      aggregation_interval_s:
        description: "聚合间隔（秒）"
        type: integer
        default: 60
        required: false
    result:
      description: "查询结果"
      type: array

  get_session_list:
    description: "获取充放电会话列表"
    arguments:
      start_time:
        description: "开始时间"
        type: string
        required: false
      end_time:
        description: "结束时间"
        type: string
        required: false
    result:
      description: "会话列表"
      type: array

  delete_old_data:
    description: "删除指定时间之前的历史数据"
    arguments:
      before_time:
        description: "删除此时间之前的数据"
        type: string
    result:
      description: "删除结果与释放空间大小"
      type: object

vars:
  recording_status:
    description: "记录状态: IDLE/RECORDING/ERROR"
    type: string
    access: read_only
  database_size_mb:
    description: "数据库文件大小（MB）"
    type: number
    access: read_only
  total_records:
    description: "总记录数"
    type: integer
    access: read_only
4.5.2 数据写入实现（批量优化）
cpp
// DataLogger批量写入实现 — 优化SQLite存储性能
// EVerest框架线程安全，本模块通过MQTT异步接收数据

#include "sqlite_manager.hpp"  // SQLite操作封装

class DataBuffer {
private:
    std::vector<MeasurementRecord> buffer_;
    std::mutex mutex_;
    size_t max_buffer_size_ = 1000;       // 缓冲区最大条目数
    std::chrono::milliseconds max_age_{5000}; // 缓冲区最大保留时间
    
public:
    bool shouldFlush() const {
        return buffer_.size() >= max_buffer_size_ || buffer_age() >= max_age_;
    }
    
    std::vector<MeasurementRecord> flush() {
        std::lock_guard<std::mutex> lock(mutex_);
        auto result = std::move(buffer_);
        buffer_.clear();
        return result;
    }
};

void DataLogger::writeBatch(const std::vector<MeasurementRecord>& records) {
    // 使用事务批量写入 — 显著提升SQLite写入性能
    // 参考SQLite论坛最佳实践：组合多条INSERT到单个事务
    sqlite3_exec(db_, "BEGIN TRANSACTION", nullptr, nullptr, nullptr);
    
    sqlite3_stmt* stmt;
    const char* sql = 
        "INSERT INTO measurement_data "
        "(timestamp, device_id, session_id, parameter_name, value, unit, quality) "
        "VALUES (?, ?, ?, ?, ?, ?, ?)";
    sqlite3_prepare_v2(db_, sql, -1, &stmt, nullptr);
    
    for (const auto& record : records) {
        sqlite3_bind_int64(stmt, 1, record.timestamp_ms);  // 毫秒级时间戳
        sqlite3_bind_text(stmt, 2, record.device_id.c_str(), -1, SQLITE_TRANSIENT);
        sqlite3_bind_text(stmt, 3, record.session_id.c_str(), -1, SQLITE_TRANSIENT);
        sqlite3_bind_text(stmt, 4, record.parameter_name.c_str(), -1, SQLITE_TRANSIENT);
        sqlite3_bind_double(stmt, 5, record.value);
        sqlite3_bind_text(stmt, 6, record.unit.c_str(), -1, SQLITE_TRANSIENT);
        sqlite3_bind_text(stmt, 7, record.quality.c_str(), -1, SQLITE_TRANSIENT);
        
        sqlite3_step(stmt);
        sqlite3_reset(stmt);
    }
    
    sqlite3_finalize(stmt);
    sqlite3_exec(db_, "COMMIT", nullptr, nullptr, nullptr);
}
4.5.3 数据记录配置
yaml
# config/logging_config.yaml
data_logging:
  default_sample_interval_ms: 1000        # 默认采样间隔1秒
  fast_sample_interval_ms: 100            # 快速采样间隔（故障时使用）
  
  buffer:
    max_size: 1000                         # 缓冲区最大条目数
    flush_interval_ms: 5000               # 缓冲区刷新间隔
    max_age_ms: 10000                     # 缓冲区最大保留时间
  
  data_retention:
    high_resolution_days: 30              # 高分辨率数据保留30天
    aggregated_1min_days: 365             # 1分钟聚合数据保留1年
    aggregated_1hour_days: 1825           # 1小时聚合数据保留5年
  
  auto_backup:
    enabled: true
    interval_hours: 24                     # 每天备份一次
    backup_path: "/data/backup/"
    max_backups: 30                        # 保留最近30个备份
  
  performance:
    pragma_journal_mode: "WAL"             # 使用WAL模式，支持并发读写
    pragma_synchronous: "NORMAL"           # 平衡性能与安全性
    pragma_cache_size: -65536              # 64MB缓存
    pragma_page_size: 4096                 # 4KB页面
    pragma_temp_store: "MEMORY"            # 临时表存储在内存中
4.6 固件升级模块 (FirmwareUpgrader)
模块ID：0x0202

功能描述：充放电设备程序自动升级。通过与下位机Bootloader配合，通过周立功CAN卡下发升级指令和固件数据包，实现设备固件的远程自动升级。

前提条件：此功能的实现前提是充放电设备本身已经具备CAN Bootloader功能，上位机负责升级流程的控制、固件分发和状态监控。

4.6.1 固件升级接口
yaml
# interfaces/firmware_upgrader.yaml
name: firmware_upgrader
description: "固件自动升级接口—通过CAN总线实现设备固件远程升级"

cmds:
  start_upgrade:
    description: "启动固件升级流程"
    arguments:
      device_id:
        description: "目标设备ID"
        type: string
      firmware_file:
        description: "固件文件路径"
        type: string
      firmware_version:
        description: "固件版本号"
        type: string
      force_upgrade:
        description: "是否强制升级（跳过版本检查）"
        type: boolean
        default: false
      verify_after_upgrade:
        description: "升级后是否校验固件完整性"
        type: boolean
        default: true
    result:
      description: "升级任务信息（含task_id）"
      type: object

  get_upgrade_status:
    description: "查询升级进度"
    arguments:
      task_id:
        description: "升级任务ID"
        type: string
    result:
      description: "升级状态详情"
      type: object

  cancel_upgrade:
    description: "取消正在进行的升级"
    arguments:
      task_id:
        description: "升级任务ID"
        type: string
    result:
      description: "取消结果"
      type: object

  get_firmware_version:
    description: "查询设备当前固件版本"
    arguments:
      device_id:
        description: "设备ID"
        type: string
    result:
      description: "固件版本信息"
      type: object

  rollback_firmware:
    description: "回滚到上一版本固件"
    arguments:
      device_id:
        description: "设备ID"
        type: string
    result:
      description: "回滚结果"
      type: object

vars:
  active_upgrade_tasks:
    description: "当前活跃的升级任务列表"
    type: array
    access: read_only
  upgrade_history:
    description: "升级历史记录"
    type: array
    access: read_only
4.6.2 固件升级流程
text
升级流程状态机:

  IDLE ──[start_upgrade]──> CHECK_VERSION
                               │
                    ┌──────────┴──────────┐
                    │ 版本相同且非强制    │ 版本不同/强制
                    ▼                     ▼
                  IDLE              ENTER_BOOTLOADER
                                         │
                                    ┌────┴────┐
                                    │ 超时30s  │ 成功
                                    ▼         ▼
                                  FAILED  ERASE_FLASH
                                               │
                                          ┌────┴────┐
                                          │ 失败     │ 成功
                                          ▼         ▼
                                        FAILED  TRANSFER_DATA
                                                     │
                                                ┌────┴────┐
                                                │ 重试3次  │ 完成
                                                │ 均失败   │
                                                ▼         ▼
                                              FAILED  VERIFY_FW
                                                           │
                                                      ┌────┴────┐
                                                      │ 校验失败 │ 校验成功
                                                      ▼         ▼
                                                    FAILED  REBOOT_DEVICE
                                                                 │
                                                            ┌────┴────┐
                                                            │ 超时60s  │ 成功
                                                            ▼         ▼
                                                          FAILED  COMPLETED
4.6.3 固件升级CAN协议定义
cpp
// 固件升级CAN协议命令码
enum class FirmwareUpgradeCommand : uint8_t {
    // 上位机 → 设备
    ENTER_BOOTLOADER    = 0x10,   // 进入Bootloader模式
    ERASE_FLASH         = 0x11,   // 擦除Flash区域
    TRANSFER_START      = 0x12,   // 开始传输固件
    TRANSFER_DATA       = 0x13,   // 传输固件数据块
    TRANSFER_END        = 0x14,   // 结束传输
    VERIFY_FIRMWARE     = 0x15,   // 校验固件完整性
    REBOOT_DEVICE       = 0x16,   // 重启设备到应用程序
    GET_VERSION         = 0x17,   // 查询固件版本
    
    // 设备 → 上位机（响应）
    ACK                 = 0x80,   // 命令执行成功
    NACK                = 0x81,   // 命令执行失败
    DATA_ACK            = 0x82,   // 数据块接收成功
    DATA_NACK           = 0x83,   // 数据块接收失败
    CHECKSUM_ERROR      = 0x84,   // 校验和错误
    FLASH_ERROR         = 0x85,   // Flash操作错误
};

// 固件升级数据结构
struct FirmwareTransferBlock {
    uint32_t block_index;       // 数据块索引（从0开始）
    uint32_t total_blocks;      // 总数据块数
    uint32_t block_size;        // 当前数据块大小（最后一包可能小于标准大小）
    uint32_t offset_address;    // Flash写入地址
    std::array<uint8_t, 64> data; // 数据内容（每包最大64字节CAN FD或8字节CAN2.0）
    uint16_t checksum;          // CRC16校验
};

// 数据块大小根据CAN模式切换
static constexpr size_t CAN20_BLOCK_SIZE = 6;    // CAN 2.0B: 6字节有效数据/帧
static constexpr size_t CANFD_BLOCK_SIZE = 62;   // CAN FD: 62字节有效数据/帧
4.7 故障录波模块 (FaultRecorder)
模块ID：0x0203

功能描述：当设备检测到故障时，自动触发高速数据录波，记录故障前后一段时间内的关键参数波形数据，用于故障分析和诊断。

前提条件：此功能的实现前提是充放电设备本身已经具备故障录波功能，可通过CAN总线输出录波数据。

4.7.1 故障录波接口
yaml
# interfaces/fault_recorder.yaml
name: fault_recorder
description: "故障录波接口—故障触发条件下高速数据波形记录与回放"

cmds:
  enable_fault_recording:
    description: "使能故障录波功能"
    arguments:
      device_id:
        description: "设备ID"
        type: string
      trigger_conditions:
        description: "触发条件配置"
        type: object
        properties:
          voltage_threshold:
            description: "电压触发阈值"
            type: number
          current_threshold:
            description: "电流触发阈值"
            type: number
          temperature_threshold:
            description: "温度触发阈值"
            type: number
          fault_codes:
            description: "触发录波的故障码列表"
            type: array
    result:
      description: "使能结果"
      type: object

  get_waveform_data:
    description: "获取录波数据"
    arguments:
      event_id:
        description: "故障事件ID"
        type: string
    result:
      description: "录波数据（含波形数据点数组）"
      type: object

  list_fault_events:
    description: "查询故障事件列表"
    arguments:
      device_id:
        description: "设备ID（可选）"
        type: string
        required: false
      start_time:
        description: "开始时间"
        type: string
      end_time:
        description: "结束时间"
        type: string
      fault_level:
        description: "故障等级过滤（可选）"
        type: string
        required: false
    result:
      description: "故障事件列表"
      type: array

  export_waveform:
    description: "导出录波数据为CSV/JSON文件"
    arguments:
      event_id:
        description: "故障事件ID"
        type: string
      format:
        description: "导出格式: csv/json"
        type: string
        default: "csv"
    result:
      description: "导出文件路径"
      type: object

vars:
  fault_recording_enabled:
    description: "故障录波使能状态"
    type: boolean
    access: read_only
  recent_fault_events:
    description: "最近故障事件摘要"
    type: array
    access: read_only
4.7.2 录波参数配置
yaml
# 录波参数配置（在模块config_module中定义）
fault_recording:
  # 录波缓冲区配置
  pre_fault_samples_high: 5000        # 高采样率下故障前样点数
  post_fault_samples_high: 10000      # 高采样率下故障后样点数
  pre_fault_samples_normal: 1000      # 正常采样率下故障前样点数
  post_fault_samples_normal: 2000     # 正常采样率下故障后样点数
  
  # 录波通道配置
  channels:
    - name: "dc_voltage"
      high_speed: true                # 高采样率通道（1kHz）
      normal_rate_hz: 10              # 正常采样率
      high_rate_hz: 1000              # 故障触发时采样率
      unit: "V"
    - name: "dc_current"
      high_speed: true
      normal_rate_hz: 10
      high_rate_hz: 1000
      unit: "A"
    - name: "ac_voltage_phase_a"
      high_speed: true
      normal_rate_hz: 10
      high_rate_hz: 1000
      unit: "V"
    - name: "igbt_temperature"
      high_speed: false               # 普通采样率通道
      normal_rate_hz: 1
      high_rate_hz: 10
      unit: "℃"
  
  # 存储配置
  max_waveform_storage_mb: 10240      # 录波数据最大存储空间(10GB)
  auto_export_enabled: true            # 是否自动导出
  auto_export_format: "csv"           # 自动导出格式
4.7.3 录波数据处理
cpp
// 故障录波数据缓冲区 — 循环缓冲区设计
template<typename T, size_t Capacity>
class CircularBuffer {
private:
    std::array<T, Capacity> buffer_;
    size_t head_{0};    // 写入位置
    size_t size_{0};    // 当前有效元素数
    
public:
    void push(const T& item) {
        buffer_[head_] = item;
        head_ = (head_ + 1) % Capacity;
        if (size_ < Capacity) size_++;
    }
    
    // 获取故障前N个样点（从head向前回溯）
    std::vector<T> getPreFaultSamples(size_t count) const {
        std::vector<T> result;
        size_t actual_count = std::min(count, size_);
        size_t start_index = (head_ + Capacity - actual_count) % Capacity;
        for (size_t i = 0; i < actual_count; i++) {
            result.push_back(buffer_[(start_index + i) % Capacity]);
        }
        return result;
    }
};

// 录波事件结构体
struct FaultWaveformEvent {
    std::string event_id;           // UUID格式事件ID
    std::string device_id;          // 故障设备ID
    std::string fault_code;         // 故障码
    std::string fault_level;        // 故障等级
    std::chrono::system_clock::time_point trigger_time;
    
    // 波形数据
    struct WaveformData {
        std::vector<double> timestamps;              // 时间轴（毫秒，相对触发时刻）
        std::map<std::string, std::vector<double>> channels; // 各通道数据
        size_t pre_fault_samples;
        size_t post_fault_samples;
        double sample_rate_hz;
    } waveform;
    
    // 元数据
    std::map<std::string, double> trigger_values;   // 触发时刻各通道瞬时值
    std::string notes;                               // 备注
};

// 录波触发处理
void FaultRecorder::handleFaultTrigger(const std::string& device_id,
                                         const std::string& fault_code) {
    FaultWaveformEvent event;
    event.event_id = generateUUID();
    event.device_id = device_id;
    event.fault_code = fault_code;
    event.trigger_time = std::chrono::system_clock::now();
    
    // 从循环缓冲区获取故障前数据
    auto pre_data = circular_buffer_.getPreFaultSamples(pre_fault_samples_high_);
    
    // 开始高速采集故障后数据
    std::vector<MeasurementRecord> post_data;
    for (size_t i = 0; i < post_fault_samples_high_; i++) {
        post_data.push_back(readHighSpeedData(device_id));
        std::this_thread::sleep_for(std::chrono::microseconds(1000)); // 1kHz采样
    }
    
    // 组装波形数据
    event.waveform.pre_fault_samples = pre_data.size();
    event.waveform.post_fault_samples = post_data.size();
    event.waveform.sample_rate_hz = 1000.0;
    
    // 持久化到数据库
    saveWaveformToDatabase(event);
    
    publish_var("fault_recorder", "new_fault_event", event);
}
5. SQLite数据库设计
5.1 数据库初始化
所有SQLite数据库初始化操作通过一个统一的shell脚本执行：

bash
#!/bin/bash
# scripts/init_database.sh — 一键创建所有数据库表
# 测试人员仅需执行此脚本即可完成数据库初始化

DB_PATH="${1:-./data/mw_scada.db}"
DB_DIR=$(dirname "$DB_PATH")

mkdir -p "$DB_DIR"

sqlite3 "$DB_PATH" <<'SQL'
-- ============================================================
-- MW级充放电系统上位机数据库 Schema
-- 基于SQLite 3.x，参考时序数据优化最佳实践
-- ============================================================

-- 全局PRAGMA设置
PRAGMA journal_mode = WAL;                 -- WAL模式：提高读写并发，参考社区建议
PRAGMA synchronous = NORMAL;               -- 平衡性能与安全性——正常运行模式
PRAGMA foreign_keys = ON;                  -- 启用外键约束
PRAGMA cache_size = -65536;               -- 64MB缓存
PRAGMA page_size = 4096;                   -- 4KB页面
PRAGMA temp_store = MEMORY;                -- 临时表存储在内存中

-- ============================================================
-- 1. 用户与权限管理
-- ============================================================
CREATE TABLE IF NOT EXISTS user_account (
    user_id TEXT PRIMARY KEY,                  -- 用户ID（UUID）
    username TEXT NOT NULL UNIQUE,             -- 用户名
    password_hash TEXT NOT NULL,               -- 密码哈希（SHA-256 + Salt）
    salt TEXT NOT NULL,                        -- 密码盐值
    display_name TEXT,                         -- 显示名称
    email TEXT,                                -- 邮箱
    phone TEXT,                                -- 电话
    role_id TEXT NOT NULL,                     -- 角色ID
    status TEXT DEFAULT 'active',              -- active / disabled / locked
    last_login_time TEXT,
    login_fail_count INTEGER DEFAULT 0,
    created_at TEXT DEFAULT (datetime('now')),
    updated_at TEXT DEFAULT (datetime('now'))
);

CREATE TABLE IF NOT EXISTS user_role (
    role_id TEXT PRIMARY KEY,
    role_name TEXT NOT NULL UNIQUE,            -- admin / operator / engineer / viewer
    description TEXT,
    created_at TEXT DEFAULT (datetime('now'))
);

CREATE TABLE IF NOT EXISTS permission (
    permission_id TEXT PRIMARY KEY,
    permission_name TEXT NOT NULL UNIQUE,      -- 如: device:read / calibration:execute
    resource TEXT NOT NULL,                    -- 资源类型: device/calibration/firmware
    action TEXT NOT NULL,                      -- 操作: read/write/execute/delete
    description TEXT
);

CREATE TABLE IF NOT EXISTS role_permission (
    role_id TEXT NOT NULL,
    permission_id TEXT NOT NULL,
    PRIMARY KEY (role_id, permission_id),
    FOREIGN KEY (role_id) REFERENCES user_role(role_id),
    FOREIGN KEY (permission_id) REFERENCES permission(permission_id)
);

CREATE TABLE IF NOT EXISTS user_session (
    session_id TEXT PRIMARY KEY,
    user_id TEXT NOT NULL,
    login_time TEXT DEFAULT (datetime('now')),
    logout_time TEXT,
    ip_address TEXT,
    token_hash TEXT,
    is_active INTEGER DEFAULT 1,
    FOREIGN KEY (user_id) REFERENCES user_account(user_id)
);

-- ============================================================
-- 2. 设备管理
-- ============================================================
CREATE TABLE IF NOT EXISTS device_info (
    device_id TEXT PRIMARY KEY,                -- 设备唯一标识
    device_type TEXT NOT NULL,                 -- PCS / BMS / INVERTER / METER / CHARGER
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
    status TEXT DEFAULT 'offline',             -- online / offline / fault / maintenance
    registered_at TEXT DEFAULT (datetime('now')),
    last_online_time TEXT,
    notes TEXT                                 -- 备注
);

CREATE TABLE IF NOT EXISTS device_config (
    config_id TEXT PRIMARY KEY,
    device_id TEXT NOT NULL,
    config_name TEXT NOT NULL,                 -- 配置项名称
    config_value TEXT,                         -- 配置值（JSON格式）
    config_type TEXT,                          -- 配置类型: string/number/boolean/json
    description TEXT,
    updated_by TEXT,                           -- 修改人
    updated_at TEXT DEFAULT (datetime('now')),
    FOREIGN KEY (device_id) REFERENCES device_info(device_id),
    FOREIGN KEY (updated_by) REFERENCES user_account(user_id)
);

CREATE TABLE IF NOT EXISTS device_status (
    record_id INTEGER PRIMARY KEY AUTOINCREMENT,
    device_id TEXT NOT NULL,
    parameter_name TEXT NOT NULL,              -- 参数名称: voltage/current/temperature/soc
    parameter_value REAL,                      -- 参数值
    unit TEXT,                                 -- 单位
    quality TEXT DEFAULT 'GOOD',               -- GOOD / UNCERTAIN / BAD
    timestamp INTEGER NOT NULL,                -- Unix毫秒时间戳，参考时序优化建议
    FOREIGN KEY (device_id) REFERENCES device_info(device_id)
);

CREATE INDEX idx_device_status_device_time 
    ON device_status(device_id, timestamp);

CREATE TABLE IF NOT EXISTS device_alarm (
    alarm_id TEXT PRIMARY KEY,
    device_id TEXT NOT NULL,
    alarm_code TEXT NOT NULL,                  -- 告警编码
    alarm_level TEXT NOT NULL,                 -- CRITICAL / MAJOR / MINOR / WARNING
    alarm_message TEXT,                        -- 告警描述
    parameter_name TEXT,                       -- 触发告警的参数名
    threshold_value REAL,                      -- 阈值
    actual_value REAL,                         -- 实际值
    is_active INTEGER DEFAULT 1,               -- 是否仍活跃
    raised_at INTEGER NOT NULL,                -- 告警触发时间
    acknowledged_at INTEGER,                   -- 确认时间
    acknowledged_by TEXT,                      -- 确认人
    cleared_at INTEGER,                        -- 清除时间
    FOREIGN KEY (device_id) REFERENCES device_info(device_id),
    FOREIGN KEY (acknowledged_by) REFERENCES user_account(user_id)
);

CREATE INDEX idx_device_alarm_device_time 
    ON device_alarm(device_id, raised_at);

-- ============================================================
-- 3. 充放电会话
-- ============================================================
CREATE TABLE IF NOT EXISTS charge_session (
    session_id TEXT PRIMARY KEY,
    device_id TEXT NOT NULL,
    battery_protocol TEXT,                     -- 电池协议名称
    session_type TEXT NOT NULL,                -- charge / discharge / cycle / formation
    status TEXT DEFAULT 'running',             -- running / paused / completed / aborted / fault
    start_time INTEGER NOT NULL,
    end_time INTEGER,
    
    -- 设定参数
    target_voltage_v REAL,                     -- 目标电压
    target_current_a REAL,                     -- 目标电流
    target_power_kw REAL,                      -- 目标功率
    target_soc_percent REAL,                   -- 目标SOC
    target_duration_s INTEGER,                 -- 目标时长
    cutoff_voltage_v REAL,                     -- 截止电压
    
    -- 累计数据
    total_energy_kwh REAL DEFAULT 0,           -- 累计能量
    total_charge_ah REAL DEFAULT 0,            -- 累计安时
    max_voltage_v REAL,                        -- 最高电压
    min_voltage_v REAL,                        -- 最低电压
    max_current_a REAL,                        -- 最大电流
    max_temperature_c REAL,                    -- 最高温度
    
    created_by TEXT,                           -- 创建人
    notes TEXT,                                -- 备注
    FOREIGN KEY (device_id) REFERENCES device_info(device_id),
    FOREIGN KEY (created_by) REFERENCES user_account(user_id)
);

CREATE INDEX idx_charge_session_time 
    ON charge_session(start_time, end_time);

-- ============================================================
-- 4. 测量数据 — 核心时序数据表
-- 参考SQLite论坛最佳实践：毫秒级INTEGER时间戳，减少存储空间
-- ============================================================
CREATE TABLE IF NOT EXISTS measurement_data (
    record_id INTEGER PRIMARY KEY AUTOINCREMENT,
    timestamp INTEGER NOT NULL,                -- Unix毫秒时间戳
    device_id TEXT NOT NULL,
    session_id TEXT,
    parameter_name TEXT NOT NULL,              -- voltage / current / power / temperature / soc
    value REAL NOT NULL,
    unit TEXT,
    quality TEXT DEFAULT 'GOOD',
    FOREIGN KEY (device_id) REFERENCES device_info(device_id),
    FOREIGN KEY (session_id) REFERENCES charge_session(session_id)
);

-- 核心查询索引：时间范围查询 + 设备过滤
CREATE INDEX idx_meas_device_time 
    ON measurement_data(device_id, timestamp);
CREATE INDEX idx_meas_session_time 
    ON measurement_data(session_id, timestamp);
CREATE INDEX idx_meas_param_time 
    ON measurement_data(parameter_name, timestamp);

-- ============================================================
-- 5. 校准数据
-- ============================================================
CREATE TABLE IF NOT EXISTS calibration_record (
    calibration_id TEXT PRIMARY KEY,
    device_id TEXT NOT NULL,
    calibration_type TEXT NOT NULL,            -- ZERO / SPAN / LINEAR / VI_CURRENT / VI_VOLTAGE
    calibration_status TEXT NOT NULL,          -- in_progress / completed / failed
    started_at INTEGER NOT NULL,
    completed_at INTEGER,
    performed_by TEXT,                         -- 执行人
    approved_by TEXT,                          -- 批准人
    is_valid INTEGER DEFAULT 1,                -- 是否有效
    valid_until INTEGER,                       -- 有效期至
    notes TEXT,
    FOREIGN KEY (device_id) REFERENCES device_info(device_id),
    FOREIGN KEY (performed_by) REFERENCES user_account(user_id),
    FOREIGN KEY (approved_by) REFERENCES user_account(user_id)
);

CREATE INDEX idx_calibration_device 
    ON calibration_record(device_id, started_at);

CREATE TABLE IF NOT EXISTS calibration_point (
    point_id INTEGER PRIMARY KEY AUTOINCREMENT,
    calibration_id TEXT NOT NULL,
    parameter_name TEXT NOT NULL,              -- voltage / current / power
    point_index INTEGER NOT NULL,              -- 校准点序号
    reference_value REAL NOT NULL,             -- 标准参考值（从高精度仪表读取）
    measured_value REAL NOT NULL,              -- 设备测量值（校准前）
    corrected_value REAL,                      -- 校准后值
    deviation_percent REAL,                    -- 偏差百分比
    is_pass INTEGER,                           -- 该点是否合格
    FOREIGN KEY (calibration_id) REFERENCES calibration_record(calibration_id)
);

-- ============================================================
-- 6. 协议管理
-- ============================================================
CREATE TABLE IF NOT EXISTS protocol_config (
    protocol_id TEXT PRIMARY KEY,
    protocol_name TEXT NOT NULL UNIQUE,
    protocol_type TEXT NOT NULL,               -- device / battery
    protocol_version TEXT,
    config_content TEXT NOT NULL,              -- JSON格式的协议定义
    is_active INTEGER DEFAULT 1,
    loaded_at TEXT DEFAULT (datetime('now')),
    updated_at TEXT
);

CREATE TABLE IF NOT EXISTS protocol_command (
    command_id TEXT PRIMARY KEY,
    protocol_id TEXT NOT NULL,
    command_name TEXT NOT NULL,
    command_code INTEGER NOT NULL,             -- CAN命令码
    request_params TEXT,                       -- 请求参数定义（JSON）
    response_params TEXT,                      -- 响应参数定义（JSON）
    timeout_ms INTEGER DEFAULT 1000,
    retry_count INTEGER DEFAULT 3,
    description TEXT,
    FOREIGN KEY (protocol_id) REFERENCES protocol_config(protocol_id)
);

CREATE TABLE IF NOT EXISTS battery_protocol_config (
    config_id TEXT PRIMARY KEY,
    protocol_name TEXT NOT NULL,
    bms_vendor TEXT NOT NULL,                  -- BMS供应商
    protocol_version TEXT,
    pgn_definition TEXT NOT NULL,              -- PGN定义（JSON）
    fault_code_map TEXT,                       -- 故障码映射（JSON）
    is_active INTEGER DEFAULT 1,
    created_at TEXT DEFAULT (datetime('now'))
);

-- ============================================================
-- 7. 固件升级
-- ============================================================
CREATE TABLE IF NOT EXISTS firmware_version (
    version_id TEXT PRIMARY KEY,
    device_id TEXT NOT NULL,
    firmware_version TEXT NOT NULL,
    firmware_file_path TEXT,                   -- 固件文件存储路径
    file_size_bytes INTEGER,
    checksum TEXT,                              -- SHA-256校验和
    release_notes TEXT,
    released_at TEXT DEFAULT (datetime('now')),
    FOREIGN KEY (device_id) REFERENCES device_info(device_id)
);

CREATE TABLE IF NOT EXISTS upgrade_task (
    task_id TEXT PRIMARY KEY,
    device_id TEXT NOT NULL,
    from_version TEXT NOT NULL,
    to_version TEXT NOT NULL,
    firmware_file TEXT NOT NULL,
    status TEXT NOT NULL,                       -- pending / in_progress / completed / failed / cancelled
    progress_percent REAL DEFAULT 0,
    started_at INTEGER,
    completed_at INTEGER,
    initiated_by TEXT,                         -- 发起人
    error_message TEXT,                        -- 失败原因
    FOREIGN KEY (device_id) REFERENCES device_info(device_id),
    FOREIGN KEY (initiated_by) REFERENCES user_account(user_id)
);

-- ============================================================
-- 8. 故障录波
-- ============================================================
CREATE TABLE IF NOT EXISTS fault_event (
    event_id TEXT PRIMARY KEY,
    device_id TEXT NOT NULL,
    session_id TEXT,
    fault_code TEXT NOT NULL,
    fault_level TEXT NOT NULL,                 -- CRITICAL / MAJOR / MINOR
    fault_description TEXT,
    triggered_at INTEGER NOT NULL,             -- Unix毫秒时间戳
    trigger_channel TEXT,                      -- 触发通道名称
    trigger_value REAL,                        -- 触发值
    pre_fault_samples INTEGER,                 -- 故障前样点数
    post_fault_samples INTEGER,                -- 故障后样点数
    sample_rate_hz REAL,                      -- 采样率
    waveform_data_path TEXT,                   -- 波形数据文件路径(大Blob存文件)
    is_exported INTEGER DEFAULT 0,
    analyzed_by TEXT,                          -- 分析人
    analysis_notes TEXT,                       -- 分析备注
    FOREIGN KEY (device_id) REFERENCES device_info(device_id),
    FOREIGN KEY (session_id) REFERENCES charge_session(session_id)
);

CREATE INDEX idx_fault_event_device 
    ON fault_event(device_id, triggered_at);

CREATE TABLE IF NOT EXISTS fault_waveform (
    waveform_id TEXT PRIMARY KEY,
    event_id TEXT NOT NULL,
    channel_index INTEGER NOT NULL,
    channel_name TEXT NOT NULL,
    data_blob BLOB,                            -- 波形数据（压缩存储）
    data_size INTEGER,                         -- 数据点数量
    unit TEXT,
    FOREIGN KEY (event_id) REFERENCES fault_event(event_id)
);

-- ============================================================
-- 9. 测试报告
-- ============================================================
CREATE TABLE IF NOT EXISTS test_report (
    report_id TEXT PRIMARY KEY,
    report_type TEXT NOT NULL,                 -- charge_test / discharge_test / cycle_test / calibration
    title TEXT NOT NULL,
    device_id TEXT,
    session_id TEXT,
    generated_by TEXT,                         -- 生成人
    generated_at TEXT DEFAULT (datetime('now')),
    report_format TEXT DEFAULT 'pdf',          -- pdf / html / csv
    report_file_path TEXT,                     -- 报告文件路径
    report_data TEXT,                          -- JSON格式的报告数据
    status TEXT DEFAULT 'draft',               -- draft / published / archived
    FOREIGN KEY (device_id) REFERENCES device_info(device_id),
    FOREIGN KEY (session_id) REFERENCES charge_session(session_id),
    FOREIGN KEY (generated_by) REFERENCES user_account(user_id)
);

CREATE TABLE IF NOT EXISTS report_template (
    template_id TEXT PRIMARY KEY,
    template_name TEXT NOT NULL,
    report_type TEXT NOT NULL,
    template_content TEXT NOT NULL,            -- Jinja2模板内容
    is_default INTEGER DEFAULT 0,
    created_by TEXT,
    updated_at TEXT DEFAULT (datetime('now')),
    FOREIGN KEY (created_by) REFERENCES user_account(user_id)
);

-- ============================================================
-- 10. 系统日志
-- ============================================================
CREATE TABLE IF NOT EXISTS system_log (
    log_id INTEGER PRIMARY KEY AUTOINCREMENT,
    timestamp INTEGER NOT NULL,
    log_level TEXT NOT NULL,                    -- DEBUG / INFO / WARNING / ERROR / CRITICAL
    module_name TEXT NOT NULL,                  -- 来源模块
    message TEXT NOT NULL,
    details TEXT,                               -- 详细信息（JSON）
    user_id TEXT,
    device_id TEXT
);

CREATE INDEX idx_system_log_level_time 
    ON system_log(log_level, timestamp);
CREATE INDEX idx_system_log_module_time 
    ON system_log(module_name, timestamp);

-- ============================================================
-- 完成初始化
-- ============================================================
VACUUM;
ANALYZE;
SQL

echo "✅ 数据库初始化完成: $DB_PATH"
echo "   - 数据库文件: $(du -h "$DB_PATH" | cut -f1)"
echo "   - 共创建 $(sqlite3 "$DB_PATH" "SELECT COUNT(*) FROM sqlite_master WHERE type='table';") 张表"
5.2 数据库性能优化要点
优化项	配置	说明
WAL模式	PRAGMA journal_mode = WAL;	支持读写并发，写操作不阻塞读操作，适合持续写入场景
同步级别	PRAGMA synchronous = NORMAL;	平衡性能与安全性，在正常运行模式下数据足够安全
缓存大小	PRAGMA cache_size = -65536;	64MB缓存，确保单次事务的所有页面在缓存中
批量写入	BEGIN...COMMIT 批量提交	减少I/O操作次数，每500-1000条数据一个事务
时间戳类型	INTEGER（Unix毫秒时间戳）	避免TEXT格式的20字节开销，减少约50%存储空间
索引策略	(device_id, timestamp) 复合索引	覆盖最常见的查询模式——按设备和时间范围查询
数据保留	定时归档删除	高分辨率数据保留30天，聚合数据保留更久
5.3 配置驱动数据库路径
在config/config.yaml中配置数据库路径，测试人员可根据部署环境修改：

yaml
# config/config.yaml 中添加
sqlite:
  database_path: "data/mw_scada.db"              # 数据库文件路径
  backup_path: "data/backup/"                     # 备份路径
  init_script: "scripts/init_database.sh"         # 初始化脚本
  auto_vacuum: false                              # 自动VACUUM
  max_database_size_gb: 100                        # 数据库最大大小（告警阈值）
6. 用户管理与权限控制
模块ID：0x0301

功能描述：实现基于角色的访问控制（RBAC），管理用户账户、角色和操作权限，确保系统操作的安全性和可审计性。

模块位于Tier 3应用服务层，不依赖ZLG CAN驱动，运行在MQTT上层。EVerest框架的Auth模块提供了一套通用的授权模式：可配置多个授权提供者和多个授权验证者。本模块参考该模式实现，同时扩展基于用户角色的操作权限管理模块，并自行管理身份认证逻辑。

6.1 用户管理接口
yaml
# interfaces/user_manager.yaml
name: user_manager
description: "用户管理接口—基于角色的访问控制(RBAC)"

cmds:
  authenticate:
    description: "用户登录认证"
    arguments:
      username:
        description: "用户名"
        type: string
      password:
        description: "密码"
        type: string
    result:
      description: "认证结果（含会话令牌）"
      type: object

  create_user:
    description: "创建新用户（管理员权限）"
    arguments:
      username:
        description: "用户名"
        type: string
      password:
        description: "初始密码"
        type: string
      role_id:
        description: "角色ID"
        type: string
      display_name:
        description: "显示名称"
        type: string
        required: false
      email:
        description: "邮箱"
        type: string
        required: false
    result:
      description: "创建结果"
      type: object

  update_user:
    description: "更新用户信息"
    arguments:
      user_id:
        description: "用户ID"
        type: string
      updates:
        description: "要更新的字段"
        type: object
    result:
      description: "更新结果"
      type: object

  delete_user:
    description: "删除用户"
    arguments:
      user_id:
        description: "用户ID"
        type: string
    result:
      description: "删除结果"
      type: object

  check_permission:
    description: "检查用户是否具有指定权限"
    arguments:
      user_id:
        description: "用户ID"
        type: string
      permission_name:
        description: "权限名称"
        type: string
    result:
      description: "权限检查结果"
      type: object

  list_roles:
    description: "列出所有角色"
    result:
      description: "角色列表"
      type: array

vars:
  active_users:
    description: "当前在线用户数"
    type: integer
    access: read_only
  total_users:
    description: "系统总用户数"
    type: integer
    access: read_only
6.2 角色与权限定义
yaml
# config/users.yaml
# 测试人员可修改此文件配置角色和用户

roles:
  - role_id: "ROLE_ADMIN"
    role_name: "admin"
    description: "系统管理员—拥有所有权限"
    permissions:
      - "user:create"
      - "user:update"
      - "user:delete"
      - "device:register"
      - "device:configure"
      - "calibration:execute"
      - "calibration:approve"
      - "protocol:manage"
      - "firmware:upgrade"
      - "fault:analyze"
      - "report:generate"
      - "system:configure"
      - "data:export"
      - "data:delete"

  - role_id: "ROLE_ENGINEER"
    role_name: "engineer"
    description: "工程师—可执行校准和升级操作"
    permissions:
      - "device:register"
      - "device:configure"
      - "calibration:execute"
      - "protocol:manage"
      - "firmware:upgrade"
      - "fault:analyze"
      - "report:generate"
      - "data:export"

  - role_id: "ROLE_OPERATOR"
    role_name: "operator"
    description: "操作员—日常监控和操作"
    permissions:
      - "device:read"
      - "report:generate"
      - "data:export"

  - role_id: "ROLE_VIEWER"
    role_name: "viewer"
    description: "查看者—只读权限"
    permissions:
      - "device:read"

# 预置用户（首次部署时自动创建，首次登录后需修改密码）
default_users:
  - username: "admin"
    password: "Admin@123456"           # 首次部署后必须修改
    role: "ROLE_ADMIN"
    display_name: "系统管理员"
  
  - username: "engineer"
    password: "Engineer@123456"
    role: "ROLE_ENGINEER"
    display_name: "维护工程师"
6.3 权限的资源-操作矩阵
资源	admin	engineer	operator	viewer
device（设备）	CRUD	CRU	R	R
calibration（校准）	执行+批准	执行	-	-
protocol（协议）	CRUD	CRU	-	-
firmware（固件）	升级+回滚	升级	-	-
fault（故障）	CRUD+分析	R+分析	R	R
report（报告）	CRUD	CRUD	CR	R
user（用户）	CRUD	-	-	-
data（数据）	CRUD+导出	CR+导出	R+导出	R
system（系统）	完全配置	-	-	-
C=创建, R=读取, U=更新, D=删除

7. 测试报告生成模块
模块ID：0x0302

功能描述：基于充放电测试数据自动生成标准化的测试报告，支持PDF和HTML格式输出。

7.1 报告生成接口
yaml
# interfaces/report_generator.yaml
name: report_generator
description: "测试报告生成接口"

cmds:
  generate_report:
    description: "生成测试报告"
    arguments:
      report_type:
        description: "报告类型"
        type: string
        enum:
          - "charge_test"          # 充电测试报告
          - "discharge_test"       # 放电测试报告
          - "cycle_test"           # 循环测试报告
          - "formation_test"       # 化成测试报告
          - "calibration_report"   # 校准报告
          - "fault_analysis"       # 故障分析报告
          - "daily_summary"        # 日报
          - "monthly_summary"      # 月报
      session_id:
        description: "关联的充放电会话ID（可选）"
        type: string
        required: false
      device_id:
        description: "关联设备ID（可选）"
        type: string
        required: false
      parameters:
        description: "报告参数"
        type: object
        required: false
        properties:
          start_time: { type: string }
          end_time: { type: string }
          include_waveforms: { type: boolean, default: false }
          include_calibration_data: { type: boolean, default: false }
    result:
      description: "报告生成结果"
      type: object

  get_report_list:
    description: "获取已有报告列表"
    arguments:
      report_type:
        description: "报告类型过滤（可选）"
        type: string
        required: false
      start_date:
        description: "开始日期"
        type: string
        required: false
      end_date:
        description: "结束日期"
        type: string
        required: false
    result:
      description: "报告列表"
      type: array

  download_report:
    description: "下载报告文件"
    arguments:
      report_id:
        description: "报告ID"
        type: string
    result:
      description: "报告文件路径或Base64内容"
      type: object

  schedule_report:
    description: "设置定时报告生成"
    arguments:
      report_type:
        description: "报告类型"
        type: string
      schedule:
        description: "Cron表达式"
        type: string
      parameters:
        description: "报告参数"
        type: object
    result:
      description: "定时任务ID"
      type: object

vars:
  report_queue_size:
    description: "报告生成队列大小"
    type: integer
    access: read_only
7.2 报告模板配置
yaml
# config/report_templates.yaml
report_templates:
  charge_test:
    title: "充电测试报告"
    sections:
      - title: "测试概要"
        fields:
          - { label: "设备名称", source: "device_name" }
          - { label: "测试时间", source: "test_time" }
          - { label: "电池类型", source: "battery_info" }
          - { label: "环境温度", source: "ambient_temperature", unit: "℃" }
      
      - title: "充电参数设定"
        fields:
          - { label: "充电模式", source: "charge_mode" }
          - { label: "目标电压", source: "target_voltage", unit: "V" }
          - { label: "目标电流", source: "target_current", unit: "A" }
          - { label: "截止条件", source: "cutoff_condition" }
      
      - title: "充电过程数据"
        fields:
          - { label: "充电时长", source: "charge_duration" }
          - { label: "充电电量", source: "total_energy", unit: "kWh" }
          - { label: "充电容量", source: "total_charge", unit: "Ah" }
          - { label: "最高单体电压", source: "max_cell_voltage", unit: "V" }
          - { label: "最高温度", source: "max_temperature", unit: "℃" }
      
      - title: "充电曲线"
        type: "chart"
        chart_type: "line"
        data_source: "measurement_data"
        parameters: ["voltage", "current", "soc", "temperature"]
        time_range: "session"
      
      - title: "异常事件"
        type: "event_list"
        data_source: "alarm_data"
      
      - title: "测试结论"
        type: "conclusion"
        auto_generate: true
  
  calibration_report:
    title: "设备校准报告"
    sections:
      - title: "校准信息"
        fields:
          - { label: "设备名称", source: "device_name" }
          - { label: "校准类型", source: "calibration_type" }
          - { label: "校准日期", source: "calibration_date" }
          - { label: "执行人", source: "performed_by" }
          - { label: "参考标准", source: "reference_standard" }
      
      - title: "校准结果"
        type: "calibration_table"
        columns:
          - { header: "校准点", field: "point_index" }
          - { header: "标准值", field: "reference_value" }
          - { header: "测量值", field: "measured_value" }
          - { header: "偏差(%)", field: "deviation_percent" }
          - { header: "结论", field: "is_pass" }
      
      - title: "校准结论"
        type: "conclusion"
        auto_generate: true

report_settings:
  company_name: "公司名称"                  # 测试人员修改为公司实际名称
  company_logo_path: "assets/logo.png"      # 公司Logo路径
  report_output_path: "data/reports/"       # 报告输出路径
  default_format: "pdf"                      # 默认输出格式
  pdf_engine: "weasyprint"                   # PDF生成引擎
8. 系统集成与配置指南
8.1 周立功CAN卡集成
8.1.1 Linux SocketCAN驱动方案
在Linux环境下，周立功CAN卡可通过SocketCAN驱动与python-can库集成：

bash
# 1. 安装周立功CAN卡Linux驱动
# 驱动支持Linux内核4.4及以上版本，支持CAN FD协议
sudo apt install -y can-utils
sudo modprobe can
sudo modprobe can_raw
sudo modprobe can_dev

# 2. 配置CAN接口
sudo ip link set can0 type can bitrate 500000
sudo ip link set up can0

# 3. 验证CAN通信
candump can0       # 查看CAN总线数据
cansend can0 123#11223344AABBCCDD  # 发送测试报文
8.1.2 Python CAN桥接模块
python
# python/zlg_can_bridge/can_receiver.py
"""
周立功CAN卡数据接收桥接模块
将CAN总线数据转换为MQTT消息，供EVerest其他模块消费
"""

import can
import json
import time
import logging
import threading
from typing import Optional, Callable

logger = logging.getLogger(__name__)

class ZlgCanReceiver:
    """周立功CAN卡接收器—SocketCAN模式"""
    
    def __init__(self, config: dict):
        """
        Args:
            config: CAN配置字典
                - can_channel: CAN接口名称，如 'can0'
                - bitrate: 波特率
                - data_bitrate: CAN FD数据段波特率（可选）
                - receive_own_messages: 是否接收自身发送的消息
        """
        self.channel = config.get('can_channel', 'can0')
        self.bitrate = config.get('bitrate', 500000)
        self.data_bitrate = config.get('data_bitrate', None)
        self.receive_own = config.get('receive_own_messages', False)
        
        self._bus: Optional[can.BusABC] = None
        self._running = False
        self._thread: Optional[threading.Thread] = None
        self._callbacks: list[Callable] = []
        
        # 统计
        self.messages_received = 0
        self.error_count = 0
        self.last_error_time: Optional[float] = None
    
    def start(self):
        """启动CAN接收"""
        try:
            # 创建CAN总线接口
            self._bus = can.Bus(
                interface='socketcan',
                channel=self.channel,
                bitrate=self.bitrate,
                receive_own_messages=self.receive_own
            )
            logger.info(f"CAN接收器启动: channel={self.channel}, bitrate={self.bitrate}")
        except Exception as e:
            logger.error(f"CAN总线初始化失败: {e}")
            raise
        
        self._running = True
        self._thread = threading.Thread(target=self._receive_loop, daemon=True)
        self._thread.start()
    
    def stop(self):
        """停止CAN接收"""
        self._running = False
        if self._thread and self._thread.is_alive():
            self._thread.join(timeout=5.0)
        if self._bus:
            self._bus.shutdown()
        logger.info(f"CAN接收器已停止，共接收 {self.messages_received} 条消息")
    
    def register_callback(self, callback: Callable):
        """注册CAN消息回调函数"""
        self._callbacks.append(callback)
    
    def _receive_loop(self):
        """CAN消息接收主循环"""
        while self._running:
            try:
                message = self._bus.recv(timeout=1.0)
                if message is None:
                    continue
                
                self.messages_received += 1
                
                # 调用所有注册的回调函数
                for callback in self._callbacks:
                    try:
                        callback(message)
                    except Exception as e:
                        logger.error(f"回调函数执行异常: {e}")
                
            except can.CanError as e:
                self.error_count += 1
                self.last_error_time = time.time()
                logger.error(f"CAN接收错误: {e}")
                time.sleep(0.1)
            except Exception as e:
                logger.error(f"未预期的异常: {e}")
                time.sleep(1.0)
    
    def get_statistics(self) -> dict:
        """获取CAN接收统计信息"""
        return {
            'channel': self.channel,
            'messages_received': self.messages_received,
            'error_count': self.error_count,
            'last_error_time': self.last_error_time,
            'is_running': self._running
        }


class ZlgCanSender:
    """周立功CAN卡发送器"""
    
    def __init__(self, config: dict):
        self.channel = config.get('can_channel', 'can0')
        self.bitrate = config.get('bitrate', 500000)
        self._bus: Optional[can.BusABC] = None
        self.messages_sent = 0
    
    def start(self):
        self._bus = can.Bus(
            interface='socketcan',
            channel=self.channel,
            bitrate=self.bitrate
        )
        logger.info(f"CAN发送器启动: channel={self.channel}")
    
    def stop(self):
        if self._bus:
            self._bus.shutdown()
    
    def send_message(self, arbitration_id: int, data: list, 
                     is_extended_id: bool = False, is_fd: bool = False) -> bool:
        """发送单条CAN消息"""
        try:
            msg = can.Message(
                arbitration_id=arbitration_id,
                data=data,
                is_extended_id=is_extended_id,
                is_fd=is_fd
            )
            self._bus.send(msg)
            self.messages_sent += 1
            return True
        except can.CanError as e:
            logger.error(f"CAN发送失败: id=0x{arbitration_id:X}, error={e}")
            return False
    
    def send_batch_messages(self, messages: list[dict], 
                            interval_ms: float = 1.0) -> int:
        """批量发送CAN消息（用于固件升级等大数据量场景）
        
        Args:
            messages: 消息列表，每项包含 arbitration_id, data, is_extended_id
            interval_ms: 消息间隔毫秒数
        
        Returns:
            成功发送的消息数量
        """
        success_count = 0
        for msg_dict in messages:
            if self.send_message(
                arbitration_id=msg_dict['arbitration_id'],
                data=msg_dict['data'],
                is_extended_id=msg_dict.get('is_extended_id', False),
                is_fd=msg_dict.get('is_fd', False)
            ):
                success_count += 1
            time.sleep(interval_ms / 1000.0)
        return success_count
8.2 EVerest集成配置
8.2.1 完整的主配置文件
yaml
# config/config.yaml — EVerest完整系统配置
# ⚠️ 此文件由测试人员根据现场实际情况修改
# 所有路径使用相对路径（相对于everest-core根目录）

active_modules:
  # ===== EVerest基础模块 =====
  admin_panel:
    module: AdminPanel
    config_module:
      port: 8849
      host: "0.0.0.0"
      enable_authentication: true
  
  api:
    module: API
    connections:
      admin_panel:
        - module_id: admin_panel
          implementation_id: admin_api

  # ===== Tier 1: 硬件驱动层 =====
  zlg_can_driver:
    module: ZlgCanDriver
    config_module:
      can_device: "can0"                             # 测试人员修改为实际CAN接口
      bitrate: 500000
      data_bitrate: 2000000
      can_mode: "CAN20B"                             # 或 "CANFD"
      socketcan_interface: "socketcan"
      enable_loopback: false
      error_recovery_enabled: true
      max_retry_count: 3

  # ===== Tier 2: 业务逻辑层 =====
  dev_monitor:
    module: DevMonitor
    config_module:
      poll_interval_ms: 100
      alarm_thresholds_file: "config/alarm_thresholds.yaml"
      watchdog_timeout_s: 30
      max_data_points_per_cycle: 200
    connections:
      zlg_can:
        - module_id: zlg_can_driver
          implementation_id: zlg_can

  dev_manager:
    module: DevManager
    config_module:
      devices_config_file: "config/devices.yaml"
      auto_discovery_enabled: false
    connections:
      zlg_can:
        - module_id: zlg_can_driver
          implementation_id: zlg_can

  device_calibration:
    module: DeviceCalibration
    config_module:
      calibration_params_file: "config/calibration_params.yaml"
      require_approval: true                         # 校准是否需要审核
      auto_apply: false                              # 是否自动应用校准结果
    connections:
      zlg_can:
        - module_id: zlg_can_driver
          implementation_id: zlg_can

  vi_calibration:
    module: VICalibration
    config_module:
      calibration_params_file: "config/calibration_params.yaml"
      default_tolerance_percent: 0.1
    connections:
      zlg_can:
        - module_id: zlg_can_driver
          implementation_id: zlg_can

  protocol_manager:
    module: ProtocolManager
    config_module:
      devices_config_file: "config/devices.yaml"
      enable_protocol_validation: true
    connections:
      zlg_can:
        - module_id: zlg_can_driver
          implementation_id: zlg_can

  battery_protocol_mgr:
    module: BatteryProtocolMgr
    config_module:
      battery_protocols_file: "config/battery_protocols.yaml"
      default_protocol: "GB_T_27930_2015"
    connections:
      zlg_can:
        - module_id: zlg_can_driver
          implementation_id: zlg_can

  data_logger:
    module: DataLogger
    config_module:
      logging_config_file: "config/logging_config.yaml"
      database_path: "data/mw_scada.db"
      enable_auto_backup: true
    connections:
      dev_monitor:
        - module_id: dev_monitor
          implementation_id: dev_monitor

  firmware_upgrader:
    module: FirmwareUpgrader
    config_module:
      firmware_repository_path: "data/firmware/"
      max_concurrent_upgrades: 1
      upgrade_timeout_s: 600
      verify_after_upgrade: true
    connections:
      zlg_can:
        - module_id: zlg_can_driver
          implementation_id: zlg_can

  fault_recorder:
    module: FaultRecorder
    config_module:
      pre_fault_samples_high: 5000
      post_fault_samples_high: 10000
      pre_fault_samples_normal: 1000
      post_fault_samples_normal: 2000
      max_waveform_storage_mb: 10240
      auto_export_enabled: true
      auto_export_format: "csv"
    connections:
      zlg_can:
        - module_id: zlg_can_driver
          implementation_id: zlg_can
      dev_monitor:
        - module_id: dev_monitor
          implementation_id: dev_monitor

  # ===== Tier 3: 应用服务层 =====
  user_manager:
    module: UserManager
    config_module:
      users_config_file: "config/users.yaml"
      database_path: "data/mw_scada.db"
      session_timeout_minutes: 480                    # 8小时会话超时
      max_login_attempts: 5                          # 最大登录尝试次数
      account_lockout_minutes: 30                    # 锁定时间
      password_min_length: 8
      password_require_special_char: true

  report_generator:
    module: ReportGenerator
    config_module:
      report_templates_file: "config/report_templates.yaml"
      report_output_path: "data/reports/"
      default_format: "pdf"
      company_name: "公司名称"                        # 测试人员修改
      company_logo_path: "assets/logo.png"
    connections:
      data_logger:
        - module_id: data_logger
          implementation_id: data_logger

# ===== 系统全局配置 =====
system:
  log_level: "INFO"                                   # 生产环境日志级别
  log_file: "logs/mw_scada.log"
  max_log_size_mb: 100
  log_backup_count: 10
  
  sqlite:
    database_path: "data/mw_scada.db"
    backup_path: "data/backup/"
    backup_interval_hours: 24
    max_backups: 30
  
  performance:
    max_mqtt_message_size_kb: 256
    mqtt_qos: 1                                       # QoS级别
    worker_threads: 4                                  # 工作线程数
8.2.2 部署脚本
bash
#!/bin/bash
# scripts/deploy.sh — 一键部署MW充放电上位机系统
# 使用方法: bash scripts/deploy.sh [config_file.yaml]

set -e

EVEREST_CORE_PATH="${EVEREST_CORE_PATH:-/opt/everest-core}"
PROJECT_PATH="$(cd "$(dirname "$0")/.." && pwd)"
CONFIG_FILE="${1:-config/config.yaml}"

echo "========================================="
echo "  MW充放电系统上位机 部署脚本"
echo "  EVerest路径: $EVEREST_CORE_PATH"
echo "  项目路径: $PROJECT_PATH"
echo "  配置文件: $CONFIG_FILE"
echo "========================================="

# 1. 检查依赖
echo "[1/6] 检查系统依赖..."
command -v cmake >/dev/null 2>&1 || { echo "❌ 需要cmake"; exit 1; }
command -v sqlite3 >/dev/null 2>&1 || { echo "❌ 需要sqlite3"; exit 1; }
python3 -c "import can" 2>/dev/null || { echo "❌ 需要python-can"; exit 1; }
echo "✅ 依赖检查通过"

# 2. 初始化数据库
echo "[2/6] 初始化SQLite数据库..."
bash "$PROJECT_PATH/scripts/init_database.sh" "$PROJECT_PATH/data/mw_scada.db"
echo "✅ 数据库初始化完成"

# 3. 创建配置目录
echo "[3/6] 创建目录结构..."
mkdir -p "$PROJECT_PATH/data/backup"
mkdir -p "$PROJECT_PATH/data/firmware"
mkdir -p "$PROJECT_PATH/data/reports"
mkdir -p "$PROJECT_PATH/logs"
echo "✅ 目录结构已创建"

# 4. 复制配置文件到EVerest
echo "[4/6] 部署配置文件..."
cp "$PROJECT_PATH/$CONFIG_FILE" "$EVEREST_CORE_PATH/config/"
cp "$PROJECT_PATH/config/"*.yaml "$EVEREST_CORE_PATH/config/" 2>/dev/null || true
echo "✅ 配置文件已部署"

# 5. 构建项目
echo "[5/6] 构建项目模块..."
cd "$EVEREST_CORE_PATH/build"
cmake .. -DEVEREST_INCLUDE_MODULES="ZlgCanDriver;DevMonitor;DevManager;DeviceCalibration;VICalibration;ProtocolManager;BatteryProtocolMgr;DataLogger;FirmwareUpgrader;FaultRecorder;UserManager;ReportGenerator"
make -j$(nproc)
sudo make install
echo "✅ 项目构建完成"

# 6. 注册模块
echo "[6/6] 注册模块到EVerest..."
ev-cli module register --path "$EVEREST_CORE_PATH/modules/ZlgCanDriver"
ev-cli module register --path "$EVEREST_CORE_PATH/modules/DevMonitor"
# ... 依次注册所有模块
echo "✅ 模块注册完成"

echo ""
echo "========================================="
echo "  ✅ 部署完成！"
echo ""
echo "  启动系统:"
echo "    cd $EVEREST_CORE_PATH/build"
echo "    ./everest-start --config config/config.yaml"
echo ""
echo "  访问 Admin Panel: http://localhost:8849"
echo "========================================="
9. 自动化测试规范
9.1 测试框架
EVerest提供基于pytest的测试工具，包含everest-core的fixtures以及OCPP实现的封装。本项目的测试基于此框架扩展。

bash
# 安装测试依赖
pip install pytest pytest-cov pytest-mock pytest-timeout pytest-asyncio

# 运行所有测试
cd tests
pytest -v --cov=../modules --cov-report=html

# 按模块运行测试
pytest unit/test_zlg_can_driver.cpp -v
pytest integration/test_data_logger.py -v
9.2 测试目录结构
text
tests/
├── conftest.py                   # pytest全局fixtures
├── pytest.ini                    # pytest配置
│
├── unit/                         # 单元测试
│   ├── test_zlg_can_driver.cpp   # ZLG CAN驱动测试
│   ├── test_sqlite_manager.cpp   # SQLite操作测试
│   ├── test_can_parser.cpp       # CAN报文解析测试
│   ├── test_calibration.cpp      # 校准算法测试
│   ├── test_protocol_parser.py   # 协议解析测试
│   └── test_data_buffer.cpp      # 数据缓冲区测试
│
├── integration/                  # 集成测试
│   ├── test_dev_monitor.py       # 设备监控集成测试
│   ├── test_data_logger.py       # 数据记录集成测试
│   ├── test_user_auth.py         # 用户认证集成测试
│   └── test_report_generation.py # 报告生成集成测试
│
├── e2e/                          # 端到端测试
│   └── test_full_charge_cycle.py # 完整充放电流程测试
│
├── fixtures/                     # 测试数据
│   ├── mock_can_data.py          # 模拟CAN数据
│   ├── test_config.yaml          # 测试配置
│   ├── sample_firmware.bin       # 示例固件文件
│   └── test.db                   # 测试数据库
│
└── performance/                  # 性能测试
    ├── test_write_performance.py # 数据库写入性能
    └── test_can_throughput.py    # CAN吞吐量测试
9.3 模拟CAN数据生成器
python
# tests/fixtures/mock_can_data.py
"""模拟CAN数据生成器—用于单元测试和集成测试"""

import random
import time
import struct
from dataclasses import dataclass
from typing import List, Optional

@dataclass
class MockCanMessage:
    arbitration_id: int
    data: List[int]
    is_extended_id: bool = False
    timestamp: float = 0.0

class MockCanDataGenerator:
    """模拟充放电设备CAN数据"""
    
    def __init__(self, seed: Optional[int] = None):
        if seed is not None:
            random.seed(seed)
        self.message_count = 0
    
    def generate_voltage_message(self, device_addr: int = 0x01,
                                  base_voltage: float = 800.0,
                                  noise_percent: float = 0.1) -> MockCanMessage:
        """生成模拟电压数据报文（CAN ID: 0x181）"""
        voltage = base_voltage * (1 + random.uniform(-noise_percent/100, noise_percent/100))
        raw_value = int(voltage * 10)  # 0.1V精度
        data = [
            (raw_value >> 8) & 0xFF,
            raw_value & 0xFF,
            0x80,  # 数据质量标志: GOOD
            0x00, 0x00, 0x00, 0x00, 0x00
        ]
        self.message_count += 1
        return MockCanMessage(
            arbitration_id=0x181,
            data=data,
            timestamp=time.time()
        )
    
    def generate_current_message(self, device_addr: int = 0x01,
                                  base_current: float = 500.0,
                                  is_charging: bool = True) -> MockCanMessage:
        """生成模拟电流数据报文（CAN ID: 0x182）"""
        current = base_current * (1 + random.uniform(-0.05, 0.05))
        if not is_charging:
            current = -current
        raw_value = int(current * 100)  # 0.01A精度
        raw_value = raw_value & 0xFFFF  # 16位有符号整数
        data = [
            (raw_value >> 8) & 0xFF,
            raw_value & 0xFF,
            0x80,  # GOOD
            0x00, 0x00, 0x00, 0x00, 0x00
        ]
        self.message_count += 1
        return MockCanMessage(
            arbitration_id=0x182,
            data=data,
            timestamp=time.time()
        )
    
    def generate_temperature_message(self, device_addr: int = 0x01,
                                      base_temp: float = 35.0) -> MockCanMessage:
        """生成模拟温度数据报文（CAN ID: 0x183）"""
        temp = base_temp + random.uniform(-2.0, 2.0)
        integer_part = int(temp)
        decimal_part = int((temp - integer_part) * 10)
        data = [
            integer_part & 0xFF,
            decimal_part & 0x0F,
            0x80,  # GOOD
            0x00, 0x00, 0x00, 0x00, 0x00
        ]
        self.message_count += 1
        return MockCanMessage(
            arbitration_id=0x183,
            data=data,
            timestamp=time.time()
        )
    
    def generate_fault_message(self, device_addr: int = 0x01,
                                fault_code: int = 0x0001) -> MockCanMessage:
        """生成模拟故障报文"""
        data = [
            (fault_code >> 8) & 0xFF,
            fault_code & 0xFF,
            0x01,  # 故障有效
            0x00, 0x00, 0x00, 0x00, 0x00
        ]
        self.message_count += 1
        return MockCanMessage(
            arbitration_id=0x200 + device_addr,
            data=data,
            timestamp=time.time()
        )
9.4 集成测试示例
python
# tests/integration/test_data_logger.py
"""数据记录模块集成测试"""

import pytest
import sqlite3
import time
from pathlib import Path

@pytest.fixture
def test_db():
    """创建测试数据库"""
    db_path = "tests/fixtures/test.db"
    # 使用测试配置初始化
    yield db_path
    # 清理
    Path(db_path).unlink(missing_ok=True)

@pytest.fixture
def data_logger(test_db, mock_zlg_can):
    """初始化DataLogger模块"""
    config = {
        "database_path": test_db,
        "logging_config_file": "tests/fixtures/test_config.yaml"
    }
    logger = DataLogger(config)
    logger.init()
    yield logger
    logger.stop()

class TestDataLogger:
    """数据记录模块测试类"""
    
    def test_single_insert(self, data_logger, test_db):
        """测试单条数据插入"""
        record = MeasurementRecord(
            timestamp=int(time.time() * 1000),
            device_id="TEST_DEV_001",
            session_id="TEST_SESSION_001",
            parameter_name="voltage",
            value=800.5,
            unit="V",
            quality="GOOD"
        )
        data_logger.write_single(record)
        
        # 验证数据库
        conn = sqlite3.connect(test_db)
        cursor = conn.execute(
            "SELECT * FROM measurement_data WHERE device_id = ?",
            ("TEST_DEV_001",)
        )
        rows = cursor.fetchall()
        conn.close()
        assert len(rows) == 1
        assert rows[0][4] == "voltage"
        assert abs(rows[0][5] - 800.5) < 0.01
    
    def test_batch_insert_performance(self, data_logger, test_db):
        """测试批量插入性能"""
        records = []
        base_time = int(time.time() * 1000)
        for i in range(1000):
            records.append(MeasurementRecord(
                timestamp=base_time + i * 100,  # 100ms间隔
                device_id=f"DEV_{i % 5:03d}",
                session_id="PERF_TEST_SESSION",
                parameter_name="voltage",
                value=800.0 + i * 0.1,
                unit="V",
                quality="GOOD"
            ))
        
        start_time = time.time()
        data_logger.write_batch(records)
        elapsed = time.time() - start_time
        
        # 验证写入速度 > 5000条/秒
        throughput = len(records) / elapsed
        assert throughput > 5000, f"写入速度不足: {throughput:.0f} 条/秒"
        
        # 验证数据完整性
        conn = sqlite3.connect(test_db)
        count = conn.execute("SELECT COUNT(*) FROM measurement_data").fetchone()[0]
        conn.close()
        assert count == 1000
    
    def test_session_query(self, data_logger, test_db):
        """测试按会话查询数据"""
        # 先写入测试数据
        records = []
        base_time = int(time.time() * 1000)
        for i in range(100):
            records.append(MeasurementRecord(
                timestamp=base_time + i * 1000,
                device_id="DEV_001",
                session_id="QUERY_TEST_SESSION",
                parameter_name="voltage",
                value=800 + i,
                unit="V",
                quality="GOOD"
            ))
        data_logger.write_batch(records)
        
        # 查询会话数据
        result = data_logger.query_data(
            session_id="QUERY_TEST_SESSION",
            start_time="2000-01-01T00:00:00",
            end_time="2099-12-31T23:59:59"
        )
        assert len(result) == 100
10. 附录：非生产环境配置示例
10.1 快速启动指南
bash
# 1. 克隆项目
git clone <项目仓库地址>
cd mw-charge-discharge-scada

# 2. 安装依赖
bash scripts/install_dependencies.sh

# 3. 部署
bash scripts/deploy.sh config/config.yaml

# 4. 验证
# 访问 http://localhost:8849 打开EVerest Admin Panel
# 检查所有模块状态是否正常
10.2 常用命令速查
操作	命令
启动系统	./build/everest-start --config config/config.yaml
查看模块状态	ev-cli module list
查看CAN数据	candump can0
数据库查询	sqlite3 data/mw_scada.db "SELECT COUNT(*) FROM measurement_data;"
查看日志	tail -f logs/mw_scada.log
数据库备份	bash scripts/backup_db.sh
运行测试	cd tests && pytest -v
生成覆盖率报告	pytest --cov --cov-report=html
10.3 故障排查速查表
常见问题	排查步骤
CAN卡无法识别	dmesg | grep can 检查驱动加载；ip link show 检查网络接口
数据不更新	检查MQTT broker状态：systemctl status mosquitto
数据库写入失败	检查磁盘空间：df -h；检查文件权限
模块启动失败	查看日志中的ERROR级别信息；检查配置文件语法
Admin Panel无法访问	检查端口占用：netstat -tlnp | grep 8849
文档结束

本文档提供了基于EVerest平台开发MW级充放电上位机系统的完整技术规范。AI编程智能体可根据各节的定义文件、代码模板和配置示例自动生成功能模块代码。测试人员仅需修改config/目录下的YAML配置文件即可适配不同的硬件设备、通信协议和业务需求。

参考项目仓库：

EVerest核心：https://github.com/EVerest/everest-core

EVerest官方文档：https://everest.github.io/nightly/

周立功CAN驱动参考：https://github.com/xjtuecho/USBCAN（Gitee）/ https://github.com/aron566/EOL_CAN_Tool（GitHub）