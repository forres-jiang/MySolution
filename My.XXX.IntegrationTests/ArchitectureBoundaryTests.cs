using My.XXX.APIs;
using My.XXX.APIs.Common.Mapping;
using My.XXX.APIs.Common.Responses;
using My.XXX.Application.AccessControl.Ports;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;

namespace My.XXX.IntegrationTests;

[TestClass]
public sealed class ArchitectureBoundaryTests
{
    private static readonly HashSet<Type> StoragePorts = typeof(IAccessControlTransaction).Assembly.GetTypes()
        .Where(type => type.IsInterface && type.Namespace?.EndsWith(".Ports", StringComparison.Ordinal) == true &&
            (type.Name.EndsWith("Repository", StringComparison.Ordinal) || type.Name.EndsWith("Store", StringComparison.Ordinal) ||
             type.Name.EndsWith("Transaction", StringComparison.Ordinal) || type.Name.EndsWith("WriteSession", StringComparison.Ordinal)))
        .ToHashSet();

    // Anchor the HTTP-adapter namespaces to real types so a rename breaks the build instead of silently matching nothing.
    private static readonly string[] HttpAdapterNamespaces =
    {
        typeof(WireMapper).Namespace!,
    };

    [TestMethod]
    public void HttpAdaptersCannotBypassUseCasesToAccessStorage()
    {
        var types = typeof(Program).Assembly.GetTypes().Where(type =>
            typeof(Microsoft.AspNetCore.Mvc.ControllerBase).IsAssignableFrom(type) ||
            type == typeof(ResultResponseExtensions) || type == typeof(ErrorResponseExtensions) ||
            HttpAdapterNamespaces.Any(ns => type.Namespace?.StartsWith(ns, StringComparison.Ordinal) == true)).ToArray();
        Assert.IsTrue(types.Any(type => typeof(Microsoft.AspNetCore.Mvc.ControllerBase).IsAssignableFrom(type)), "No controllers inspected.");

        foreach (var type in types)
            foreach (var dependency in Dependencies(type).SelectMany(Expand).Distinct())
                Assert.IsFalse(StoragePorts.Contains(dependency) ||
                    dependency.Assembly.GetName().Name is "My.XXX.Persistence" or "My.XXX.Infrastructure" ||
                    dependency.Namespace?.StartsWith("LinqToDB", StringComparison.Ordinal) == true,
                    $"{type.FullName} bypasses use cases through {dependency.FullName}.");
    }

    // Inspect both signatures and IL, including async state machines, so static calls,
    // local variables and service-locator lookups cannot evade constructor-only checks.
    private static IEnumerable<Type> Dependencies(Type type)
    {
        const BindingFlags flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly;
        if (type.BaseType != null) yield return type.BaseType;
        foreach (var contract in type.GetInterfaces()) yield return contract;
        foreach (var field in type.GetFields(flags)) yield return field.FieldType;
        foreach (var method in type.GetMethods(flags).Cast<MethodBase>().Concat(type.GetConstructors(flags)))
        {
            if (method is MethodInfo info) yield return info.ReturnType;
            foreach (var parameter in method.GetParameters()) yield return parameter.ParameterType;
            var body = method.GetMethodBody();
            if (body == null) continue;
            foreach (var local in body.LocalVariables) yield return local.LocalType;
            var bytes = body.GetILAsByteArray();
            if (bytes == null) continue;
            for (var offset = 0; offset < bytes.Length;)
            {
                var code = (short)bytes[offset++];
                if (code == 0xfe) code = (short)(0xfe00 | bytes[offset++]);
                var operand = Opcodes[code].OperandType;
                if (operand is OperandType.InlineField or OperandType.InlineMethod or OperandType.InlineTok or OperandType.InlineType)
                {
                    var member = method.Module.ResolveMember(BitConverter.ToInt32(bytes, offset),
                        type.GetGenericArguments(), method.IsGenericMethod ? method.GetGenericArguments() : null);
                    if (member is Type target) yield return target;
                    else if (member?.DeclaringType != null) yield return member.DeclaringType;
                    if (member is MethodInfo called)
                    {
                        yield return called.ReturnType;
                        foreach (var argument in called.GetGenericArguments()) yield return argument;
                    }
                }
                offset += operand switch
                {
                    OperandType.InlineNone => 0,
                    OperandType.ShortInlineBrTarget or OperandType.ShortInlineI or OperandType.ShortInlineVar => 1,
                    OperandType.InlineVar => 2,
                    OperandType.InlineI8 or OperandType.InlineR => 8,
                    OperandType.InlineSwitch => 4 + 4 * BitConverter.ToInt32(bytes, offset),
                    _ => 4
                };
            }
        }
    }

    private static IEnumerable<Type> Expand(Type type)
    {
        yield return type;
        if (type.HasElementType)
            foreach (var nested in Expand(type.GetElementType()!)) yield return nested;
        foreach (var argument in type.GetGenericArguments())
            foreach (var nested in Expand(argument)) yield return nested;
    }

    private static readonly Dictionary<short, OpCode> Opcodes = typeof(OpCodes).GetFields(BindingFlags.Public | BindingFlags.Static)
        .Where(field => field.FieldType == typeof(OpCode)).Select(field => (OpCode)field.GetValue(null)!)
        .ToDictionary(code => code.Value);
}
