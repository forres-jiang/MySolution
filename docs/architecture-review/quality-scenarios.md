# 质量属性场景

- **配套文档**：`architecture-review.md`（发现清单）、`remediation-plan.md`（整改动作）
- **用途**：把抽象的"架构好不好"转成**可判定的场景**——每个场景给出刺激、响应、度量与当前证据状态
- **产出技能**：`architecture-visualization:risk-quality-reviewer`
- **日期**：2026-10-12

## 如何读这份文档

每个场景的结构：

| 字段 | 含义 |
| --- | --- |
| 刺激 | 触发条件（谁在什么环境下做什么） |
| 期望响应 | 架构应当表现出的行为 |
| 度量 | 可判定的验收阈值 |
| 当前状态 | **已验证** / **有实现但未验证** / **未满足** / **无法判定** |
| 证据 | 支撑该状态判断的代码位置或实测结果 |

"有实现但未验证"是本审查中出现最多的状态——代码写对了，但没有任何自动化手段证明它在真实环境下仍然对。这类场景是整改的主要目标。

---

## 场景 1 — 可验证性：架构约束被侵蚀时应当自动失败

**质量属性**：可维护性 / 可验证性
**关联发现**：H1

| 项 | 内容 |
| --- | --- |
| 刺激 | 开发者在 `My.XXX.Application` 中添加对 `My.XXX.Persistence` 的项目引用，或在控制器中直接注入 `IAccessControlTransaction` |
| 期望响应 | 提交前或 CI 中测试失败，给出明确的违规位置 |
| 度量 | 该变更**不可能**在测试全绿的情况下被合并 |
| 当前状态 | **未满足** |
| 证据 | 守卫测试本身存在且设计良好（`ArchitectureTests.cs:57-87` 校验 `.csproj` 引用白名单；`ArchitectureBoundaryTests.cs:29-104` IL 扫描控制器越界）。但仓库无 CI（`.github` 不存在），且实测集成测试为红（1 失败）。守卫**能**拦住违规，前提是有人运行它——而当前连它自己的健康状态都没人监控 |

**验证方法**：故意在 `Application.csproj` 加一条 `<ProjectReference Include="..\My.XXX.Persistence\...">`，确认 `ProjectDependenciesPointInward` 失败；再在 `MenuController` 注入 `IAccessControlTransaction`，确认 `HttpAdaptersCannotBypassUseCasesToAccessStorage` 失败。两个实验都应能触发——这验证的是守卫的**有效性**，而 H1 的整改验证的是守卫的**强制性**。

---

## 场景 2 — 可靠性：Redis 完全不可用时授权不应失败

**质量属性**：可靠性 / 可用性
**关联发现**：H2、M1

| 项 | 内容 |
| --- | --- |
| 刺激 | 启用权限缓存（`AppConfig:PermissionDataCache=1`）后 Redis 连接中断或超时 |
| 期望响应 | 权限查询降级为数据库直读，授权结果正确，仅记录警告并递增降级计数器 |
| 度量 | Redis 不可用期间授权成功率 100%；`permissions.database.fallbacks` 计数器递增；无 5xx |
| 当前状态 | **有实现但未验证**（且文档否认该实现存在） |
| 证据 | 实现：`CachedPermissionStore.cs:58-71` 捕获读异常并降级、`:73-83` 捕获写异常并跳过、`:21` `Fallbacks` 计数器。文档：`docs/Architecture.md:150` 声称"没有捕获 Redis 读写异常并自动降级的逻辑"。测试：`PermissionCacheTests.cs` 中 `Cache` 桩（`:99-116`）**永不抛异常**，因此降级路径零覆盖 |

**验证方法**：在 `PermissionCacheTests` 中增加一个 `ThrowingCache` 桩（`GetAsync`/`SetAsync` 抛 `RedisConnectionException`），断言 `GetPermissionCodesAsync` 仍返回正确权限码且 `Fallbacks` 递增。这是**成本最低的高价值补测**——不需要真实 Redis，也不需要数据库。

---

## 场景 3 — 可靠性：并发改同一菜单树不应静默覆盖

**质量属性**：可靠性 / 数据完整性
**关联发现**：M4、M1

