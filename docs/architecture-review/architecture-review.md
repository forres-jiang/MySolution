# 架构风险与质量审查

> 本文为 2026-10-12 的历史评审快照，其中的路径、版本、测试结果与发现状态不代表当前代码。2026-10-23 优化已覆盖架构守卫、AppHost 配置、可选缓存探针、授权快照、窄事务接口与模型契约；后续维护以 [当前架构说明](../Architecture.md) 和 [优化记录](optimization-2026-10-23.md) 为准。旧文中的缓存日志依赖问题已修复，管理员与白名单放行描述已不适用。

- **审查对象**：`E:\SourceCode\MySolution`（.NET 10 分层后端模板，MIT 许可）
- **审查日期**：2026-10-12
- **产出技能**：`architecture-visualization:risk-quality-reviewer` + `graphviz`
- **审查目标**：判断该架构对其**声明的目标**（作为可复用的分层后端模板）是否足够健康，风险集中在何处
- **风险容忍度基线**：模板项目。缺陷的真正代价不是本项目出问题，而是**被复制到下游项目后放大**

## 阅读顺序

1. 本文（结论与证据）
2. `risk-map.dot`（风险与架构元素的关系图，Graphviz）
3. `quality-scenarios.md`（质量属性场景与验证方法）
4. `remediation-plan.md`（按优先级排序的整改动作）

## 范围与方法

| 维度 | 说明 |
| --- | --- |
| 规模 | 7 个项目，164 个 `.cs` 文件，约 6453 行 |
| 版本控制 | **不是 git 仓库**（`git rev-parse` 失败）。无法用 diff/PR/提交历史界定审查范围，也无法评估变更节奏 |
| 证据来源 | 源代码、`.csproj`/`.props`、`appsettings*.json`、`Dockerfile`、迁移 SQL、`docs/Architecture.md`、`README.md`，以及本机实际执行的构建与测试 |
| 实测结果 | `dotnet build` → 0 警告 0 错误；单元测试 → **27/27 通过**；集成测试 → **1 失败 / 20 通过 / 8 跳过** |
| 不在范围 | 运行时性能压测、生产配置强度、真实数据库行为（本机缺 PostgreSQL 工具，见 M1） |

置信度标记遵循架构契约：**high** = 直接代码/配置/实测证据；**medium** = 多个信号一致但无单一确证；**low** = 由命名或结构推断；**unknown** = 明确的待验证空白。

---

## 总体结论

**架构设计质量高，交付纪律存在缺口。**

这两件事必须分开评价，否则任一方向的结论都会误导。

设计层面，这个项目做了多数同类模板不做的事：**用可执行的测试把架构约束锁住**，而不是只在文档里描述。依赖方向、领域层纯净度、"控制器不得绕过用例直连存储"这三条最容易被侵蚀的边界，都有对应的自动化守卫（其中 IL 扫描那一条强度罕见）。安全实现经得起逐行核对，权限缓存的 revision-in-key 设计消除了我原本预期会找到的授权绕过窗口。

交付层面，仓库当前**带着一个失败的测试**，且**没有任何 CI 配置**。这两件事叠加，意味着上述所有守卫只在"有人记得手动运行"时才生效。更关键的是，同一个变更同时造成了三处不一致——代码新增了依赖、测试未跟进、架构文档未更新——而没有任何机制拦住它。这不是单点疏忽，而是缺少质量闸门的直接后果。

对模板项目而言，这个缺口的严重度要上调：下游使用者会假设"模板自带的测试是绿的"。

---

## 值得肯定的设计（同样是证据支撑的结论）

省略这部分会让整改建议失去参照系——需要知道哪些约束是**已经守住**的，改动时不能破坏。

