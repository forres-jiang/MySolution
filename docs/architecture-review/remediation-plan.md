# 整改计划

> 历史整改计划，保留当时的发现与建议，不作为当前待办清单。2026-10-23 已完成的优化及验证见 [优化记录](optimization-2026-10-23.md)，当前行为见 [架构说明](../Architecture.md)。

- **配套文档**：`architecture-review.md`（发现与证据）、`quality-scenarios.md`（验收场景）
- **产出技能**：`architecture-visualization:risk-quality-reviewer`
- **日期**：2026-10-12

## 排序依据

按四个维度排序，而非单纯按严重度：

1. **阻塞性**——是否让其他整改无法验证
2. **可逆性**——低风险可逆动作优先，需要架构决策的推后
3. **成本/收益比**——先摘低垂果实，把注意力留给真正难的问题
4. **放大效应**——模板缺陷会被下游复制，因此模板卫生类问题等级上调

**关键排序约束**：P0 必须先于 P1。先建 CI 会立即变红，容易被误判为"CI 有问题"而被关闭；先修红再建闸门，CI 从第一天起就是绿的，才能建立"红 = 真问题"的信任。

负责人一栏为**假设**，实际归属需按团队职责确认。

---

## P0 — 立即修复（成本极低，解除后续所有验证阻塞）

### P0-1 修复失败的集成测试

| 项 | 内容 |
| --- | --- |
| 对应发现 | H1（`architecture-review.md`）、场景 1 与 10 |
| 动作 | 在 `PermissionCacheTests.CacheToggleKeepsTheApplicationServiceAndItsResults` 的 `ServiceCollection` 上补注册日志 |
| 具体改动 | `My.XXX.IntegrationTests/PermissionCacheTests.cs:27` 之后增加 `services.AddLogging();`（需 `using Microsoft.Extensions.Logging;`，该文件第 6 行已引入）。这与同文件 `Decorate()` 辅助方法（`:84-85`）显式传 `NullLogger` 的做法在语义上一致——都是为测试提供日志实现，只是一个走 DI 一个走直接构造 |
| 备选方案 | 若不希望测试依赖 `AddLogging()`，可改为 `services.AddSingleton<ILogger<CachedPermissionStore>>(NullLogger<CachedPermissionStore>.Instance)`，意图更显式 |
| 验收标准 | `dotnet test My.XXX.IntegrationTests` → **0 失败**；该用例断言的缓存开关行为（`enabled ? 1 : 2` 次读取，`:39`）仍然成立 |
| 工作量 | < 15 分钟 |
| 可逆性 | 完全可逆，仅影响测试装配 |
| 负责人（假设） | 提交该 Redis 降级变更的作者 |
| 注意 | **不要**为了让测试通过而移除 `CachedPermissionStore` 的 `ILogger` 依赖——那会同时移除 M7 之外唯一有价值的降级日志。修测试，不修产品代码 |

### P0-2 修正架构文档中的 Redis 降级描述

| 项 | 内容 |
| --- | --- |
| 对应发现 | H2、场景 2 |
| 动作 | 更新 `docs/Architecture.md:150` 使其与代码一致 |
| 具体改动 | 当前文本："这里的数据库回退针对修订号持续变化。`CachedPermissionStore` 没有捕获 Redis 读写异常并自动降级的逻辑；启用缓存时 Redis 同时纳入就绪检查。"<br>应改为描述两层回退：(1) 修订号持续变化时最多重试 3 轮后直读数据库（`CachedPermissionStore.cs:42-55`）；(2) Redis 读写异常被捕获并降级为数据库直读，写入失败则跳过本次缓存（`:58-83`），两者均递增 `permissions.database.fallbacks` 计数器并记录警告 |
| 附带核查 | 本次审查仅逐条验证了与已发现缺陷相关的文档段落。建议同时复核 §7 其余描述与 `CachedPermissionStore.cs` 是否一致，以及 §5 的锁语义描述与 `AccessControlTransaction.cs` 是否一致 |
| 验收标准 | 文档描述与代码行为逐条对得上；一位未参与本次审查的工程师读完 §7 后能正确回答"Redis 挂了会怎样" |
| 工作量 | < 30 分钟（不含附带核查） |
| 可逆性 | 完全可逆 |
| 负责人（假设） | 文档维护者 |

---