| 项 | 内容 |
| --- | --- |
| 刺激 | 两个请求同时对同一父节点下的菜单执行排序或层级调整 |
| 期望响应 | 后到者阻塞等待，前者提交后后到者基于最新状态执行；无丢失更新，无死锁 |
| 度量 | 两次请求都返回成功（或后者因业务规则失败），最终菜单顺序自洽；无死锁错误 |
| 当前状态 | **有实现但未验证** |
| 证据 | 实现：`AccessControlTransaction.cs:58-63` 通过 `UPDATE MenuWriteLock ... WHERE Id=1` 的行级 X 锁串行化；固定加锁顺序 menu → permission（`:57`）避免死锁；锁随事务回滚自动释放，`writing` 标志在 `finally` 复位（`:50,67`）。设计取舍已在 `docs/Architecture.md:117` 明确承认。验证缺口：`PermissionWritesCanCompleteWhileMenuWriteIsOpen` 与 `MenuWritesDoNotInvalidatePermissions` 均处于跳过状态（`PostgresFixture.cs:26-29`） |

**验证方法**：在具备 PostgreSQL 的环境中运行跳过的 8 个测试；额外做一次并发实验——两个连接同时对 `MenuWriteLock` 执行写事务，测量第二个的等待时长，确认是否存在无限等待（当前未设置 lock timeout）。

---

## 场景 4 — 可靠性：组合写入中途失败应完整回滚

**质量属性**：可靠性 / 数据完整性
**关联发现**：M1、L10

| 项 | 内容 |
| --- | --- |
| 刺激 | `RoleAccessAdministration.ReplaceAsync` 在替换角色菜单成功、替换权限码失败时；或请求在写入中途被取消 |
| 期望响应 | 全部写入回滚，`PermissionRevision` 不推进，事务适配器可复用 |
| 度量 | 数据库中角色菜单与权限码保持变更前状态；无孤儿关联记录 |
| 当前状态 | **有实现但未验证** |
| 证据 | 实现：`AtomicWrite.cs:14-27` 业务失败结果、异常、取消均回滚，取消后使用不受取消影响的令牌清理。风险叠加：`RoleMenu.MenuId` 与 `Menus.ParentId` **无外键约束**（`000_menu_schema.*.sql`），一致性完全依赖这把应用层锁——任何绕过 `IAccessControlTransaction` 的写入路径（例如未来的运维脚本、数据修复）都会产生无约束的孤儿数据。验证缺口：`CombinedFailureRollsBackAllWritesAndRevision`、`CancellationRollsBackEvenWhenCleanupTokenIsCancelled`、`ExceptionRollsBackAndTransactionCanBeReused` 均跳过 |

**验证方法**：在具备 PostgreSQL 的环境运行上述 3 个测试。关于外键缺失：这是一次**需要决策**的取舍而非明确缺陷——加外键能防孤儿数据，但会与软删除（`IsDeleted` + 过滤唯一索引）交互，需要确认级联语义。建议在 `docs/Architecture.md` §5 补充说明"一致性由应用层锁保证，数据库层无外键约束"，让下游知情。

---

## 场景 5 — 可运维性：长写事务期间就绪探针应保持正常

**质量属性**：可运维性 / 可用性
**关联发现**：M3、M4、M2

| 项 | 内容 |
| --- | --- |
| 刺激 | 一次涉及数千菜单项的排序操作进行中（持 `MenuWriteLock` 排它锁，逐行 UPDATE），Kubernetes 每 10 秒探测 `/ready` |
| 期望响应 | `/ready` 在探针超时内返回 200，Pod 不被摘除流量 |
| 度量 | `/ready` p99 延迟 < 探针 `timeoutSeconds`；长写事务期间零次探针失败 |
| 当前状态 | **未满足**（medium-high 置信度，未实测） |
| 证据 | `MenuSchemaHealthCheck.cs:18` 就绪检查 SELECT `MenuWriteLock` 单行，而 `AccessControlTransaction.cs:58-63` 的写事务对该行持 X 锁至提交；`MenuMutations.cs:86-94` 的 `UpdateSort` 在持锁期间逐行 UPDATE，事务时长随菜单数线性增长；`PersistenceHealthRegistration.cs:12-21` 各检查 5 秒超时。**关键不确定性**：若数据库启用 RCSI（SQL Server）或读走只读副本，则 SELECT 不被 X 锁阻塞，风险大幅降低——连接串在仓库内为空，无法判定 |

