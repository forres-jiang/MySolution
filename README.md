# My 后端项目模板

基于 .NET 10 和 ASP.NET Core 的分层后端模板，解决方案为 `MySolution.sln`，项目统一使用 `My.XXX` 命名。当前代码围绕菜单、角色权限和身份会话实现业务功能，并提供数据库迁移、健康检查、可观测性及自动化测试。

## 已实现功能

- **菜单管理**：新增、修改、删除、分页查询、搜索、菜单树、显示菜单树、排序和层级调整。
- **角色菜单分配**：单项或批量勾选、替换、移除，以及按一个或多个角色查询带勾选状态的菜单树。
- **权限管理**：查询稳定权限码目录、读取和替换角色权限；菜单分配与接口权限使用独立模型。
- **身份与会话**：区分访问令牌和刷新令牌；校验数据库会话状态、轮换刷新令牌、撤销会话，并读取当前用户信息。
- **授权约束**：HTTP 边界和应用用例均执行授权检查，业务规则由领域层负责。
- **运行支撑**：统一响应与异常处理、跨域、刷新接口限流、可选 Redis 权限缓存、健康检查和遥测。

当前没有提供登录、注册或完整用户与角色管理接口。初次身份认证、用户和会话初始化需要根据业务补充，不能将此模板直接视为完整账号系统。

## 项目结构

| 项目或目录 | 职责 |
| --- | --- |
| `My.XXX.APIs` | 程序入口、控制器、HTTP 契约、JWT 认证、授权适配、中间件及依赖装配 |
| `My.XXX.Application` | 菜单、权限、会话等应用用例，以及存储和外部能力的接口定义 |
| `My.XXX.Domain` | 菜单层级、排序、删除约束、权限码与领域结果；不依赖其他项目 |
| `My.XXX.Persistence` | 基于 LINQ to DB 的数据库访问、事务、持久化模型、迁移和数据库健康检查 |
| `My.XXX.Infrastructure` | Redis 缓存、AES 加密、会话指标等技术实现 |
| `My.XXX.ServiceDefaults` | 服务发现、HTTP 弹性与通用健康端点，不依赖业务项目 |
| `My.XXX.AppHost` | 本地资源编排、配置注入与迁移完成后的服务启动 |
| `My.XXX.UnitTests` | 领域规则、用例授权和架构边界测试 |
| `My.XXX.IntegrationTests` | 数据库事务、迁移、缓存、配置、HTTP 契约和遥测测试 |
| `observability` | OpenTelemetry Collector 配置示例 |

应用层依赖领域层，通过接口调用存储及外部能力；持久化层和基础设施层提供接口实现，API 项目负责组合各层。HTTP 请求模型、应用模型、领域模型和数据库模型分别定义，通过 Mapperly 等映射代码转换。

## 技术栈

- .NET 10、ASP.NET Core、JWT Bearer、Swagger / OpenAPI。
- LINQ to DB，支持 SQL Server 和 PostgreSQL，默认选择 PostgreSQL。
- StackExchange.Redis、FluentResults、Mapperly、Scrutor。
- Serilog、OpenTelemetry，支持日志、链路追踪、指标和可选 OTLP 导出。
- MSTest、Coverlet，以及基于 .NET 10 Alpine 镜像的多阶段 Docker 构建。

依赖版本集中定义在 `Directory.Packages.props`；`global.json` 指定 SDK 起始版本为 `10.0.100`，允许滚动到更高的 .NET 10 功能版本。

## 本地启动

### 方式 A（推荐）：Aspire AppHost 一键启动

需要本机已安装并启动 Docker Desktop、Rancher Desktop 或兼容的容器引擎。首次运行前，在 AppHost 的用户机密中保存稳定密钥（JWT 密钥至少 32 个 UTF-8 字节；加密密钥为 32 字节的 Base64 编码）：

```powershell
dotnet user-secrets set 'Parameters:jwt-secret' '<JWT签名密钥>' --project My.XXX.AppHost/09My.XXX.AppHost.csproj
dotnet user-secrets set 'Parameters:encryption-key' '<32字节密钥的Base64编码>' --project My.XXX.AppHost/09My.XXX.AppHost.csproj
```

随后启动 AppHost，自动拉起 PostgreSQL、Redis、数据库迁移进程、API 和 Aspire Dashboard：