## P1 — 建立强制闸门（让 P0 的成果不再退化）

### P1-1 建立 CI 流水线

| 项 | 内容 |
| --- | --- |
| 对应发现 | H1、M1、场景 1、场景 10 |
| 动作 | 新增 `.github/workflows/ci.yml`（或组织实际使用的 CI 系统） |
| 必须包含的步骤 | 1. `dotnet build MySolution.sln` 并**将警告视为失败**（当前构建 0 警告 0 错误，这是可以立即锁住的基线）<br>2. `dotnet test My.XXX.UnitTests` → 27 个必须全绿<br>3. `dotnet test My.XXX.IntegrationTests`，并提供 **PostgreSQL 服务容器**使 8 个跳过的测试真正执行（见 P1-2）<br>4. 架构守卫测试（`ArchitectureTests`、`ApplicationBoundaryTests`、`ArchitectureBoundaryTests`）失败必须阻断合并 |
| 验收标准 | 一次故意引入的分层违规 PR 被 CI 拒绝；一次故意让测试失败的 PR 被 CI 拒绝 |
| 工作量 | 0.5–1 天 |
| 可逆性 | 可逆（删除 workflow 文件即可），但会失去守卫 |
| 负责人（假设） | 平台/DevOps |
| 依赖 | **必须在 P0-1 之后**，否则 CI 从第一天就是红的 |

### P1-2 让数据库测试在 CI 中真正执行

| 项 | 内容 |
| --- | --- |
| 对应发现 | M1、场景 3、场景 4 |
| 动作 | 在 CI 中提供 PostgreSQL，使 `PostgresFixture` 不再走 `Assert.Inconclusive` 分支 |
| 两种路径 | **路径 A（改动小）**：CI 中安装 PostgreSQL 客户端工具并设置 `MY_TEST_POSTGRES_BIN`，沿用现有 `initdb`/`pg_ctl` 隔离集群方案（`PostgresFixture.cs:26-29`）<br>**路径 B（更标准）**：用 CI 服务容器提供 PostgreSQL 实例，并为夹具增加"连接外部实例"的模式，跳过本地集群创建 |
| 附带决策 | SQL Server 侧目前**没有**对应的 CI 覆盖。项目声称双数据库支持且脚本成对等价，但只有 PG 能被自动验证。建议至少在 CI 中加一个 SQL Server 容器的迁移冒烟测试，否则"双数据库等价"这一主张只有静态比对支撑 |
| 验收标准 | CI 日志显示 29 个集成测试**全部执行**，跳过数为 0；`MigrationsAreRepeatableAndVerified` 等 8 项有真实结果 |
| 工作量 | 0.5–2 天（取决于路径选择） |
| 可逆性 | 可逆 |
| 负责人（假设） | 平台/DevOps + 后端 |

### P1-3 为 Redis 降级路径补测试

| 项 | 内容 |
| --- | --- |
| 对应发现 | H2、场景 2 |
| 动作 | 补一个不需要真实 Redis、也不需要数据库的单元测试 |
| 具体改动 | 在 `PermissionCacheTests.cs` 中新增一个 `ThrowingCache` 桩实现 `IPermissionCache`（`GetAsync` 与 `SetAsync` 抛异常），断言：(1) `GetPermissionCodesAsync` 仍返回数据库中的正确权限码；(2) 不抛异常给调用方；(3) 复用现有 `Decorate()` 辅助方法（`:84-85`）构造被测对象 |
| 为何优先级高 | 成本约 30 分钟，却把 H2 涉及的**真实运行时行为**从"零覆盖"变为"有覆盖"。当前 `Cache` 桩（`:99-116`）永不抛异常，意味着 `CachedPermissionStore.cs:58-83` 的 catch 分支从未被执行过——这正是文档能长期与代码矛盾而无人发现的原因 |
| 验收标准 | 新测试通过；且人为删除 `TryGetCachedAsync` 的 catch 块后该测试失败 |
| 工作量 | < 1 小时 |
| 可逆性 | 完全可逆 |
| 负责人（假设） | 后端 |

---

## P2 — 模板卫生（成本低，放大效应高）

### P2-1 移除内部基础设施标识