**验证方法**：先在目标环境查 `SELECT is_read_committed_snapshot_on FROM sys.databases WHERE name = DB_NAME()`。若为 0，则做实测：开启一个长写事务，并发请求 `/ready` 观察是否阻塞。整改方向是让就绪检查**不依赖被业务写锁定的行**（例如改为检查 `SchemaMigrations` 表或轻量只读查询）。

---

## 场景 6 — 安全性：权限撤销应立即生效

**质量属性**：安全性
**关联发现**：无（此场景**已满足**，列出作为不可回退的基线）

| 项 | 内容 |
| --- | --- |
| 刺激 | 管理员移除某角色的 `menu.edit` 权限，该角色的用户随后立即发起菜单编辑请求 |
| 期望响应 | 请求被拒绝，不出现"旧缓存在 TTL 内仍授权成功"的窗口 |
| 度量 | 权限变更后第一个请求即被拒绝；授权绕过窗口 = 0 |
| 当前状态 | **已验证（设计层面）** |
| 证据 | 缓存键内嵌 `PermissionRevision`（`CachedPermissionStore.cs:44-45`）；权限写事务在同一事务内原子推进 revision（`AccessControlTransaction.cs:61-63`）→ 旧键立即不可达，TTL 仅用于回收空间而非决定正确性；读后复核 revision（`:49`），3 轮冲突后直读数据库（`:54-55`）。测试：`RevisionChangeDuringReadRetriesWithoutPublishingOldData`、`CachedValuesCannotBeModifiedByCallersAndRoleSetsAreCanonical`（`PermissionCacheTests.cs:44-82`）**均通过**；`MenuWritesDoNotInvalidatePermissions` 需真实数据库，处于跳过状态 |

**为何重要**：这是本项目最强的单点设计。多数缓存权限的实现会留下 TTL 长度的授权绕过窗口，这里用 revision-in-key 从根上消除了它。任何整改都**不得**为了性能把 revision 从缓存键中移除。

---

## 场景 7 — 安全性：白名单误配应可被发现

**质量属性**：安全性 / 可审计性
**关联发现**：M5

| 项 | 内容 |
| --- | --- |
| 刺激 | 运维为排查联调问题，把 `menu.edit` 加入 `PermissionWhitelist:Codes` 并部署 |
| 期望响应 | 启动时记录警告（或拒绝启动）；每次经白名单放行的授权决策可审计 |
| 度量 | 白名单非空时启动日志有显著告警；白名单放行路径产生审计事件 |
| 当前状态 | **未满足** |
| 证据 | `UseCaseAuthorization.cs:33-34` 白名单对任意已认证用户放行，无日志、无告警、无审计；三个环境的 `appsettings` 均为 `"Codes": []`（当前安全）。现有测试覆盖了"匿名不放行"（`CleanArchitectureTests.cs:58-64`）与"管理员不放行未定义码"（`:54`），但**没有**测试覆盖"白名单被填入敏感码时的可观测性"。风险特征：配置变更走运维路径，通常不经过代码审查 |

**验证方法**：设置 `PermissionWhitelist__Codes__0=menu.edit` 启动应用，检查是否有任何日志输出。当前预期为无——这正是要整改的点。

---

## 场景 8 — 可运维性：多实例部署下日志应可定位来源

**质量属性**：可观测性
**关联发现**：M7

| 项 | 内容 |
| --- | --- |
| 刺激 | 生产多副本部署中出现偶发 500，运维需按机器名/进程/线程筛选日志 |
| 期望响应 | 每条日志带有 `MachineName`、`ProcessId`、`ThreadId` 字段 |
| 度量 | 抽样日志中三个字段的存在率 = 100% |
| 当前状态 | **未满足（静默失效）** |
| 证据 | `appsettings.{Development,Production,Staging}.json` 分别在第 44-46 / 54-56 / 51-53 行声明 `WithMachineName`、`WithProcessId`、`WithThreadId`，但 `Serilog.Enrichers.Environment`、`Serilog.Enrichers.Process`、`Serilog.Enrichers.Thread` **均未**出现在 `Directory.Packages.props`（集中版本管理，任何包引用必须在此声明）或任何 `.csproj` 中。`Serilog.AspNetCore` 的传递依赖不含这三者。`Serilog.Settings.Configuration` 找不到扩展方法时静默跳过，不抛异常、不写警告 |