```powershell
dotnet run --project My.XXX.AppHost/09My.XXX.AppHost.csproj
```

启动后浏览器会自动打开 Aspire Dashboard（默认 `http://localhost:17xxx`），可查看资源状态、Traces、Metrics、Logs。API 端点会在 Dashboard 中列出，点击即可访问 Swagger。

AppHost 将数据库连接直接注入 `ConnectionStrings__Default`，将 Redis 连接注入 `RedisConfig__ConnectionString`，并显式选择 PostgreSQL 与 Redis 权限缓存。迁移进程只依赖数据库，API 等迁移成功退出后启动；Redis 启动或运行故障允许权限查询回退数据库。正常手动启动 API 仍不会自动迁移。

### 方式 B：手动启动（不依赖容器）

### 1. 准备环境

安装兼容的 .NET 10 SDK，并准备可连接的 SQL Server 或 PostgreSQL 数据库。只有启用 Redis 权限缓存时才需要 Redis。

在仓库根目录执行：

```powershell
dotnet restore MySolution.sln
dotnet build MySolution.sln
```

### 2. 设置配置

仓库中的开发配置没有填写数据库连接和 JWT 密钥，需要通过环境变量或其他配置提供程序注入。以下是 PowerShell 示例，请替换数据库连接中的占位值：

```powershell
$env:ASPNETCORE_ENVIRONMENT = 'Development'
$env:DatabaseProviders__Default = 'PostgreSQL'
$env:ConnectionStrings__Default = 'Host=localhost;Port=5432;Database=mydb;Username=postgres;Password=<数据库密码>'
$env:JwtConfig__Secret = [Convert]::ToBase64String([System.Security.Cryptography.RandomNumberGenerator]::GetBytes(32))
$env:APP_ENCRYPTION_KEY = [Convert]::ToBase64String([System.Security.Cryptography.RandomNumberGenerator]::GetBytes(32))
```

上面的随机密钥适合首次本地配置。持续运行或部署时应保存并重复使用密钥，尤其是用于解密已持久化数据保护密钥的 `APP_ENCRYPTION_KEY`。

| 配置项 | 含义与约束 |
| --- | --- |
| `DatabaseProviders:Default` | `SqlServer` 或 `PostgreSQL`，默认 `PostgreSQL` |
| `ConnectionStrings:Default` | 必填业务数据库连接字符串；支持明文或 `enc:v1:` 加密格式 |
| `JwtConfig:Secret` | JWT 签名密钥，至少 32 个 UTF-8 字节 |
| `JwtConfig:Issuer` / `Audience` | 必填签发者与受众；开发配置已提供示例值 |
| `JwtConfig:ExpiryInMinutes` | 访问令牌有效期，开发配置为 20 分钟 |
| `JwtConfig:RefreshExpiryInMinutes` | 刷新有效期，开发配置为 43200 分钟，必须大于访问有效期 |
| `APP_ENCRYPTION_KEY` | 必填，32 字节密钥的 Base64 编码；用于 AES-GCM 和数据保护密钥加密 |
| `AppConfig:PermissionDataCache` | `0` 不启用权限缓存，`1` 使用 Redis |
| `RedisConfig:ConnectionString` | Redis 连接，支持明文或 `enc:v1:` 加密格式 |
| `PermissionCache:KeyPrefix` / `ExpiryInMinutes` | 缓存键前缀及有效期，默认有效期 5 分钟 |
| `AllowedHostArray` | 跨域允许的来源列表 |
| `DataProtection:KeyPath` | 数据保护密钥目录，默认位于应用目录的 `keys` 下 |

### 3. 执行迁移

先创建目标数据库，再执行迁移：

```powershell
dotnet run --project My.XXX.APIs/01My.XXX.APIs.csproj --no-launch-profile -- --migrate
```

迁移分别提供 SQL Server 与 PostgreSQL 脚本，建立菜单、角色菜单、权限、用户、会话及并发控制等表结构。迁移程序使用数据库锁、迁移记录及校验和，避免并发执行或修改已经应用的脚本。

正常启动不会自动迁移。新增数据库变更应添加新的迁移脚本。

### 4. 启动 API

```powershell
dotnet run --project My.XXX.APIs/01My.XXX.APIs.csproj --no-launch-profile -- --urls http://localhost:5000
```