| 项 | 内容 |
| --- | --- |
| 对应发现 | M6 |
| 动作 | 删除 `My.XXX.APIs/appsettings.Production.json:9` 的注释 |
| 具体改动 | 移除 `/*Rancher内部调用Redis的地址和端口 fcs-redis-service.commoncache.svc.cluster.local:6379*/`。若需保留说明，改为中性示例：`/*示例: redis-host:6379,password=<从密钥提供方注入>*/` |
| 附带核查 | 本次扫描仅覆盖基础设施标识模式（`svc.cluster.local`、`rancher`、`fcs-`、内网 IP 段等），命中仅此一处。建议对源组织的**业务术语、内部项目代号、人名、内部域名**再做一次针对性扫描——这类内容不在通用模式内 |
| 验收标准 | 扫描无命中；模板中不再出现任何指向特定组织内部环境的标识 |
| 工作量 | < 15 分钟（不含附带核查） |
| 可逆性 | 完全可逆 |
| 负责人（假设） | 模板维护者 |
| 备注 | 若该模板**从未对外发布**，严重度可下调至低。但既然是 MIT 许可且 README 面向复用者撰写，应按对外发布处理 |

### P2-2 清理未使用的 NuGet 包

| 项 | 内容 |
| --- | --- |
| 对应发现 | M7 |
| 动作 | 从 `Directory.Packages.props` 与各 `.csproj` 移除零引用的包 |
| 待移除 | `Azure.Identity` (1.21.0)、`Polly` (8.8.0)、`Microsoft.Extensions.Http.Polly` (10.0.12)、`System.Security.Cryptography.Pkcs` (10.0.12)、`Serilog.Sinks.Splunk` (5.2.0) |
| 保留 | `Newtonsoft.Json`（5 处使用）、`Microsoft.Extensions.Caching.Memory`（`InfrastructureRegistration.cs:13`）、`StackExchange.Redis`、`FluentResults`、`Riok.Mapperly`、`Scrutor`、`linq2db.*`、OpenTelemetry 系列、Serilog.AspNetCore/Expressions、Swashbuckle |
| 附带改动 | `HttpPolicyRegistration.cs` 文件名暗示弹性策略但实际只含 CORS/ForwardedHeaders/RateLimiter，移除 Polly 后建议重命名为 `HttpBoundaryRegistration.cs` 或 `HttpMiddlewareRegistration.cs`，避免误导 |
| 决策点 | 移除 `Azure.Identity` 前需确认：是否有意保留以支持下游接入 Azure Key Vault？若是，应在 README 的技术栈段落说明其用途，而不是无声地留在依赖清单里 |
| 验收标准 | `dotnet build` 仍为 0 警告 0 错误；`dotnet test` 全绿；`dotnet restore` 还原的包数量减少 |
| 工作量 | < 1 小时 |
| 可逆性 | 完全可逆（版本集中在 `Directory.Packages.props`，恢复只需加回一行） |
| 负责人（假设） | 模板维护者 |

### P2-3 修复 Serilog 增强器静默失效

| 项 | 内容 |
| --- | --- |
| 对应发现 | M7、场景 8 |
| 动作 | 二选一：补齐包，或删除配置。**不要保持现状**（声明了但无效） |
| 选项 A（推荐） | 在 `Directory.Packages.props` 增加 `Serilog.Enrichers.Environment`、`Serilog.Enrichers.Process`、`Serilog.Enrichers.Thread`，并在 `My.XXX.APIs/01My.XXX.APIs.csproj` 引用。多实例容器部署下机器名与进程 ID 是排查问题的必要上下文 |
| 选项 B | 从三个 `appsettings.{Development,Production,Staging}.json` 的 `Enrich` 段移除这三项，只保留 `FromLogContext`。适用于已依赖 OpenTelemetry 资源属性做实例标识的情况 |
| 附带修复 | 同一 Serilog 配置段中：`"path": "logs/.txt"` 缺少文件名前缀（应为如 `logs/api-.txt`）；`"retainedFileCountLimit": "90"` 用字符串而非数字（Serilog 容忍但不规范）。三处配置文件需一并核对 |
| 验收标准 | 启动应用产生一条日志，输出中可见 `MachineName`/`ProcessId`/`ThreadId`（选项 A）或配置中不再声明这些增强器（选项 B）；三个环境配置一致 |
| 工作量 | < 1 小时 |
| 可逆性 | 完全可逆 |
| 负责人（假设） | 模板维护者 |

---