**验证方法**：启动应用产生一条日志，检查输出中是否含 `MachineName` 字段。预期缺失。整改后（补包或删配置）应二选一并对齐三个环境的配置文件。

---

## 场景 9 — 可维护性：菜单表增长后搜索仍应可用

**质量属性**：性能 / 可扩展性
**关联发现**：M2

| 项 | 内容 |
| --- | --- |
| 刺激 | 菜单表达到 5 万行，用户执行关键字搜索并按创建时间分页 |
| 期望响应 | 查询在可接受延迟内返回 |
| 度量 | 搜索 p95 < 200ms（模板未定义 SLO，此为建议基线） |
| 当前状态 | **未满足**（随数据量增长而显现） |
| 证据 | `MenuReadRepository.cs:40` 用 `Contains` 生成 `LIKE '%关键字%'`，前置通配符使 B-tree 索引不可用；`000_menu_schema.sqlserver.sql:7` `DisplayName` 为 `nvarchar(max)`（行外存储，且不可作普通索引键）；`:20` `CreatedBy` 同样为 `nvarchar(max)`；`MenuReadRepository.cs:45` 按 `CreatedAt` 排序，但**整个 `Migrations/` 目录对 `Menus` 表未建任何索引**（全库仅 `UX_RoleMenu_Active` 与 `IX_AuthenticationSessions_Expiry` 两条索引） |

**验证方法**：需要真实数据库与数据量，本机无法执行。可先做静态整改：将 `DisplayName`/`CreatedBy` 改为有界长度（如 `nvarchar(200)`/`nvarchar(100)`），并为 `ParentId`、`IsDeleted`、`CreatedAt` 建索引。若搜索需求确实需要子串匹配，应显式决策是否引入全文索引，而非依赖 `LIKE '%..%'`。

---

## 场景 10 — 可复用性：下游首次克隆即可信任

**质量属性**：可复用性（模板项目的**首要**质量属性）
**关联发现**：H1、M1、M6、M7、L2、L3

| 项 | 内容 |
| --- | --- |
| 刺激 | 下游团队 `git clone` 该模板，执行 `dotnet build` 与 `dotnet test`，然后 `dotnet run` |
| 期望响应 | 构建通过、测试全绿（或明确标注为环境依赖跳过且提供一键补齐方式）、启动后落到可用页面 |
| 度量 | 构建 0 错误；测试 0 失败；首次运行不出现 404 |
| 当前状态 | **未满足** |
| 证据 | 构建：0 警告 0 错误 ✅。单元测试：27/27 通过 ✅。集成测试：**1 失败**（`CacheToggleKeepsTheApplicationServiceAndItsResults`）❌、8 跳过（需 PostgreSQL 工具）⚠️。首次运行：三个 profile 的 `launchUrl` 指向不存在的 `api/Demo/Index`（`launchSettings.json:6,15,24`）→ 404 ❌（`README.md:102` 已告知，但告知不等于修好）。另有空目录 `My.XXX.APIs/Legacy/` 与 5 个零引用 NuGet 包构成噪音 |

**验证方法**：这正是 CI 应当固化的检查项。整改完成的判据 = 一次干净克隆后 `build + test` 全绿，且 `dotnet run` 落到 Swagger UI。

---

## 状态汇总

| 状态 | 场景 |
| --- | --- |
| 已验证 | 6 |
| 有实现但未验证 | 2、3、4 |
| 未满足 | 1、5、7、8、9、10 |
| 无法判定 | 5 的一部分（依赖数据库隔离级别配置，仓库内不可见） |

**读法**：10 个场景中只有 1 个完全已验证。但"未满足"的 6 个里，有 4 个（1、7、8、10）的整改成本很低——它们主要是缺少配置、缺少 CI、缺少一条日志。真正需要数据库环境与实测投入的是 5 和 9。"有实现但未验证"的 3 个（2、3、4）代码是对的，缺的是让它们能被自动验证的环境。
