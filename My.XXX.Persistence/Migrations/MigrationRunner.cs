using System;
using System.Collections.Generic;
using System.Data.Common;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace My.XXX.Persistence.Migrations;

/// <summary>Explicit deployment operation; never invoked by normal application startup.</summary>
public static class MigrationRunner
{
    public static async Task ApplyAsync(string connectionString, DatabaseProvider provider,
        CancellationToken cancellationToken = default)
    {
        var settings = new DbConnectionStringBuilder { ConnectionString = connectionString };
        settings["Pooling"] = false; // Session advisory locks must not survive in a pool after failure.
        await using var connection = DatabaseConfiguration.CreateConnection(settings.ConnectionString, provider);
        await connection.OpenAsync(cancellationToken);
        var postgres = provider == DatabaseProvider.PostgreSQL;
        // Session locks cover the scripts' own transactions and the journal writes.
        await Execute(connection, postgres
            ? "SELECT pg_advisory_lock(714025891);"
            : "DECLARE @r int; EXEC @r = sys.sp_getapplock @Resource='BackendTemplate.Migrations', @LockMode='Exclusive', @LockOwner='Session', @LockTimeout=60000; IF @r < 0 THROW 51000, 'Migration lock unavailable.', 1;", cancellationToken);
        try
        {
            var table = postgres ? "public.\"SchemaMigrations\"" : "dbo.SchemaMigrations";
            await Execute(connection, postgres
                ? $"CREATE TABLE IF NOT EXISTS {table} (name varchar(200) PRIMARY KEY, checksum varchar(64) NOT NULL, applied_utc timestamp NOT NULL DEFAULT (CURRENT_TIMESTAMP AT TIME ZONE 'UTC'));"
                : $"IF OBJECT_ID(N'{table}', N'U') IS NULL CREATE TABLE {table} (name varchar(200) PRIMARY KEY, checksum varchar(64) NOT NULL, applied_utc datetime2 NOT NULL DEFAULT SYSUTCDATETIME());", cancellationToken);
            foreach (var migration in RequiredMigrations(provider))
            {
                var (name, sql, checksum) = migration;
                await using var lookup = connection.CreateCommand();
                lookup.CommandText = $"SELECT checksum FROM {table} WHERE name = @name";
                Parameter(lookup, "name", name);
                var existing = await lookup.ExecuteScalarAsync(cancellationToken);
                if (existing is string previous)
                {
                    if (previous != checksum) throw new InvalidOperationException($"Applied migration changed: {name}. Add a new migration instead.");
                    continue;
                }
                await Execute(connection, sql, cancellationToken);
                // Scripts are repeatable: a crash after commit but before journaling safely replays the script.
                await using var record = connection.CreateCommand();
                record.CommandText = $"INSERT INTO {table} (name, checksum) VALUES (@name, @checksum)";
                Parameter(record, "name", name);
                Parameter(record, "checksum", checksum);
                await record.ExecuteNonQueryAsync(cancellationToken);
            }
        }
        finally
        {
            // Closing the unpooled session rolls back unfinished transactions and releases its lock.
            await connection.CloseAsync();
        }
    }

    /// <summary>Read-only compatibility check; newer migrations are allowed for additive rolling upgrades.</summary>
    public static async Task VerifyAsync(string connectionString, DatabaseProvider provider,
        CancellationToken cancellationToken = default)
    {
        await using var connection = DatabaseConfiguration.CreateConnection(connectionString, provider);
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        var table = provider == DatabaseProvider.PostgreSQL ? "public.\"SchemaMigrations\"" : "dbo.SchemaMigrations";
        command.CommandText = $"SELECT name, checksum FROM {table}";
        command.CommandTimeout = 5;
        var applied = new Dictionary<string, string>(StringComparer.Ordinal);
        await using (var reader = await command.ExecuteReaderAsync(cancellationToken))
            while (await reader.ReadAsync(cancellationToken))
                applied.Add(reader.GetString(0), reader.GetString(1));
        foreach (var migration in RequiredMigrations(provider))
            if (!applied.TryGetValue(migration.Name, out var checksum) || checksum != migration.Checksum)
                throw new InvalidOperationException($"Required migration missing or changed: {migration.Name}.");
    }

    private static IEnumerable<(string Name, string Sql, string Checksum)> RequiredMigrations(DatabaseProvider provider)
    {
        var assembly = typeof(MigrationRunner).Assembly;
        var suffix = provider == DatabaseProvider.PostgreSQL ? ".postgresql.sql" : ".sqlserver.sql";
        foreach (var name in assembly.GetManifestResourceNames().Where(n => n.EndsWith(suffix, StringComparison.Ordinal)).OrderBy(n => n, StringComparer.Ordinal))
        {
            using var stream = assembly.GetManifestResourceStream(name);
            using var reader = new StreamReader(stream!);
            var sql = reader.ReadToEnd();
            var checksum = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(sql.Replace("\r\n", "\n"))));
            yield return (name, sql, checksum);
        }
    }

    private static async Task Execute(DbConnection connection, string sql, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        command.CommandTimeout = 120;
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static void Parameter(DbCommand command, string name, string value)
    {
        var parameter = command.CreateParameter();
        parameter.ParameterName = name;
        parameter.Value = value;
        command.Parameters.Add(parameter);
    }
}
