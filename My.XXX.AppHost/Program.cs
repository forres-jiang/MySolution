// Aspire AppHost：本地一键拉起 PostgreSQL、Redis、API 与 Aspire Dashboard。
// 生产部署仍走 Dockerfile，本项目仅参与开发/编排期，不进入发布产物。

var builder = DistributedApplication.CreateBuilder(args);

// PostgreSQL 主库：容器名 "postgres"，数据库名 "mydb"。
// API 项目会通过 WithReference 拿到形如 ConnectionStrings__postgres 的环境变量，
// 需要在 appsettings 中把 "postgres" 或 "Default" 映射到该键（见 README 说明）。
var postgres = builder.AddPostgres("postgres");
var pgDb = postgres.AddDatabase("mydb");

// Redis 缓存：容器名 "redis"。
// API 项目通过 WithReference 注入 ConnectionStrings__redis；
// 现有 RedisConfig:ConnectionString 可在 Program 里改用 GetConnectionString("redis") 读取。
var redis = builder.AddRedis("redis");

// API 项目：等待 PG 与 Redis 就绪后再启动，避免启动竞态。
builder.AddProject<Projects._01My_XXX_APIs>("api")
    .WithReference(pgDb)
    .WaitFor(pgDb)
    .WithReference(redis)
    .WaitFor(redis);

builder.Build().Run();
