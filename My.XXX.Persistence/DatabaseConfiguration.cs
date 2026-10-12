using LinqToDB;
using LinqToDB.DataProvider.PostgreSQL;
using LinqToDB.DataProvider.SqlServer;
using Microsoft.Data.SqlClient;
using Npgsql;
using System;
using System.Data.Common;

namespace My.XXX.Persistence;

public enum DatabaseProvider { SqlServer, PostgreSQL }

public static class DatabaseConfiguration
{
    public static DatabaseProvider ParseProvider(string? value)
    {
        if (value == null) return DatabaseProvider.PostgreSQL;
        if (string.Equals(value, "SqlServer", StringComparison.OrdinalIgnoreCase)) return DatabaseProvider.SqlServer;
        if (string.Equals(value, "PostgreSQL", StringComparison.OrdinalIgnoreCase)) return DatabaseProvider.PostgreSQL;
        throw new InvalidOperationException("Database provider must be SqlServer or PostgreSQL.");
    }

    public static DataOptions Configure(DataOptions options, string connectionString, DatabaseProvider provider) => provider switch
    {
        DatabaseProvider.SqlServer => options.UseSqlServer(connectionString, SqlServerVersion.v2016, SqlServerProvider.MicrosoftDataSqlClient),
        DatabaseProvider.PostgreSQL => options.UsePostgreSQL(connectionString, PostgreSQLVersion.v13),
        _ => throw new ArgumentOutOfRangeException(nameof(provider))
    };

    public static DbConnection CreateConnection(string connectionString, DatabaseProvider provider) => provider switch
    {
        DatabaseProvider.SqlServer => new SqlConnection(connectionString),
        DatabaseProvider.PostgreSQL => new NpgsqlConnection(connectionString),
        _ => throw new ArgumentOutOfRangeException(nameof(provider))
    };
}
