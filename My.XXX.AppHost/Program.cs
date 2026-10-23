// Aspire AppHost：本地一键拉起 PostgreSQL、Redis、API 与 Aspire Dashboard。
// 生产部署仍走 Dockerfile，本项目仅参与开发/编排期，不进入发布产物。

var builder = DistributedApplication.CreateBuilder(args);

// 密钥由 AppHost 的用户机密或 Parameters 配置提供，持续运行时必须保持稳定。
var jwtSecret = builder.AddParameter("jwt-secret", secret: true);
var encryptionKey = builder.AddParameter("encryption-key", secret: true);

var postgres = builder.AddPostgres("postgres");
var pgDb = postgres.AddDatabase("mydb");
var redis = builder.AddRedis("redis");

// 迁移进程只依赖数据库；成功退出后才启动正常服务。
var migrations = builder.AddProject<Projects._01My_XXX_APIs>("migrations", launchProfileName: null)
    .WithArgs("--migrate")
    .WithEnvironment("DatabaseProviders__Default", "PostgreSQL")
    .WithEnvironment("ConnectionStrings__Default", pgDb.Resource.ConnectionStringExpression)
    .WaitFor(pgDb);

builder.AddProject<Projects._01My_XXX_APIs>("api")
    .WithEnvironment("DatabaseProviders__Default", "PostgreSQL")
    .WithEnvironment("ConnectionStrings__Default", pgDb.Resource.ConnectionStringExpression)
    .WithEnvironment("RedisConfig__ConnectionString", redis.Resource.ConnectionStringExpression)
    .WithEnvironment("AppConfig__PermissionDataCache", "1")
    .WithEnvironment("JwtConfig__Secret", jwtSecret)
    .WithEnvironment("APP_ENCRYPTION_KEY", encryptionKey)
    .WithReference(redis)
    .WaitForCompletion(migrations);

builder.Build().Run();
