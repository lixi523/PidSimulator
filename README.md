# PID 模拟器（Windows 桌面应用）

一款运行于 Windows 的 **PID 控制器模拟器**：内置可调的**过程仿真模型（被控对象）**形成闭环，并对外提供 **Modbus TCP 服务**，支持上位机/PLC 通过标准协议读写参数。适用于 PID 教学演示、上位机组态联调、控制算法验证等场景。

- **UI**：C# / .NET 8 + WPF（MVVM）
- **协议**：自实现轻量 Modbus TCP 服务端（`127.0.0.1:502`）
- **打包**：单文件自包含 exe，目标机免装 .NET

---

## 功能特性

| 功能 | 说明 |
|------|------|
| PID 闭环仿真 | 离散位置式 PID，含**微分先行**、**条件积分抗饱和**、**积分钳位** |
| 过程仿真 FOPDT | 一阶惯性 + 纯滞后（`G(s)=K·e^(−θs)/(τs+1)`），K/τ/θ 可调，运行中实时生效 |
| 手/自动切换 | 支持自动/手动切换，切换**无扰动（bumpless）** |
| 参数设置 | SP、P、I、D 界面可设，运行中实时生效 |
| 实时趋势 | PV / SP / OP 三条曲线（内置绘制，零第三方依赖） |
| Modbus TCP 服务 | `127.0.0.1:502`，支持 FC03 / FC06 / FC16 |
| 写入回显 | 外部 Modbus 写入后界面**即时刷新**，并记录到日志 |
| Modbus 日志 | **时间倒序**显示连接、写入、模式切换等事件，可一键清空 |
| 配置持久化 | 参数可保存/加载（`appsettings.json`） |
| 运行/暂停 | 暂停后 PV/OP 冻结，参数仍可修改与读写 |

---

## 界面说明

```
┌──────────────────────────────────────────────────────────────────────────┐
│ PID 模拟器            运行状态：暂停仿真   模式：自动仿真   Modbus:502 已监听 │
├───────────────────────┬──────────────────────────────────────────────────┤
│  参数设置              │            实时趋势（PV / SP / OP）                │
│  SP 设定值 (4001)      │   ┌────────────────────────────────────────────┐  │
│  [ 50.00 ]             │   │      ╱╲    PV(蓝)                          │  │
│  P 比例增益 (4005)     │   │  ╱╲╱  ╲   SP(橙)                          │  │
│  [  2.000 ]            │   │ ╱      ╲____  OP(绿)                      │  │
│  I 积分时间 s (4007)   │   └────────────────────────────────────────────┘  │
│  [ 10.00 ]             │                                                  │
│  D 微分时间 s (4009)   ├──────────────────────────────────────────────────┤
│  [  0.00 ]             │   PV 过程值(4011)  OP 输出%(4003)  SP 设定值(4001) │
│  OP 输出 % (4003)      │      99.98            100.00         100.00       │
│  [ 0.00 ]              │                                                  │
│  [自动仿真] [暂停仿真]  │                                                  │
│  [恢复默认] [保存配置]  │                                                  │
│  [清空趋势] [清空日志]  │                                                  │
├───────────────────────┴──────────────────────────────────────────────────┤
│ 过程模型(FOPDT) K[1.00] τ[10.0] θ[1.0] 扫描周期[100]ms │ Modbus 日志(倒序) │
└──────────────────────────────────────────────────────────────────────────┘
```

- **左侧参数区**：SP / P / I / D 可编辑（标签含 Modbus 地址），OP 为只读显示；下方按钮组控制模式、运行、复位与清空。
- **中部趋势**：PV（蓝）/ SP（橙）/ OP（绿）实时曲线。
- **底部读数**：PV / OP / SP 大数字实时刷新。
- **底部日志**：Modbus 事件日志，最新一条置顶。

---

## Modbus TCP 寄存器映射

服务端监听 `127.0.0.1:502`，从站号（Unit ID）默认 `1`。

| 变量 | 名称 | 参考号 (4xxxx) | PDU 起始偏移 | 寄存器 | 类型 | 访问 |
|------|------|----------------|--------------|--------|------|------|
| SP | 设定值 | 4001 | 0 | 0–1 | float32 | R/W |
| OP | 输出值 | 4003 | 2 | 2–3 | float32 | R/W |
| P | 比例增益 | 4005 | 4 | 4–5 | float32 | R/W |
| I | 积分时间 Ti (s) | 4007 | 6 | 6–7 | float32 | R/W |
| D | 微分时间 Td (s) | 4009 | 8 | 8–9 | float32 | R/W |
| **PV** | **过程值** | **4011** | **10** | **10–11** | float32 | **只读** |

**要点**
- 地址约定：`PDU 偏移 = 地址号 − 4001`；寄存器区共 **12** 个（偏移 0–11）。
- 数据类型：IEEE-754 单精度 float，占 2 个连续保持寄存器，**大端 ABCD**（寄存器 N 存高 16 位）。
- 功能码：`0x03` 读保持寄存器 / `0x06` 写单寄存器 / `0x10` 写多寄存器。
- **PV 只读**：写 PV（偏移 10–11）返回异常 `0x02`，值不变。
- 自动模式下写 OP → 自动切换到手动（无扰）。
- 写 float 推荐用 `FC16` 一次写 2 个寄存器（原子）；用 `FC06` 分两次写高/低字中间态可能被读到。