## P3 — 需要环境或设计的整改（成本较高，收益随规模增长）

### P3-1 解耦就绪探针与业务写锁

| 项 | 内容 |
| --- | --- |
| 对应发现 | M3、场景 5 |
| 前置调查 | **先确认数据库隔离级别**：`SELECT is_read_committed_snapshot_on FROM sys.databases WHERE name = DB_NAME()`（SQL Server）或 `SHOW default_transaction_isolation`（PG）。若已启用 RCSI，SELECT 不被 X 锁阻塞，本项可降级为低优先级 |
| 动作 | 让 `/ready` 的表结构检查不读取被业务写锁定的行 |
| 候选方案 | (a) `MenuSchemaHealthCheck.cs:18` 改为查询元数据（`INFORMATION_SCHEMA`/`sys.tables`）而非 `SELECT` `MenuWriteLock` 数据行；(b) 用 `NOLOCK`/`READ UNCOMMITTED` 执行结构探测（结构检查不要求一致性读）；(c) 缩短 `UpdateSort` 事务——改逐行 UPDATE 为集合式 UPDATE |
| 验收标准 | 开启一个持续 30 秒的菜单写事务，期间 `/ready` 持续返回 200 且延迟 < 100ms |
| 工作量 | 0.5–1 天（含真实数据库实测） |
| 可逆性 | 可逆 |
| 负责人（假设） | 后端 + 运维 |

### P3-2 修正菜单表建模与索引

| 项 | 内容 |
| --- | --- |
| 对应发现 | M2、场景 9 |
| 动作 | 通过**新迁移脚本**（`MigrationRunner` 是单向的，见 L9）修正列类型并补索引 |
| 具体改动 | (a) `DisplayName` 由 `nvarchar(max)` 改为有界长度（建议 `nvarchar(200)`，需先确认业务上限）；`CreatedBy` 改为 `nvarchar(100)`；(b) 为 `Menus` 增加 `ParentId`、`IsDeleted`、`CreatedAt` 索引；(c) 双数据库脚本成对新增，保持等价 |
| 决策点 | 子串搜索（`LIKE '%..%'`）无法靠 B-tree 索引加速。需显式决策：接受全表扫描（菜单表通常不大）、限制为前缀匹配、还是引入全文索引。**不要无声地保留当前行为**——下游会在数据量增长后才发现 |
| 附带考量 | 列类型收窄属于**破坏性迁移**：若现有数据超长会失败。模板阶段无存量数据，成本低；下游采用后成本上升。**这是应当尽早做的理由** |
| 验收标准 | 迁移在 SQL Server 与 PostgreSQL 上均成功且可重复；5 万行数据下搜索 p95 < 200ms（建议基线，需与业务确认） |
| 工作量 | 1–2 天（含双库验证与数据量测试） |
| 可逆性 | 索引可逆；列类型收窄**部分不可逆**（超长数据已被截断/迁移失败） |
| 负责人（假设） | 后端 |

### P3-3 为权限白名单增加可观测性

| 项 | 内容 |
| --- | --- |
| 对应发现 | M5、场景 7 |
| 动作 | 让白名单从"静默放权"变为"可见放权" |
| 具体改动 | (a) `UseCaseAuthorization.cs:33-34` 经白名单放行时记录警告日志（含用户 ID 与权限码），并递增一个自定义计数器（项目已有 `permissions.*` 指标体系可复用）；(b) 启动时若 `PermissionWhitelist:Codes` 非空，输出显著警告；(c) 可选：为白名单增加"仅非生产环境生效"的约束 |
| 不建议 | 不要为白名单引入复杂的审批流——它是模板的调试便利设施，过度设计会让下游直接删掉它 |
| 验收标准 | 配置一个非空白名单启动，日志与指标中均可见；经白名单放行的授权决策可被审计查询到 |
| 工作量 | 0.5 天 |
| 可逆性 | 可逆 |
| 负责人（假设） | 后端 |

---

## P4 — 低风险清理

