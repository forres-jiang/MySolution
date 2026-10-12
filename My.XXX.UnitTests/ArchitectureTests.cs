using My.XXX.Application;
using My.XXX.Domain.Abstractions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.IO;
using System.Linq;
using System.Xml.Linq;

namespace My.XXX.UnitTests;

[TestClass]
public sealed class ArchitectureTests
{
    [TestMethod]
    public void DomainHasNoApplicationOrThirdPartyDependencies()
    {
        var assembly = typeof(DomainResult).Assembly;
        foreach (var reference in assembly.GetReferencedAssemblies())
            Assert.IsTrue(reference.Name == "netstandard" || reference.Name == "System" || reference.Name!.StartsWith("System.", StringComparison.Ordinal),
                $"Domain cannot reference {reference.Name}.");
        Assert.IsFalse(assembly.GetExportedTypes().Any(type => type.Namespace?.EndsWith(".Ports", StringComparison.Ordinal) == true),
            "Use-case storage and transaction ports belong to Application.");
        var project = XDocument.Load(Path.Combine(Root(), "My.XXX.Domain", "03My.XXX.Domain.csproj"));
        Assert.IsFalse(project.Descendants("PackageReference").Any(), "Keep the domain independent of third-party packages.");
    }

    [TestMethod]
    public void MutationHelpersAreNotPublicUseCaseEntrypoints()
    {
        var helpers = typeof(ApplicationRegistration).Assembly.GetTypes()
            .Where(type => type.Name.EndsWith("Mutations", StringComparison.Ordinal)).ToArray();
        Assert.IsTrue(helpers.Length > 0);
        foreach (var helper in helpers)
            Assert.IsFalse(helper.IsVisible, $"{helper.Name} must only be called inside application use cases.");
    }

    [TestMethod]
    public void DomainNavigationDoesNotExposeFrameworkFieldNames()
    {
        var forbidden = new[] { "ControllerName", "ActionName", "Component", "LinkTarget" };
        foreach (var type in new[] { typeof(My.XXX.Domain.Menus.Models.MenuState), typeof(My.XXX.Domain.Menus.Models.MenuUpdate) })
            Assert.IsFalse(type.GetProperties().Any(property => forbidden.Contains(property.Name)));
        var catalog = (System.Collections.Generic.ISet<string>)My.XXX.Domain.Authorization.Policies.PermissionCodes.AllSet;
        Assert.ThrowsExactly<NotSupportedException>(() => catalog.Add("test.invalid"));
        Assert.IsFalse(catalog.Contains("test.invalid"));
    }

    private static string Root()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory != null && !File.Exists(Path.Combine(directory.FullName, "MySolution.sln")))
            directory = directory.Parent;
        return directory?.FullName ?? throw new InvalidOperationException("Repository root unavailable.");
    }

    [TestMethod]
    public void ProjectDependenciesPointInward()
    {
        Check("Domain", Array.Empty<string>());
        Check("Application", new[] { "Domain" });
        Check("Persistence", new[] { "Domain", "Application" });
        Check("Infrastructure", new[] { "Domain", "Application" });
        Check("APIs", new[] { "Domain", "Application", "Persistence", "Infrastructure" });
        Check("UnitTests", new[] { "Domain", "Application" });
        Check("IntegrationTests", new[] { "APIs" });
    }

    private static void Check(string project, string[] allowed)
    {
        var path = Directory.GetFiles(Path.Combine(Root(), "My.XXX." + project), "*.csproj").Single();
        var xml = XDocument.Load(path);
        foreach (var reference in xml.Descendants("ProjectReference"))
        {
            var name = ((string?)reference.Attribute("Include"))!.Replace('\\', '/').Split('/')[1];
            Assert.IsTrue(allowed.Any(layer => name == "My.XXX." + layer), $"{project} cannot reference {name}.");
        }
        if (project is "Domain" or "Application")
        {
            Assert.IsFalse(xml.Descendants("FrameworkReference").Any(), "Inner layers cannot reference ASP.NET.");
            foreach (var package in xml.Descendants("PackageReference"))
            {
                var name = ((string?)package.Attribute("Include"))!;
                Assert.IsFalse(new[] { "Microsoft.AspNetCore", "Microsoft.EntityFrameworkCore", "linq2db", "Npgsql", "Microsoft.Data.SqlClient", "StackExchange.Redis" }
                    .Any(prefix => name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)), $"{project} cannot depend on {name}.");
            }
        }
    }
}