### 读写示例（Modbus Poll / QModMaster）

1. 连接 `127.0.0.1:502`，Unit ID = 1。
2. 读全部参数：`FC03` 起始偏移 `0`、数量 `12` → 得到 SP/OP/P/I/D/PV 共 6 个 float。
3. 改 SP：`FC16` 写偏移 `0–1` = 80.0 → 界面 SP 即时刷新，日志新增一行。
4. 写 OP：`FC16` 写偏移 `2–3` → 自动模式下会切到手动。

---

## 技术栈与项目结构

```
PidSimulator.slnx
├─ src/PidSimulator.Core        # 数据模型：ControlState、ControlMode
├─ src/PidSimulator.Engine      # 算法：PidController、FopdtModel、TrendBuffer、SimulationEngine
├─ src/PidSimulator.Modbus      # Modbus：RegisterMap、FloatCodec、ModbusTcpServer
├─ src/PidSimulator.Config      # 配置：AppConfig、ConfigService
├─ src/PidSimulator.ViewModels  # MVVM：MainViewModel、RelayCommand
├─ src/PidSimulator.App         # WPF 界面：App、MainWindow、appsettings.json
└─ tests/PidSimulator.Tests     # 单元测试（编解码 / 算法 / Modbus 端到端）
```

依赖方向：`App → ViewModels → { Core, Engine, Modbus, Config }`。

---

## 快速开始

### 环境要求
- .NET 8 SDK（开发/运行），Windows 10/11 x64

### 运行（源码）
```powershell
dotnet run --project src/PidSimulator.App
```

### 构建
```powershell
dotnet build PidSimulator.slnx
```

### 运行测试
```powershell
dotnet test
```

### 发布为独立 exe（免装 .NET）
```powershell
dotnet publish src/PidSimulator.App/PidSimulator.App.csproj `
  -c Release -r win-x64 --self-contained true `
  -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true `
  -o publish/win-x64
```
产物：`publish/win-x64/PidSimulator.exe`（自包含单文件，约 150 MB）+ `appsettings.json`。

> 端口 502 属特权端口，若绑定失败请**以管理员身份运行**；程序启动时会读取同目录的 `appsettings.json`。

---

## 配置（appsettings.json）

```json
{
  "Modbus": {
    "ListenAddress": "127.0.0.1",
    "Port": 502,
    "UnitId": 1,
    "MaxConnections": 5,
    "WordSwap": false,
    "OpWriteInAuto": "SwitchToManual"
  },
  "Pid": { "DirectAction": false, "OpMin": 0.0, "OpMax": 100.0, "SpMin": 0.0, "SpMax": 100.0 },
  "Process": { "K": 1.0, "Tau": 10.0, "Theta": 1.0, "Noise": 0.0, "InitialPv": 0.0 },
  "Ui": { "TrendWindowSec": 60, "TrendBufferSize": 600 }
}
```

| 字段 | 说明 |
|------|------|
| `Modbus.Port` | 监听端口，默认 502 |
| `Modbus.MaxConnections` | 最大并发连接数，默认 5 |
| `Modbus.WordSwap` | `false`=大端 ABCD（默认）；`true`=字序交换 CDAB |
| `Modbus.OpWriteInAuto` | 自动态写 OP 策略：`SwitchToManual`（默认）/ `Ignore` |
| `Pid.DirectAction` | `false`=反作用 e=SP−PV（默认）；`true`=正作用 |
| `Process.K / Tau / Theta` | 过程模型增益 / 时间常数(s) / 纯滞后(s) |
| `Ui.TrendBufferSize` | 趋势缓冲点数，默认 600（@100ms ≈ 60s） |

---

## 默认参数

| 参数 | 默认值 | 范围 |
|------|--------|------|
| SP | 50.0 | 0–100 |
| OP | 0.0 | 0–100 |
| P (Kp) | 2.0 | 0–1000 |
| I (Ti) | 10.0 s | 0–10000 |
| D (Td) | 0.0 s | 0–10000 |
| K | 1.0 | 0.01–1000 |
| τ | 10.0 s | 0.1–10000 |
| θ | 1.0 s | 0–1000 |
| 扫描周期 | 100 ms | 10–60000 |

> `I` = 积分时间 Ti，`D` = 微分时间 Td；`Ti=0` 无积分、`Td=0` 无微分。默认**反作用**方向。

---

## 测试

`tests/PidSimulator.Tests` 含 34 个用例，覆盖：

- float ↔ 寄存器对编解码（ABCD / CDAB、往返一致）
- 寄存器地址映射与读写
- PID 比例/限幅/正反作用/无扰切换/抗饱和/复位
- FOPDT 稳态增益、纯滞后抑制与延迟响应
- 趋势环形缓冲
- 仿真引擎闭环收敛、手动保持、暂停冻结
- **Modbus 端到端**：FC03 读全部、FC16 写 SP、写 OP 切手动、读 PV、写 PV 被拒、写入回显事件

---

## 许可

内部演示项目，未附开源许可。