| 编号 | 动作 | 具体位置 | 工作量 |
| --- | --- | --- | --- |
| P4-1 | 删除空目录 | `My.XXX.APIs/Legacy/`（无任何文件） | 5 分钟 |
| P4-2 | 修正启动 URL | `launchSettings.json:6,15,24` 的 `api/Demo/Index` → `swagger`。模板首次运行不应 404 | 10 分钟 |
| P4-3 | 移除死中间件 | `Program.cs:103` 的 `app.UseStaticFiles()`——`wwwroot` 不存在。若有意保留以支持下游放静态文件，应同时创建 `wwwroot` 并加注释说明 | 10 分钟 |
| P4-4 | 修正可空性声明 | `AuthenticationUser.cs:10-11` 的 `UserId` 声明为 `string?` 却标注 NotNull，二者矛盾。按实际语义择一，符合 `AGENTS.md`「正确声明可空类型」 | 15 分钟 |
| P4-5 | 在约定中写明测试例外 | `AGENTS.md` 禁止 `null!`，但 `CleanArchitectureTests.cs:36-39,50` 有意使用（`:33` 已注释说明用途）。建议在约定中补一句"授权测试可用 `null!` 断言存储未被访问"，消除规则与实操的矛盾 | 10 分钟 |
| P4-6 | 核对生产 Host 过滤 | `appsettings.Production.json` 的 `"AllowedHosts": "*"`。确认是否由入口代理负责 Host 校验；若否，改为显式域名列表 | 30 分钟（含确认） |

---

## 需要决策、暂不建议动手的事项

这些不是遗漏，而是**取舍未定**。在缺少业务上下文时贸然整改的风险高于收益。

| 项 | 为何暂缓 | 需要先回答的问题 |
| --- | --- | --- |
| **M4** 全局单行锁的并发粒度 | `docs/Architecture.md:117` 已明确承认这是取舍，实现本身干净（固定加锁顺序、无死锁、无锁泄漏）。改为细粒度锁会显著增加复杂度与死锁风险 | 目标并发菜单写吞吐量是多少？是否有量化数据支撑"当前粒度不够"？ |
| **L10** 缺失外键约束 | 加外键能防孤儿数据，但会与软删除（`IsDeleted` + 过滤唯一索引）交互，级联语义需要设计 | 是否存在绕过 `IAccessControlTransaction` 的写入路径（运维脚本、数据修复、批量导入）？ |
| **L6** 刷新令牌重放检测 | 当前条件 UPDATE 已防并发重放；增加 reuse-detection（重放即撤销整个会话）是安全性与可用性的取舍——网络抖动导致的重试会误杀合法会话 | 是否接受"检测到旧 jti 即撤销全会话"？需要配套的日志与用户提示 |
| **L5** 密钥职责分离 | DataProtection 与配置加密共用 `APP_ENCRYPTION_KEY`。分离更安全，但增加运维需管理的密钥数量 | 部署环境是否有密钥管理服务可托管多把密钥？ |
| **L9** 迁移回滚能力 | 无 down 脚本是常见取舍（回滚通常靠备份恢复而非逆向 DDL）。但应在文档中**明确写出**这一取舍 | 下游的故障恢复策略是逆向迁移还是备份恢复？ |
| **L13** `ApplicationName` 传 `FullName` | 置信度 low。当前构建与全部测试均通过，未见实际故障。可能是刻意写法，也可能是隐患 | 需实测：`Assembly.FullName` 是否影响 `WebApplicationOptions` 的内容根或入口程序集解析 |

---

## 建议执行顺序

```
P0-1 修红测试 ──┬──> P1-1 建 CI ──> P1-2 CI 中跑通数据库测试
P0-2 修文档  ──┘         │
                         └──> P1-3 补 Redis 降级测试

P2-1 清内部标识 ┐
P2-2 清死包    ├──> 可并行, 互不依赖, 完成后 CI 验证
P2-3 修日志配置 ┘

P3-1 解耦就绪探针 (先做隔离级别调查)
P3-2 索引与列类型 (破坏性迁移, 尽早做)
P3-3 白名单可观测性

P4-* 随时可插入

决策项: 收集业务上下文后单独评审
```

**总计工作量估算**：P0 + P1 约 1.5–3 天（含 CI 搭建与 PG 环境）；P2 约半天；P3 约 2–4 天；P4 约 1.5 小时。

**最小可行整改**：若只能投入一天，做 **P0-1 + P0-2 + P1-1 + P1-3**。这四项完成后：测试全绿、文档与代码一致、闸门存在、最关键的未覆盖运行时行为有测试。架构守卫从"设计良好但失效"变为"设计良好且强制执行"——这是本次审查中收益最大的一步。