| 做法 | 证据 | 为何重要 |
| --- | --- | --- |
| 依赖方向用测试锁住，而非仅靠约定 | `ArchitectureTests.cs:57-87` 解析 `.csproj` XML 校验 `ProjectReference` 白名单 | 分层腐化通常始于一次"临时"引用 |
| 领域层纯净度双重校验 | `ArchitectureTests.cs:15-25` 运行时断言 Domain 程序集只引用 `System.*`；`:80-85` 禁止 Domain/Application 引入 ASP.NET、EF、linq2db、Npgsql、SqlClient、Redis | 编译期与运行期都拦得住 |
| IL 级边界守卫 | `ArchitectureBoundaryTests.cs:29-104` 手写 IL 扫描，覆盖 async 状态机、局部变量、`ResolveMember` 解析的静态调用与服务定位 | 构造函数注入检查会被静态调用绕过，这里没有 |
| 内部用例入口不公开 | `ArchitectureTests.cs:28-35` 断言所有 `*Mutations` 类型 `IsVisible == false` | 防止辅助类被误用为用例入口 |
| 权限码单一来源 + 双层授权同一实现 | HTTP 层 `PermissionsHandler.cs:19` 与用例层共用 `UseCaseAuthorization.RequireAnyAsync`；权限码集中在 `Domain/Authorization/Policies/PermissionCodes.cs`；`PermissionCodes.AllSet` 只读（`ArchitectureTests.cs:43-45` 验证） | 避免两套权限清单漂移 |
| 授权先于存储访问 | `CleanArchitectureTests.cs:28-44` 传入 `null!` 存储，断言未授权调用返回业务失败 | 证明授权确实在任何数据访问之前执行 |
| **权限缓存无 TTL 绕过窗口** | `CachedPermissionStore.cs:44-45` 缓存键内嵌 revision；写事务在同一事务内原子推进 revision（`AccessControlTransaction.cs:61-63`）；读后复核 revision（`:49`），3 轮冲突后直读数据库（`:54-55`） | 权限变更后旧缓存键立即不可达，而非等 TTL 过期。这是本次审查中最强的单点设计 |
| JWT 验证参数严谨 | `TokenAuthentication.cs:42-54` 全项校验 + `ValidAlgorithms` 仅 HS256 + `ClockSkew.Zero` + `RequireExpirationTime`；access/refresh 用 audience 后缀（`:46`）与 `token_use`（`:59-63`）双重区分 | 无算法混淆、无令牌交叉使用 |
| 密钥缺失即启动失败，无硬编码 fallback | `TokenAuthentication.cs:19-30`、`AESHelper.cs:11-13`、`WebOptionsRegistration.cs:13`；各环境 `appsettings` 的 `Secret`/连接串均为空串 | 全库未发现弱密钥或默认密钥 |
| AES-GCM 实现正确 | `AESHelper.cs:37-49` 随机 12 字节 nonce、16 字节 tag、用后 `ZeroMemory`；`:54-57` 强校验 `enc:v1:` 前缀与最小长度 | 无 nonce 复用、无未认证加密 |
| 事务回滚语义完整 | `AtomicWrite.cs:14-27` 业务失败结果、异常、取消均回滚；取消后使用不受取消影响的令牌执行清理 | 取消路径的回滚常被遗漏 |
| 刷新令牌轮换有并发防护 | `AuthenticationStore.cs:58-64` 条件 UPDATE（`WHERE RefreshTokenId == expected`），并发双刷只有一方成功 | 避免刷新竞态产生两个有效令牌 |
| 迁移防篡改 | `MigrationRunner.cs:90` SHA-256 + CRLF 归一化校验和；`:43` 已应用脚本被改动即抛异常；`:26-27` PG advisory lock / SQL Server `sp_getapplock` | 迁移历史可信 |
| 双数据库脚本成对且等价 | 6 组 `.sqlserver.sql`/`.postgresql.sql` 逐一比对，表结构、过滤唯一索引、单行 CHECK 约束等价；仓储层纯 LINQ，grep `UPDLOCK/MERGE/NEWID/ISNULL/方括号标识符` 无命中 | provider 差异被隔离在 `DatabaseConfiguration.cs:23-35` |
| 失败语义诚实 | `PostgresFixture.cs:26-29` 缺工具时用 `Assert.Inconclusive` 而非假装通过；`README.md:135` 明确说明"不代表验证通过" | 跳过 ≠ 通过，这个区分很多人做错 |
| 文档主动声明自身局限 | `README.md:14`（无登录入口）、`:102`（`Demo/Index` 失效）、`docs/Architecture.md:115`（组合用例未暴露）、`:117`（锁粒度取舍） | 已知限制被写明，而非隐藏 |

---

## 发现清单

严重度针对**模板项目**校准：能被下游复制放大的问题，等级上调。

