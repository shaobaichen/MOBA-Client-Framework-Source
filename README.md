# MOBA Client Framework · 社交大厅与客户端框架

> **项目定位：Unity 6 游戏客户端架构与社交大厅 Demo（公开源码展示版）**
>
> 本仓库主要展示项目的 **C# / Lua 业务代码、网络通信、UI 管理和资源更新架构**，**不是可以直接打开运行的完整 Unity 工程**。
> 为避免再分发第三方素材及插件，已排除 Unity 场景、Prefab、美术资源、AssetBundle 成品、第三方 DLL / 原生库、生成的 Protobuf C# 代码以及 xLua 插件本体。
> 游戏实际运行截图可在 [GitHub 个人主页的 MOBA 展柜](https://github.com/shaobaichen) 查看。

## 一、项目简介

这是一个基于 **Unity 6 + C# + xLua + TCP + Protobuf** 的游戏客户端基础框架与社交大厅演示项目，重点实践客户端模块分层、界面生命周期、网络消息处理、资源加载以及 Lua 业务逻辑组织。

目前源码包含的主要功能：

- **账号与大厅**：注册、登录、玩家信息展示及大厅 UI。
- **好友系统**：好友申请、好友列表、好友关系处理与在线状态通知。
- **私聊功能**：好友聊天消息发送、接收与聊天记录请求。
- **网络通信**：TCP 长连接、自定义消息帧、Protobuf 协议以及基于消息 ID 的路由。
- **C# / Lua 协作**：xLua 控制器调用 C# 网络接口，通过事件中心通知 UI 更新。
- **资源管理**：AssetBundle 依赖加载、资源引用计数及基础版本更新流程。
- **本地联调**：使用 MockServer 验证账号、好友和聊天等客户端交互流程。

**范围说明：** 这是一个以**社交大厅和客户端基础设施**为重点的技术实践项目，**不是已经完成的 MOBA 游戏**。当前公开快照不包含英雄技能战斗、匹配对局或完整 MOBA 战斗系统。

## 二、技术栈

| 模块 | 使用技术 | 主要职责 |
| --- | --- | --- |
| 引擎与语言 | Unity 6（6000.3.10f1）、C# | 客户端运行环境与基础模块 |
| 业务脚本 | Lua / xLua | 登录、好友、聊天等业务控制器 |
| 网络通信 | TCP Socket | 连接管理、收发与分包 |
| 消息协议 | Google Protobuf | 业务请求、响应与消息序列化 |
| 界面系统 | UGUI | 面板管理与生命周期 |
| 资源管理 | AssetBundle | 依赖加载与资源引用管理 |
| 版本更新 | UnityWebRequest | 版本检查、差异下载等基础流程 |
| 模拟服务端 | C# MockServer | 本地业务联调与消息验证 |

## 三、项目结构

本仓库保留项目的核心源码目录，省略依赖库、场景、美术和生成资源。

```text
Assets/
├── Editor/
│   └── ABBuildEditor.cs          # AssetBundle 构建工具
├── Framework/
│   ├── GameManager.cs           # 客户端初始化入口
│   ├── UI/                      # UIManager、BasePanel、UpdatePanel
│   ├── Net/                     # TCP、消息路由、业务网络封装、MockServer、.proto
│   ├── Lua/                     # LuaManager 与脚本加载
│   ├── Resource/                # AssetBundle、版本检查、资源句柄
│   ├── Config/                  # 配置读取
│   ├── Event/                   # EventCenter 事件中心
│   └── Data/                    # DataManager 玩家数据
├── Game/                        # 登录、注册、大厅、好友、私聊等 UI 脚本
└── Lua/Controller/              # 登录、注册、好友、聊天 Lua 控制器
```

文件筛选范围见 [SOURCE_FILES.txt](SOURCE_FILES.txt)。

## 四、客户端架构与消息流程

### 4.1 分层思路

- **UI 层**：负责界面展示、按钮操作和面板生命周期，不直接处理底层 Socket 数据。
- **Lua 业务层**：负责登录、好友、聊天等业务流程，调用 C# 提供的网络接口。
- **网络层**：使用 NetManager 收发消息、MessageRouter 按消息 ID 分发、GameNetwork 进行 Protobuf 协议转换。
- **数据与事件层**：使用 DataManager 保存当前玩家数据，通过 EventCenter 进行跨模块通知。
- **资源层**：通过 ResourceManager、VersionManager 等管理资源加载和版本更新。
- **测试服务端**：MockServer 为本地业务验证服务，不属于正式线上服务器。

### 4.2 登录与通信流程

```text
玩家操作：LoginPanel / HallPanel / ChatPanel
                    |
                    v
          Lua 业务 Controller
                    |  同 EventCenter / DataManager 协作
                    v
               GameNetwork
                    |  Protobuf 编码 / 解码
                    v
              MessageRouter
                    |  消息 ID 路由
                    v
               NetManager
                    |  TCP 长连接 / 自定义消息帧
                    v
            MockServer（测试用途）
```

以登录为例：登录面板读取用户输入，Lua 登录控制器调用 GameNetwork 发送登录请求；网络层发送 Protobuf 消息，接收响应后按照消息 ID 分发；登录成功时写入玩家数据并打开大厅界面。

### 4.3 网络协议

客户端使用带长度前缀的二进制帧封装业务消息，并以 Protobuf 定义请求和响应的数据结构。

公开协议文件包括：

- [login.proto](Assets/Framework/Net/Proto/login.proto)：登录、注册和玩家数据。
- [friend.proto](Assets/Framework/Net/Proto/friend.proto)：好友列表、申请与关系处理。
- [chat.proto](Assets/Framework/Net/Proto/chat.proto)：私聊消息与历史记录请求。
- [common.proto](Assets/Framework/Net/Proto/common.proto)：通用通信消息。

## 五、资源管理与热更新

项目实现了基于 AssetBundle 的资源加载管理，并搭建了版本更新的基础流程：

```text
客户端启动
    ↓
读取本地版本 / 检查远端版本
    ↓
读取与比较资源清单
    ↓
下载需要更新的资源
    ↓
写入本地更新目录
    ↓
后续资源和 Lua 脚本按加载策略读取
```

其中涉及 AssetBundle 依赖关系、引用计数、资源句柄以及多来源 Lua 脚本加载。

**现阶段限制：** 文件内容哈希校验、异常下载恢复、失败回滚和运行中 Lua 模块缓存失效仍需完善或验证。因此，这里展示的是**基础热更新实现与架构实践**，不宣称已经达到稳定商用热更新标准。

## 六、如何查看与运行

### 查看源码（推荐）

可以直接阅读以下几个入口：

1. [GameManager.cs](Assets/Framework/GameManager.cs)：客户端模块初始化。
2. [UIManager.cs](Assets/Framework/UI/UIManager.cs) 与 [BasePanel.cs](Assets/Framework/UI/BasePanel.cs)：面板管理和生命周期。
3. [NetManager.cs](Assets/Framework/Net/NetManager.cs)、[MessageRouter.cs](Assets/Framework/Net/MessageRouter.cs)、[GameNetwork.cs](Assets/Framework/Net/GameNetwork.cs)：网络传输、消息路由与业务协议。
4. [LuaManager.cs](Assets/Framework/Lua/LuaManager.cs) 与 [Lua/Controller](Assets/Lua/Controller)：C# / Lua 协作。
5. [ResourceManager.cs](Assets/Framework/Resource/ResourceManager.cs) 与 [VersionManager.cs](Assets/Framework/Resource/VersionManager.cs)：资源加载与版本更新。
6. [MockServer.cs](Assets/Framework/Net/MockServer.cs)：模拟服务端逻辑。

### 运行限制

**本仓库不能直接作为完整 Unity 工程运行或构建 EXE。**

运行原项目还需要对应的 Unity 场景、Prefab、配置、生成的 Protobuf 类型、xLua 与 Protobuf 等依赖，以及有适当使用授权的资源。上述内容没有全部包含在公开源码快照中。

如果只想了解项目实际界面，请查看 [个人主页 MOBA 项目实机截图](https://github.com/shaobaichen)。

## 七、已知问题与后续改进

本项目定位为学习、实践和作品展示，**不属于经过安全审计的生产级客户端或服务端系统**。

| 方向 | 目前的限制或改进点 |
| --- | --- |
| TCP 网络 | 固定接收缓存、心跳检测与重连流程仍需健壮性修复 |
| 更新可靠性 | 需要真实内容哈希、完整性校验及下载失败恢复 |
| 账号安全 | 已移除登录面板打印密码的调试日志，但当前测试协议中的 `PasswordHash` 字段仍直接传递输入密码，不适用于正式账号认证 |
| Lua 热更新 | 运行中模块重载、缓存失效及更新成功验证仍需补充 |
| MockServer | 权限验证、共享状态并发安全和聊天持久化能力不足，不应作为生产服务端 |
| 测试覆盖 | 仍需针对弱网、断线、异常更新与双客户端消息交互开展系统性验收 |

这些问题也代表了后续可以继续优化的方向：将已有功能从“能演示”提升到“可靠、可测试、可维护”。

## 八、项目用途与版权说明

本仓库用于展示个人在 **Unity 客户端开发、网络通信、Lua 业务逻辑及资源系统** 方面的实践，不包含完整可分发游戏。

- 已有的第三方美术、插件、二进制库等不在此仓库中再分发，相关权利仍归各自权利人所有。
- 本仓库**尚未添加开源许可证**。代码公开可阅读，并不自动表示允许复制、修改、再发布或商用。
- 实际 Unity 游戏画面请参考 [GitHub 个人主页](https://github.com/shaobaichen)，展示图与可运行工程、公开源码快照需要明确区分。
