# legod-auto-pause-dotnet

雷神加速器 · 时长自动暂停工具（.NET 重写版）

<!-- 仓库地址：github.com/oOtroyOo/legod-auto-pause-dotnet（已确定） -->
<!-- 1) GitHub Release 徽章（发布首个 Release 后会自动显示版本号；当前无 Release，先预留） -->
[![GitHub Release](https://img.shields.io/github/v/release/oOtroyOo/legod-auto-pause-dotnet?include_prereleases&style=flat-square)](https://github.com/oOtroyOo/legod-auto-pause-dotnet/releases)

<!-- 2) 累计下载量徽章（同样待发布后自动统计） -->
[![GitHub Downloads](https://img.shields.io/github/downloads/oOtroyOo/legod-auto-pause-dotnet/total?style=flat-square)](https://github.com/oOtroyOo/legod-auto-pause-dotnet/releases)

<!-- 3) GitHub Actions 构建状态徽章（需先在本仓库添加 .github/workflows/build.yml 等工作流文件） -->
[![Build](https://github.com/oOtroyOo/legod-auto-pause-dotnet/actions/workflows/build.yml/badge.svg)](https://github.com/oOtroyOo/legod-auto-pause-dotnet/actions/workflows/build.yml)

## 一、项目简介

本工具用于**自动监控雷神加速器（Leigod）的加速状态与游戏进程**，在游戏退出后自动暂停加速，
帮你省下每一分钟加速时长；并在**系统关机 / 注销前**自动暂停，避免忘记暂停而白白浪费时间。

本版本使用 **C# / .NET** 全面重构（Fork 自原 Python 项目 `legod-auto-pause`），核心升级点：

- 基于 **.NET Framework 4.8.1** 构建，Windows 10/11 已自带该运行时，**用户无需安装运行时**；
- **前后端分离**：监控逻辑运行在 **Windows 服务**中，前端托盘 UI 可随时关闭，监控不中断；
- 内置 **WebView2** 打开官网登录页，**一键从网页提取 token**，账号密码无需写入配置文件；
- 监听系统**关机 / 注销**事件，自动暂停，免得关机前忘记。

---

## 二、快速开始（使用步骤）

> 下面是最短上手路径；各步骤的详细说明见后文对应章节。

1. **下载并解压**
   前往 [Releases 页面](https://github.com/oOtroyOo/legod-auto-pause-dotnet/releases) 下载最新版压缩包
   （若当前尚未发布 Release，可先用 `publish.ps1` 或 `dotnet publish` 自行构建）

2. **安装为 Windows 服务（推荐）**
   - 方式 A（图形界面）：双击运行 `LeigodPause.exe` → 「仪表盘」页点击 **「安装服务」** → 按 UAC 提示授权。
   - 方式 B（命令行）：以管理员身份执行 `LeigodPause.Service.exe --install`（或 `-i`）。
   
   安装后服务随系统启动自动运行；同一按钮 / `--uninstall` 可卸载。

3. **登录获取 Token**
   运行 `LeigodPause.exe`，在「仪表盘」页右侧 **WebView** 中登录雷神官网
   （默认进入个人中心 `https://www.leigod.com/personal-center`），登录后点击 **「检查 Token」**，
   程序会自动从网页 `localStorage` 提取 `account_token` 并写入 `config.ini`，同时拉取账号信息。

4. **配置要监控的游戏进程**
   编辑 `config.ini` 的 `games` 字段（逗号分隔，进程名可不含 `.exe`），或在软件内「游戏」页面动态增删。
   例如：`games = GTA5,Overwatch,notepad`。

5. **正常使用**
   - 游戏进程全部退出后，超过 `looptime`（默认 30 秒）仍未开新游戏，后端**自动调用暂停**；
   - **系统关机 / 重启 / 注销前**，后端自动尝试暂停，防止忘记暂停而浪费时长；
   - 前端 UI 可随时最小化到托盘或退出，监控由后台服务持续执行。

---

## 三、核心特性

- 🔐 **WebView2 内置登录**：在软件内直接打开雷神官网登录页，登录后一键从网页
  `localStorage` 提取 `account_token`，账号密码**无需写入配置文件**。
- 🔄 **自动检查状态**：后台服务定时调用雷神 Web API，自动获取登录状态、暂停状态。
- ⏸️ **进程退出自动暂停**：可配置游戏进程名，实时检测游戏进程是否存在；游戏退出后，
  在设定的「宽限时间」内未重新启动别的游戏，则自动调用雷神暂停接口。
- 🖥️ **前后端分离 + Windows 服务**：核心监控逻辑运行在 **Windows 服务**中，前端 UI
  （托盘程序）可随时关闭，监控不中断。无需前端长时间挂起，更省资源、更稳定。
- 🔌 **进程名可配置**：通过 `config.ini` 或软件内「游戏」页面增删游戏进程名，支持
  多游戏切换（切换间隙不误暂停）。
- 🛡️ **关机 / 注销保护**：监听系统会话结束事件，在电脑关机、重启、注销前自动暂停加速，
  免得关机前忘记暂停而浪费时长。
- 📦 **免运行时部署**：基于 .NET Framework 4.8.1 编译，Windows 10/11 已自带该运行时，
  **用户无需额外安装 .NET 运行时**即可运行。
- 🔧 **托盘常驻 + 图形界面**：基于 WPF + WPF-UI 的现代化界面，系统托盘常驻，最小化不退出。

---

## 四、环境要求

- **操作系统**：Windows 10 / 11（x64）。
- **运行时**：**无需安装**。程序基于 **.NET Framework 4.8.1** 构建，Windows 10/11 已内置该版本。
- **WebView2 运行时**：现代 Windows 10/11 已随系统自带 Edge WebView2；若缺失，程序首次使用
  登录页时会提示，可前往微软官网安装「WebView2 Runtime」。
- **权限**：将工具注册为 Windows 服务需要**管理员权限**（UAC 提权），仅首次安装 / 卸载时需要。

---

## 五、工作原理

```
┌─────────────────────────┐        命名管道(IPC, protobuf)        ┌──────────────────────────────┐
│   LeigodPause (前端)     │ <──────────────────────────────────> │   LeigodPause.Service (后端)  │
│  WPF + WebView2 + 托盘   │   命令: Ping/PauseState/             │   Windows 服务 (Session 0)    │
│  - WebView 登录取 token  │   RefreshUserInfo/FindGameProcess/   │   - 定时拉取用户信息/暂停状态 │
│  - 展示状态/手动暂停     │   ReloadConfig/Exit                  │   - 轮询游戏进程             │
│  - 游戏进程配置          │                                      │   - 进程退出延时自动暂停     │
└─────────────────────────┘                                      │   - 关机/注销事件自动暂停    │
                                                                    └──────────────┬───────────────┘
                                                                                     │ HTTPS
                                                                                     ▼
                                                              ┌────────────────────────────────────┐
                                                              │   webapi.leigod.com (雷神官方 API)  │
                                                              │   /api/user/info  ·  /api/user/pause│
                                                              └────────────────────────────────────┘
```

- **后端服务**（`LeigodPause.Service`）以 Windows 服务形式后台运行，负责所有网络请求与
  进程监控；即使你关掉前端窗口（最小化到托盘或退出 UI），监控依旧持续。
- **前端 UI**（`LeigodPause`）是一个轻量托盘程序，通过命名管道与后端通信：展示账号、
  暂停状态、当前匹配到的游戏进程，并提供「网页登录取 token」「立即暂停」「游戏进程管理」等入口。
- 当后端检测到：
  1. 当前**正在加速**（`pause_status_id == 0`），且
  2. 配置的游戏进程**全部退出**，且
  3. 超过 `looptime` 秒宽限时间仍无新游戏启动，
  
  则自动调用雷神暂停接口。
- 当系统触发**关机 / 注销 / 服务停止**事件时，后端会以非阻塞方式（带 3 秒超时）尝试一次暂停，
  避免卡住系统关机流程。

---

## 六、配置文件 `config.ini`

配置文件位于程序同级目录 `config.ini`，用记事本即可编辑。下列为默认模板（注释已保留）：

```ini
[config]

# 游戏名称（游戏程序在任务管理器中的名称，逗号分隔，可不含 .exe 后缀）
# 支持在软件内「游戏」页面动态增删
games = dontstarve_steam_x64,GTA5

# 允许游戏关闭的时间（在此时间内切换游戏不会暂停加速器）单位：秒
looptime = 30

# 检测时间，多少秒检测一次程序
update = 1

# token，优先度大于密码
# 不填会选择用户密码验证，如果怕泄露密码可以自己去官网获取 token
# 注意：token 会过期，需要时常手动获取（WebView 登录后也会自动写入）
account_token = 
```

| 配置项 | 说明 | 是否必需 |
| --- | --- | --- |
| `games` | 监控的游戏进程名，逗号分隔；进程退出后触发自动暂停逻辑 | 是（至少一个） |
| `looptime` | 游戏全部退出后的「宽限时间」（秒），超时仍未开新游戏则自动暂停 | 是 |
| `update` | 后端服务轮询间隔（秒），最小为 1 | 是 |
| `account_token` | 雷神登录 token；由 WebView 登录后自动写入，优先级最高 | 是（登录后） |

> 修改 `config.ini` 后，前端点击「重载配置」或通过 IPC 下发 `ReloadConfig` 命令即可生效，
> 一般无需重启服务。

---

## 七、使用说明

- **仪表盘（Dashboard）**：查看当前账号、暂停状态、是否匹配到游戏进程；右侧 WebView 用于
  登录与取 token；提供「安装/卸载服务」「运行服务」「立即暂停」等操作。
- **游戏（Games）**：可视化管理需要监控的游戏进程名，新增 / 删除即时同步到 `config.ini`。
- **设置（Settings）**：界面主题等偏好设置。
- **系统托盘**：程序最小化后常驻托盘，不退出监控；仅当在界面中确认退出时才会清理 WebView 缓存。
- **自动暂停流程**：游戏进程全部退出 → 等待 `looptime` 秒 → 仍未开新游戏 → 后端自动调用暂停。
- **关机保护**：系统关机 / 重启 / 注销前，后端自动尝试暂停（非阻塞，3 秒超时），防止忘记暂停。

---

## 八、已知问题 / 常见问题

- **token 会过期**：雷神 `account_token` 存在有效期，过期后监控会失效。请重新在 WebView 中
  登录并点击「检查 Token」刷新。token 优先级最高，无需填写账号密码。
- **部分游戏进程名不匹配**：若某游戏在雷神数据库中的进程名与任务管理器不一致，可在「游戏」
  页面手动补充其进程名。
- **杀软误报**：本工具会注册/操作 Windows 服务、与雷神 Web API 通信，部分杀毒软件可能弹窗
  警告，属正常行为，可加入白名单。
- **需要管理员权限**：仅安装 / 卸载 Windows 服务时需要提权；日常监控运行在服务账户下，无需
  前台常开 UI。

---

## 九、免责声明

本项目仅为**兴趣使然、边学边写**，与雷神官方无任何关联。

- 禁止将本工具用于任何**商业用途**。
- 使用本工具产生的任何账号风险、时长损失等后果，**由使用者自行承担**。
- 可以转载、使用并帮忙优化，发布时请**保留原作者署名**。

---

## 十、致谢 / 参考

- 本项目的架构与核心思路 Fork 自原 Python 项目
  [**`legod-auto-pause`**](https://github.com/6yy66yy/legod-auto-pause)（作者 [@6yy66yy](https://github.com/6yy66yy)），
  在此表示感谢。
- 雷神加速器官方 Web API（`webapi.leigod.com`）仅作技术研究用途调用。

---

## 十一、构建（开发者）

仓库根目录提供 `dotnet/publish.ps1` 一键发布脚本（停止服务 → 发布 → 重启服务）：

```powershell
cd dotnet
.\publish.ps1
```

或手动发布：

```bat
dotnet publish LeigodPause/LeigodPause.csproj -c Release -f net481 -p:PublishLayout=true -o ./publish
```

解决方案包含三个项目：

| 项目 | 说明 |
| --- | --- |
| `LeigodPause` | WPF 前端（WebView2 登录 + 托盘 UI + IPC 客户端） |
| `LeigodPause.Service` | 后端 Windows 服务（API 调用 + 进程监控 + IPC 服务端 + 关机监听） |
| `LeigodPauses.Test` | 单元测试 |

---

<!-- 觉得好用可以给个 Star ⭐ -->
