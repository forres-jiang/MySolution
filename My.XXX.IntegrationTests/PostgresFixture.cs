using My.XXX.Persistence;
using My.XXX.Persistence.Migrations;
using LinqToDB;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;

namespace My.XXX.IntegrationTests;

/// <summary>Owns an isolated local cluster. Never connects to an application database.</summary>
internal sealed class PostgresFixture : IAsyncDisposable
{
    private readonly string bin;
    private readonly string directory;
    private bool started;
    public string ConnectionString { get; private set; }

    private PostgresFixture(string bin, string directory) { this.bin = bin; this.directory = directory; }

    public static async Task<PostgresFixture> Start()
    {
        var bin = Environment.GetEnvironmentVariable("MY_TEST_POSTGRES_BIN")
            ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "PostgreSQL", "15", "bin");
        if (!File.Exists(Path.Combine(bin, Executable("initdb"))))
            Assert.Inconclusive("Set MY_TEST_POSTGRES_BIN to a PostgreSQL bin directory to run database tests.");
        var root = new DirectoryInfo(AppContext.BaseDirectory);
        while (root != null && !File.Exists(Path.Combine(root.FullName, "MySolution.sln"))) root = root.Parent;
        if (root == null) throw new InvalidOperationException("Repository root unavailable.");
        var fixture = new PostgresFixture(bin, Path.Combine(root.FullName, ".test-artifacts", "architecture-" + Guid.NewGuid().ToString("N")));
        Directory.CreateDirectory(fixture.directory);
        try
        {
            await fixture.Run("initdb", "-D", Path.Combine(fixture.directory, "data"), "-U", "postgres", "--auth=trust", "--encoding=UTF8", "--no-locale");
            var listener = new TcpListener(IPAddress.Loopback, 0);
            listener.Start();
            var port = ((IPEndPoint)listener.LocalEndpoint).Port;
            listener.Stop();
            await fixture.Run("pg_ctl", "-D", Path.Combine(fixture.directory, "data"), "-l", Path.Combine(fixture.directory, "postgres.log"),
                "-o", $"-h 127.0.0.1 -p {port} -F", "-w", "start");
            fixture.started = true;
            fixture.ConnectionString = $"Host=127.0.0.1;Port={port};Database=postgres;Username=postgres;Pooling=false;Timeout=10";
            await MigrationRunner.ApplyAsync(fixture.ConnectionString, DatabaseProvider.PostgreSQL);
            await MigrationRunner.VerifyAsync(fixture.ConnectionString, DatabaseProvider.PostgreSQL);
            return fixture;
        }
        catch { await fixture.DisposeAsync(); throw; }
    }

    public DBContext Open() => new(new DataOptions<DBContext>(
        DatabaseConfiguration.Configure(new DataOptions(), ConnectionString, DatabaseProvider.PostgreSQL)));

    private static string Executable(string name) => OperatingSystem.IsWindows() ? name + ".exe" : name;

    private async Task Run(string name, params string[] args)
    {
        // On Windows pg_ctl's server can inherit redirected pipe handles even with -l.
        // Do not wait for pipe EOF from a server that is intentionally still running.
        var capture = name != "pg_ctl";
        var info = new ProcessStartInfo(Path.Combine(bin, Executable(name)))
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = capture,
            RedirectStandardError = capture,
            WorkingDirectory = directory
        };
        foreach (var argument in args) info.ArgumentList.Add(argument);
        using var process = Process.Start(info) ?? throw new InvalidOperationException("Could not launch PostgreSQL tool.");
        var stdout = capture ? process.StandardOutput.ReadToEndAsync() : Task.FromResult("");
        var stderr = capture ? process.StandardError.ReadToEndAsync() : Task.FromResult("");
        try { await process.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(45)); }
        catch { if (!process.HasExited) process.Kill(entireProcessTree: true); throw; }
        var output = await stdout + await stderr;
        if (process.ExitCode != 0) throw new InvalidOperationException($"{name} failed: {output}");
    }

    public async ValueTask DisposeAsync()
    {
        if (started || File.Exists(Path.Combine(directory, "data", "postmaster.pid")))
        {
            await Run("pg_ctl", "-D", Path.Combine(directory, "data"), "-m", "fast", "-w", "stop");
            started = false;
        }
        // Keep logs and data under the ignored .test-artifacts directory for diagnosis.
    }
}
