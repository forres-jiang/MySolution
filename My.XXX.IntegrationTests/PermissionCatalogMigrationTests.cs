using My.XXX.Domain.Authorization.Policies;
using My.XXX.Persistence.Migrations;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace My.XXX.IntegrationTests;

/// <summary>迁移 003 的 LegacyPath→Code 目录是权限码的手工副本；此测试保证它与领域目录不漂移。</summary>
[TestClass]
public sealed class PermissionCatalogMigrationTests
{
    private static readonly Regex Mapping = new(@"^\s*\('[^']+',\s*'([^']+)'\)[,;]?\r?$", RegexOptions.Multiline);

    private static IReadOnlySet<string> CodesIn(string resourceName)
    {
        var assembly = typeof(MigrationRunner).Assembly;
        using var stream = assembly.GetManifestResourceStream(resourceName)
            ?? throw new InvalidOperationException($"Missing embedded migration: {resourceName}");
        using var reader = new StreamReader(stream);
        return Mapping.Matches(reader.ReadToEnd()).Select(m => m.Groups[1].Value).ToHashSet(StringComparer.Ordinal);
    }

    [TestMethod]
    public void StablePermissionMappingsUseCatalogCodes()
    {
        var assembly = typeof(MigrationRunner).Assembly;
        var resources = assembly.GetManifestResourceNames()
            .Where(n => n.Contains("003_stable_permissions", StringComparison.Ordinal)).ToArray();
        Assert.AreEqual(2, resources.Length, "Expected sqlserver and postgresql copies of migration 003.");

        var catalog = PermissionCodes.AllSet;
        foreach (var resource in resources)
        {
            var codes = CodesIn(resource);
            Assert.IsTrue(codes.Count > 0, $"{resource} contains no permission mapping.");
            var unknown = codes.Where(code => !catalog.Contains(code)).ToArray();
            Assert.AreEqual(0, unknown.Length,
                $"{resource} maps to codes missing from PermissionCodes: {string.Join(", ", unknown)}");
        }

        var sqlServer = CodesIn(resources.First(n => n.EndsWith(".sqlserver.sql", StringComparison.Ordinal)));
        var postgres = CodesIn(resources.First(n => n.EndsWith(".postgresql.sql", StringComparison.Ordinal)));
        CollectionAssert.AreEquivalent(sqlServer.ToList(), postgres.ToList(),
            "The two provider copies of migration 003 must map the same permission codes.");
    }
}