开发环境可访问 `http://localhost:5000/swagger`。开发启动配置默认打开 Swagger，生产与容器启动配置使用存活检查地址。

## 接口概览

| 路径 | 功能 |
| --- | --- |
| `/api/Menu/*` | 菜单增删改查、分页与搜索、菜单树、角色菜单分配、排序 |
| `GET /api/v2/permissions/catalog` | 查询权限码目录 |
| `GET /api/v2/permissions/roles/{roleId}` | 查询角色权限 |
| `PUT /api/v2/permissions/roles/{roleId}` | 替换角色权限 |
| `POST /api/User/RefreshToken` | 使用刷新令牌续期并轮换令牌 |
| `DELETE /api/User/Session` | 撤销当前会话 |
| `GET /api/User/GetRoles` | 返回当前用户信息 |
| `GET /healthy` | 匿名存活检查，不检查外部依赖 |
| `GET /ready` | 匿名就绪检查，检查数据库、所需迁移与表结构 |
| `GET /health` | 匿名完整诊断，启用缓存时包含 Redis；Redis 故障报告 Degraded，默认返回 HTTP 200 |
| `GET /alive` | 匿名存活检查，与 `/healthy` 同样不依赖外部服务 |

业务接口默认要求身份认证，菜单和权限接口还校验对应权限码。HTTP 与应用用例的授权检查在同一请求作用域复用用户和角色集合对应的权限快照；身份、角色变化或当前作用域内成功修改权限后重新读取。其他请求的权限变更在下一请求生效。后台调用须为每次工作建立独立作用域。刷新接口使用独立的 `Refresh` 认证方案。令牌验证同时检查数据库中的会话和当前用户状态。

通用响应采用 `APIResponse<T>`，包含 `StatusCode`、`Message`、`Data`、`Code` 和 `TraceId`；分页数据使用 `PagedResponse<T>`。响应体中的业务状态码与 HTTP 状态码需要分别理解。撤销会话成功返回 HTTP 204。

## 测试

```powershell
dotnet test My.XXX.UnitTests/06My.XXX.UnitTests.csproj
dotnet test My.XXX.IntegrationTests/07My.XXX.IntegrationTests.csproj
```

部分数据库集成测试需要本机 PostgreSQL 工具。测试夹具默认查找 Windows 的 `C:\Program Files\PostgreSQL\15\bin`，也可指定：

```powershell
$env:MY_TEST_POSTGRES_BIN = 'C:\Program Files\PostgreSQL\15\bin'
```

夹具通过 `initdb` 和 `pg_ctl` 创建隔离的本地数据库集群，使用动态端口，在 `.test-artifacts` 下保留数据和日志用于排查。未找到工具时，相关测试标记为无法判定，不代表验证通过。

## 部署与可观测性

仓库的 GitHub Actions 流水线会构建解决方案并运行单元、架构与集成测试；运行器安装 PostgreSQL 工具，数据库测试使用隔离集群。分支保护规则需在托管平台中将此检查设为合并条件。

```powershell
docker build -t my-api .
```

Dockerfile 使用多阶段构建，运行镜像为 .NET 10 Alpine，安装 ICU 与时区数据，以 `app` 用户运行并暴露 8080 端口。运行时需要注入环境配置，并为数据保护密钥目录提供可写、持久化的存储。

生产模式按 TLS 在入口代理终止的方式设计，应用启用转发头和 HSTS。Swagger 只在开发环境开启。

`OpenTelemetry:Enabled` 默认开启，`OpenTelemetry:OtlpEnabled` 默认关闭；启用导出后可使用标准 `OTEL_*` 环境变量配置收集端。`TraceSampleRatio` 控制追踪采样比例，范围为 0 到 1。采集涵盖 HTTP、数据库、运行时及会话和授权相关指标；健康检查路径不纳入 HTTP 追踪。收集器配置示例见 `observability/otel-collector.yaml`。

## 开发约定与许可证

文档与代码注释使用中文；C# 类型、方法和公共属性使用 PascalCase，局部变量使用 camelCase。异步 I/O 使用 `async/await` 并传递 `CancellationToken`，新文件优先使用文件范围命名空间，具体约定见 `AGENTS.md`。

项目采用 MIT 许可证，详见 `LICENSE`。