### H1 — 仓库带红测试，且无 CI 强制执行架构守卫

**严重度：高（流程）／运行时：无影响** · **置信度：high**

实测证据：

```
dotnet test My.XXX.IntegrationTests → 失败 1，通过 20，跳过 8，总计 29
失败用例：PermissionCacheTests.CacheToggleKeepsTheApplicationServiceAndItsResults
System.InvalidOperationException: Unable to resolve service for type
  'ILogger`1[My.XXX.Infrastructure.Caching.CachedPermissionStore]'
  while attempting to activate 'CachedPermissionStore'
    at PermissionCacheTests.cs:line 34
```

`ls .github` → 目录不存在。全仓库无任何 CI/CD 配置。

**根因链**（三步，全部有直接证据）：

1. `CachedPermissionStore.cs:13-14` 主构造函数声明 `ILogger<CachedPermissionStore> logger` 依赖
2. `PermissionCacheTests.cs:31` 用 `services.Decorate<IPermissionStore, CachedPermissionStore>()` 走**真实 DI 容器**解析
3. `ApplicationRegistration.cs:11-17` 的 `AddApplicationServices()` 不调用 `AddLogging()`，测试的最小化 `ServiceCollection` 中没有 `ILogger<T>` 注册

同文件另外 3 个测试通过私有辅助方法 `Decorate()`（`PermissionCacheTests.cs:84-85`）**显式传入** `NullLogger<CachedPermissionStore>.Instance`，因此不受影响。这说明：`logger` 依赖是后加的，改造时只更新了直接构造的测试路径，**走 DI 的那一条被漏掉，且没有机制发现它被漏掉**。

**为何不是运行时缺陷**：生产路径 `Program.cs:55-59` 使用 `WebApplication.CreateBuilder`（默认注册 logging），并在 `:65-69` 调用 `UseSerilog`，因此 `ILogger<CachedPermissionStore>` 可正常解析。应用能启动。

**真实影响**：这个项目的核心卖点是"架构约束由测试守卫"（`docs/Architecture.md:172-178` 整节在讲这件事）。守卫本身处于红色状态且无人运行，等于守卫失效。对模板而言，下游使用者会合理假设自带测试是绿的——一个 `git clone` 后 `dotnet test` 就失败的模板，会让人怀疑其余 47 个测试的可信度。

### H2 — 架构文档与代码直接矛盾（Redis 故障降级）

**严重度：高** · **置信度：high**

| 来源 | 内容 |
| --- | --- |
| `docs/Architecture.md:150` | "`CachedPermissionStore` **没有**捕获 Redis 读写异常并自动降级的逻辑；启用缓存时 Redis 同时纳入就绪检查。" |
| `CachedPermissionStore.cs:58-71` | `TryGetCachedAsync` 用 `try/catch` 包裹 `cache.GetAsync`，捕获后 `Fallbacks.Add(1)`、记录警告"权限缓存读取失败，降级到数据库直读"、返回 `null` 走数据库 |
| `CachedPermissionStore.cs:73-83` | `TrySetCachedAsync` 同样捕获写入异常并跳过本次缓存 |

文档描述的是**变更之前**的行为。代码里的降级逻辑（含 `Fallbacks` 计数器 `:21`）确实存在。

**影响**：`docs/Architecture.md` 是本项目唯一的架构说明文档。它不是遗漏了信息，而是**主动传递错误信息**。后果具体可预期：维护者可能重复实现已存在的降级逻辑；或在容量规划时误判"Redis 故障 = 全站授权失败"，从而建设不必要的冗余；或反过来，因为文档说没有降级而不为降级路径写测试（事实上确实没有——见 M1 的跳过清单）。

**与 H1 同源**：同一次变更（为 `CachedPermissionStore` 加入 Redis 降级与日志）同时造成了代码/测试/文档三处不一致，而没有任何闸门拦住其中任何一处。这不是两个独立缺陷，是**一个流程缺陷的两个症状**。

### M1 — 全部数据库事务与并发测试在本机不可判定

**严重度：中** · **置信度：high**

`PostgresFixture.cs:26-29`：找不到 PostgreSQL 工具时 `Assert.Inconclusive`。本机 8 个测试跳过：

| 跳过用例 | 未获验证的声明（`docs/Architecture.md` §5） |
| --- | --- |
| `CombinedFailureRollsBackAllWritesAndRevision` | 组合失败回滚全部写入与修订号 |
| `ExceptionRollsBackAndTransactionCanBeReused` | 异常回滚且事务可复用 |
| `CancellationRollsBackEvenWhenCleanupTokenIsCancelled` | 取消后仍完成回滚清理 |
| `PermissionWritesCanCompleteWhileMenuWriteIsOpen` | 菜单锁不阻塞权限写 |
| `MenuWritesDoNotInvalidatePermissions` | 菜单写不误失效权限 |
| `MigrationsAreRepeatableAndVerified` | 迁移可重复且经校验 |
| `AuthenticationSupportsUtcRotationAndRevocation` | UTC 轮换与撤销 |
| `NarrowSessionsRejectOtherCapabilitiesAndExpiredUsage` | 窄化写会话拒绝越权与过期使用 |

**影响**：文档 §5「事务与并发约束」是全文技术密度最高的一节，也是这个项目相对普通模板最主要的差异化主张。这 8 个测试正是它的唯一验证手段，而在当前环境下**零覆盖**。

需要明确区分：`Assert.Inconclusive` 是**正确做法**（诚实报告不可判定，而非伪装通过），缺陷不在测试写法，而在于没有 CI 保证"至少存在一个环境能真正跑这些测试"。跳过状态可以无限期持续而无人知晓。

### M2 — 菜单搜索为前置通配符 LIKE，且初始迁移脚本零索引

**严重度：中** · **置信度：high**

- `MenuReadRepository.cs:40` 用 `Contains` → 生成 `LIKE '%关键字%'`，前置通配符使任何 B-tree 索引失效
- `000_menu_schema.sqlserver.sql:7` `DisplayName` 为 `nvarchar(max)`；`:20` `CreatedBy` 同样为 `nvarchar(max)`。`max` 类型行外存储，比较代价更高，且无法作为普通索引键
- `MenuReadRepository.cs:45` `Search` 按 `CreatedAt` 排序，但**全部迁移脚本对 `Menus` 表未建任何索引**。已核实整个 `Migrations/` 目录仅有两条索引：`UX_RoleMenu_Active`（`001_permission_revision.sqlserver.sql:10`、`.postgresql.sql:8`，`RoleMenu(RoleId, MenuId) WHERE NOT IsDeleted` 过滤唯一索引）与 `IX_AuthenticationSessions_Expiry`（`002_authentication.sqlserver.sql:17`）。`Menus` 的 `ParentId`、`IsDeleted`、`CreatedAt` 均无索引
- `RoleMenu` 仅靠 `001_permission_revision.sqlserver.sql:9-10` 的过滤唯一索引间接覆盖 `RoleId`

**影响**：当前数据量下不会显现。但模板的价值在于被复用——下游项目菜单表达到数万行后，搜索与分页必然全表扫描，而**届时补索引需要停机或在线 DDL**，成本远高于在模板阶段就建好。`nvarchar(max)` 用于 `DisplayName`/`CreatedBy` 这类明显有长度上限的字段，属于建模时未做约束决策。

### M3 — `/ready` 就绪探针会被菜单写事务阻塞

**严重度：中** · **置信度：medium-high**

- `MenuSchemaHealthCheck.cs:18` 就绪检查 `SELECT` `MenuWriteLock` 单行
- `AccessControlTransaction.cs:58-63` 每个菜单写事务对该行执行 `UPDATE ... WHERE Id=1`，持有排它锁至提交
- `MenuMutations.cs:86-94` 的 `UpdateSort` 在**持锁期间**全表加载菜单并**逐行 UPDATE**，N 次数据库往返，事务时长随菜单数线性增长
- SQL Server 默认 READ COMMITTED 下，探针的 SELECT 会被该 X 锁阻塞

**影响**：一次大规模排序操作期间，`/ready` 可能超时（`PersistenceHealthRegistration.cs:12-21` 各检查 5 秒）。在 Kubernetes 中就绪探针失败的后果是 **Pod 被摘除流量**；若持续失败触发重启，正在执行的长事务回滚，形成放大故障。健康检查本应反映"能否服务读请求"，却被写锁耦合——而读请求实际上并不被 `MenuWriteLock` 阻塞。

置信度 medium-high 而非 high：未在真实数据库上实测锁等待行为（本机无 PG 工具），结论基于 SQL Server 默认隔离级别语义推断。若连接串启用了 RCSI，该风险显著降低——见「未知项」。

### M4 — 全局单行锁使所有菜单写串行化，且无锁等待超时

**严重度：中（已知取舍，需标注为容量上限）** · **置信度：high**

- `AccessControlTransaction.cs:58-63`：`UPDATE MenuWriteLock SET Version=Version+1 WHERE Id=1`，靠行级 X 锁互斥。`Version` 值**从不被读取或比对**，因此这是纯互斥量，**不是乐观并发**
- 加锁顺序固定为 menu → permission（`:57`），锁随事务回滚自动释放，`writing` 标志在 `finally` 复位（`:50,67`）——**无死锁风险，无锁泄漏**，这部分设计是干净的
- 未设置 lock timeout 或 statement timeout。PG 侧 advisory lock 无超时（`MigrationRunner.cs:26`），SQL Server 侧 `sp_getapplock` 有 60 秒（`:27`）

**必须公正说明**：`docs/Architecture.md:117` 已明确写出"固定锁行简化了跨请求和跨实例的协调，但也使相应写入共享串行入口；这是当前实现的并发粒度"。这是**被文档承认的设计取舍，不是隐藏缺陷**。

**保留为中级风险的理由**：取舍已知，但**容量上限未被量化**。下游使用者无法从文档判断"这个模板能支撑多少并发菜单写"。叠加 M3（长事务阻塞就绪探针）与 M2（无索引），在高负载下会形成连锁反应。缺 PG 时锁等待可能无限期（见 M1，`PermissionWritesCanCompleteWhileMenuWriteIsOpen` 正是验证此类行为的测试，处于跳过状态）。

### M5 — 权限白名单与硬编码 AppAdmin 构成静默放权路径

**严重度：中** · **置信度：high**

- `UseCaseAuthorization.cs:33-34`：硬编码 `AppAdmin` 超级角色 + `PermissionWhitelist` 配置放行
- 白名单绑定自 `WebOptionsRegistration.cs:28`
- 三个环境的 `appsettings.{Development,Production,Staging}.json` 均为 `"Codes": []`（当前安全）
- 有测试保护：`CleanArchitectureTests.cs:58-64` 断言白名单**不放行匿名调用者**；`:54` 断言管理员身份**不放行未定义的权限码**

**影响**：一处配置误填（例如把 `menu.edit` 放进白名单以"临时解决联调问题"），即让**任意已认证用户**获得该权限，且代码路径中**无审计日志**。现有测试覆盖了"匿名不放行"和"未定义码不放行"，但没有测试覆盖"白名单被填入敏感权限码时应当告警"。这类配置的变更通常在运维侧发生，不经过代码审查。

### M6 — 内部基础设施标识泄漏到 MIT 许可的模板中

**严重度：中** · **置信度：high**

`appsettings.Production.json:9`：

```
/*Rancher内部调用Redis的地址和端口 fcs-redis-service.commoncache.svc.cluster.local:6379*/
```

全仓库泄漏扫描（`svc.cluster.local|rancher|fcs-|\.corp\.|\.internal|内网 IP 段`）**仅此一处命中**，范围是孤立的。`LICENSE` 为 MIT（`README.md:153`），即模板可被任意再分发。

**影响**：暴露内部 Kubernetes 服务 DNS 名、命名空间（`commoncache`）、编排平台（Rancher）与项目前缀（`fcs-`）。对复用者是噪音（一个指向不存在服务的注释）；对源组织是内部拓扑信息泄漏。同时说明模板**脱敏流程不完整**——注释类内容容易被漏检，值得检查是否还有其他内部残留（见「未知项」）。

### M7 — 4 个零使用的 NuGet 包与 3 个静默失效的 Serilog 增强器

**严重度：中（静默失效类，最难被发现）** · **置信度：high**

**未被任何代码引用的包**（全库 grep `.cs` 零命中）：

| 包 | 版本 | 说明 |
| --- | --- | --- |
| `Azure.Identity` | 1.21.0 | 零引用 |
| `Polly` | 8.8.0 | 零引用 |
| `Microsoft.Extensions.Http.Polly` | 10.0.12 | 零引用；grep `AddPolicyHandler`/`AddTransientHttpErrorPolicy`/`IAsyncPolicy` 无命中 |
| `System.Security.Cryptography.Pkcs` | 10.0.12 | 零引用 |
| `Serilog.Sinks.Splunk` | 5.2.0 | 零引用，任何 `appsettings` 中均未配置 Splunk sink |

对比：`Newtonsoft.Json` 确有使用（`HttpCurrentRequest.cs:5`、`JwtTokenIssuer.cs:7`、`TokenAuthentication.cs:8`、`DataProtectionBuilderExtensions.cs:6`、`HttpContractRegistration.cs:5`），`Microsoft.Extensions.Caching.Memory` 确有使用（`InfrastructureRegistration.cs:13` `AddMemoryCache()`）。

命名误导：`HttpPolicyRegistration.cs` 文件名暗示弹性策略（Polly），实际内容只有 CORS、`ForwardedHeaders`、`RateLimiter` 三项，与 Polly 无关。

**声明了但缺少对应包的 Serilog 增强器**：

| 配置文件 | 声明的增强器 | 缺失的包 |
| --- | --- | --- |
| `appsettings.Development.json:44-46` | `WithMachineName` | `Serilog.Enrichers.Environment` |
| `appsettings.Production.json:54-56` | `WithProcessId` | `Serilog.Enrichers.Process` |
| `appsettings.Staging.json:51-53` | `WithThreadId` | `Serilog.Enrichers.Thread` |

`Serilog.AspNetCore` 的传递依赖**不包含**这三个增强器包，且它们未出现在 `Directory.Packages.props`（集中版本管理，任何包引用都必须在此声明）或任何 `.csproj` 中。`Serilog.Settings.Configuration` 找不到对应扩展方法时**静默跳过**，不抛异常。

**影响**：三个环境的日志都缺少配置所承诺的机器名、进程 ID、线程 ID 上下文——而这些正是多实例容器部署下排查问题最需要的字段。由于是静默失效，只有在真正需要按机器名聚合日志时才会发现。同时，5 个未使用的包虚增了供应链攻击面与还原时间，对模板而言还传递了错误的技术栈信号（下游会以为项目用了 Polly 做弹性、用 Azure.Identity 做托管身份）。

### 低严重度发现

| 编号 | 发现 | 证据 | 严重度 | 置信度 |
| --- | --- | --- | --- | --- |
| L1 | 无登录/令牌签发入口，认证闭环无法端到端验证 | `SessionService.cs:30` 的 `IssueAsync` 仅测试调用；全库无 Login action。**已在 `README.md:14` 与 `docs/Architecture.md:132` 诚实声明** | 低（模板取舍） | high |
| L2 | `My.XXX.APIs/Legacy/` 为空目录，无任何文件 | `ls My.XXX.APIs/Legacy/` → 仅 `.` 与 `..` | 低 | high |
| L3 | 三个启动 profile 的 `launchUrl` 指向不存在的 `api/Demo/Index` | `launchSettings.json:6,15,24`；无 Demo 控制器。**`README.md:102` 已知** | 低（首次运行即 404） | high |
| L4 | `app.UseStaticFiles()` 为死中间件，且位于认证之前 | `Program.cs:103`；`My.XXX.APIs/wwwroot` 不存在 | 低 | high |
| L5 | DataProtection 密钥与配置加密共用同一 `APP_ENCRYPTION_KEY`；`KeyPath` 默认在应用目录下 | `DataProtectionBuilderExtensions.cs:30,56`；`WebSecurityRegistration.cs:31` 默认 `AppContext.BaseDirectory/keys`。**`README.md:143` 已提示需持久可写卷** | 低（单钥失守即全失守；容器临时文件系统重启丢钥） | high |
| L6 | 刷新令牌重放无 reuse-detection | `SessionService.cs:25` 旧 jti 重放仅被拒绝，不撤销整个会话；`RefreshExpiryInMinutes=43200`（30 天）。被窃令牌与合法端可持续各自刷新至过期 | 低-中 | high |
| L7 | 生产配置 `"AllowedHosts": "*"`，Host 头过滤关闭 | `appsettings.Production.json` | 低 | high |
| L8 | Serilog File sink `path` 为 `logs/.txt`（缺文件名前缀）；`retainedFileCountLimit` 用字符串 `"90"` | `appsettings.Production.json` Serilog 段 | 低 | high |
| L9 | 迁移无 down 脚本，无降级能力，单向 | `MigrationRunner.cs`；`Migrations/` 下仅正向脚本 | 低（取舍常见，但未在文档说明） | high |
| L10 | `RoleMenu.MenuId`、`Menus.ParentId` 无外键约束，一致性完全依赖应用层全局锁 | `000_menu_schema.*.sql`、`001_permission_revision.*.sql` | 低-中（锁失效或旁路写入即产生孤儿数据） | high |
| L11 | `AuthenticationUser.UserId` 声明为 `string?` 却标注 NotNull，可空性意图自相矛盾 | `AuthenticationUser.cs:10-11`。**违反 `AGENTS.md`「正确声明可空类型」** | 低 | high |
| L12 | 测试中使用 `null!`，与 `AGENTS.md`「不使用 `null!` 掩盖未初始化问题」冲突 | `CleanArchitectureTests.cs:36-39,50`。此处为**有意的测试技巧**（`:33` 注释说明"任何意外访问都会让测试失败"），属合理例外，但约定未写明例外范围 | 低（约定与实操未对齐） | high |
| L13 | `ApplicationName` 传入 `Assembly.FullName` 而非程序集简单名 | `Program.cs:58`。`WebApplicationOptions.ApplicationName` 期望简单名；`FullName` 含 `Version/Culture/PublicKeyToken`，在某些程序集解析路径上可能不匹配。当前构建与测试均通过，未见实际故障 | 低 | **low**（需验证） |

---

## 未知项（明确标注为待验证，不作为结论）

| 项 | 为何未知 | 验证方法 |
| --- | --- | --- |
| 数据库连接的锁/语句超时与隔离级别 | 连接串在仓库内为空串，由环境变量注入。是否启用 RCSI 直接决定 M3 的实际严重度 | 在目标环境执行 `SELECT is_read_committed_snapshot_on FROM sys.databases` (SQL Server) 或 `SHOW default_transaction_isolation` (PG) |
| 生产 `JwtConfig:Secret` 与 `APP_ENCRYPTION_KEY` 的实际强度 | 由密钥提供方注入，仓库内不可见 | 审查部署环境的密钥生成与轮换流程 |
| 8 个数据库测试在具备 PG 工具的环境中是否通过 | 本机未安装 PostgreSQL 客户端工具 | 设置 `MY_TEST_POSTGRES_BIN` 后重跑，或在 CI 中提供 PG 服务容器 |
| `ForwardedHeaders:KnownProxies` 生产实配是否正确 | 未在仓库内配置。配错会导致限流按代理 IP 聚合而失效 | 核对部署环境的入口代理地址配置 |
| Redis 连接是否启用 TLS 与认证 | `RedisConfig` 仅有连接串字段，仓库内为空 | 检查实际连接串是否含 `ssl=True` 与密码 |
| 是否还有其他内部信息残留 | M6 的扫描仅覆盖基础设施标识模式，未覆盖业务术语、内部项目名、人名、内部 URL | 对源组织内部标识做一次针对性扫描 |
| 变更节奏与历史腐化趋势 | 非 git 仓库，无提交历史可分析 | 无法验证。这本身限制了审查的完整性 |

---

## 下一步检查建议

1. **修复 H1 后立即建立 CI**——顺序很重要。先建 CI 会立即红，容易被理解为"CI 有问题"而被关闭
2. **修正 H2 的文档**，并顺手核对 `docs/Architecture.md` 其余段落是否同样滞后于代码（本次审查仅逐条验证了与已发现缺陷相关的段落）
3. **在一个有 PostgreSQL 的环境跑通全部 29 个集成测试**，确认 M1 的 8 项在真实数据库下的行为与文档 §5 一致
4. 若模板确实对外发布，**清理 M6 的内部标识**并复查脱敏完整性

具体动作、验收标准与排序见 `remediation-plan.md`。
