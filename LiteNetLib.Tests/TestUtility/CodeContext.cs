using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Loader;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace LiteNetLib.Tests.TestUtility;

internal class CodeContext : IDisposable
{
    private static readonly string[] DefaultNamespaces =
    {
        "System",
        "System.IO",
        "System.Net",
        "System.Linq",
        "System.Text",
        "System.Text.RegularExpressions",
        "System.Collections.Generic"
    };

    private readonly AssemblyLoadContext _assemblyContext;

    private readonly Lazy<MetadataReference[]> _managedRuntimeReferences = new(() =>
    {
        var netRuntimePath = Path.GetDirectoryName(typeof(object).Assembly.Location);
        if (!Directory.Exists(netRuntimePath))
            throw new DirectoryNotFoundException($".NET Runtime path '{netRuntimePath}' is not found");

        List<MetadataReference> references = new();
        foreach (var file in Directory.GetFiles(netRuntimePath, "System.*"))
        {
            if (file.Contains(".Native.", StringComparison.Ordinal)) continue;

            references.Add(MetadataReference.CreateFromFile(file));
        }

        return references.ToArray();
    });

    public CodeContext()
    {
        _assemblyContext = new AssemblyLoadContext("Temporary " + Guid.NewGuid(), true);
    }

    public void Dispose()
    {
        _assemblyContext.Unload();
    }

    public void AddAssemblyFromCode(string assemblyName, string code, params Assembly[] references)
    {
        var compilation = CSharpCompilation.Create(
            assemblyName,
            new[] { CSharpSyntaxTree.ParseText(code) },
            _managedRuntimeReferences.Value.Concat(
                references?.Select(r => MetadataReference.CreateFromFile(r.Location)) ??
                ArraySegment<PortableExecutableReference>.Empty),
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary)
                .WithUsings(DefaultNamespaces)
                .WithOverflowChecks(true));
        // Compile code in memory to an assembly image
        using MemoryStream assemblyMemory = new();
        var emitResult = compilation.Emit(assemblyMemory);
        IEnumerable<Diagnostic> failures = emitResult.Diagnostics.Where(diagnostic =>
            diagnostic.IsWarningAsError || diagnostic.Severity == DiagnosticSeverity.Error).ToArray();
        if (failures.Any())
            throw new Exception($"Compilation errors{Environment.NewLine}{string.Join(Environment.NewLine, failures)}");

        // Load assembly image into .NET assembly context with unloading support
        assemblyMemory.Position = 0;
        _assemblyContext.LoadFromStream(assemblyMemory);
    }

    public static CodeContext CreateAndLoad(string assemblyName, string code, params Assembly[] references)
    {
        ArgumentException.ThrowIfNullOrEmpty(assemblyName);
        ArgumentException.ThrowIfNullOrEmpty(code);

        var context = new CodeContext();
        context.AddAssemblyFromCode(assemblyName, code, references);
        return context;
    }

    public Type GetType(string typeName)
    {
        var typeParts = typeName.Split(',', StringSplitOptions.TrimEntries);
        var applicableAssemblies = _assemblyContext.Assemblies;
        if (typeParts.Length >= 2)
            applicableAssemblies = applicableAssemblies.Where(a => a.GetName().Name == typeParts[1]);
        foreach (var assembly in applicableAssemblies)
        {
            var type = assembly.GetType(typeParts[0]);
            if (type != null) return type;
        }

        return null;
    }
}
