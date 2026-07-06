using System.Reflection;
using CreditFlow.Api.Endpoints;
using CreditFlow.Domain.Applications;
using CreditFlow.Domain.SharedKernel;

namespace CreditFlow.ArchitectureTests.Rules;

public sealed class ArchitectureDependencyTests
{
    private static readonly Assembly DomainAssembly = typeof(LoanApplication).Assembly;
    private static readonly Assembly ApiAssembly = typeof(ApplicationEndpoints).Assembly;

    [Fact]
    public void Domain_Should_Not_Reference_Api_Infrastructure_Or_Workers()
    {
        var forbiddenReferences = new[]
        {
            "CreditFlow.Api",
            "CreditFlow.Infrastructure",
            "CreditFlow.DecisionWorker",
            "CreditFlow.AuditWorker"
        };

        var references = GetReferencedAssemblyNames(DomainAssembly);

        foreach (var forbiddenReference in forbiddenReferences)
        {
            Assert.DoesNotContain(forbiddenReference, references);
        }
    }

    [Fact]
    public void Domain_Should_Not_Reference_AspNetCore_Or_Aws_Sdks()
    {
        var references = GetReferencedAssemblyNames(DomainAssembly);

        Assert.DoesNotContain(references, reference =>
            reference.StartsWith("Microsoft.AspNetCore", StringComparison.Ordinal) ||
            reference.StartsWith("Amazon.", StringComparison.Ordinal) ||
            reference.StartsWith("AWSSDK", StringComparison.Ordinal));
    }

    [Fact]
    public void Api_Should_Not_Reference_Worker_Projects()
    {
        var references = GetReferencedAssemblyNames(ApiAssembly);

        Assert.DoesNotContain("CreditFlow.DecisionWorker", references);
        Assert.DoesNotContain("CreditFlow.AuditWorker", references);
    }

    [Fact]
    public void Infrastructure_Should_Not_Reference_Api_Or_Workers()
    {
        var infrastructureAssembly = LoadOutputAssembly("CreditFlow.Infrastructure");
        var references = GetReferencedAssemblyNames(infrastructureAssembly);

        Assert.DoesNotContain("CreditFlow.Api", references);
        Assert.DoesNotContain("CreditFlow.DecisionWorker", references);
        Assert.DoesNotContain("CreditFlow.AuditWorker", references);
    }

    [Fact]
    public void Workers_Should_Not_Reference_Api()
    {
        var decisionWorkerAssembly = LoadOutputAssembly("CreditFlow.DecisionWorker");
        var auditWorkerAssembly = LoadOutputAssembly("CreditFlow.AuditWorker");

        Assert.DoesNotContain("CreditFlow.Api", GetReferencedAssemblyNames(decisionWorkerAssembly));
        Assert.DoesNotContain("CreditFlow.Api", GetReferencedAssemblyNames(auditWorkerAssembly));
    }

    [Fact]
    public void Domain_Event_Types_Should_Implement_IDomainEvent()
    {
        var eventTypes = DomainAssembly
            .GetTypes()
            .Where(type =>
                type.IsClass &&
                type.Namespace is not null &&
                type.Namespace.Contains(".Events", StringComparison.Ordinal))
            .ToArray();

        Assert.NotEmpty(eventTypes);

        foreach (var eventType in eventTypes)
        {
            Assert.True(
                typeof(IDomainEvent).IsAssignableFrom(eventType),
                $"{eventType.FullName} should implement {nameof(IDomainEvent)}.");

            Assert.True(
                eventType.IsSealed,
                $"{eventType.FullName} should be sealed.");
        }
    }

    [Fact]
    public void Domain_Aggregates_Should_Not_Expose_Public_Setters()
    {
        var aggregateTypes = DomainAssembly
            .GetTypes()
            .Where(type =>
                type.IsClass &&
                !type.IsAbstract &&
                type != typeof(AggregateRoot) &&
                typeof(AggregateRoot).IsAssignableFrom(type))
            .ToArray();

        Assert.NotEmpty(aggregateTypes);

        foreach (var aggregateType in aggregateTypes)
        {
            var publicProperties = aggregateType.GetProperties(
                BindingFlags.Instance | BindingFlags.Public);

            foreach (var property in publicProperties)
            {
                Assert.False(
                    property.SetMethod?.IsPublic == true,
                    $"{aggregateType.FullName}.{property.Name} should not expose a public setter.");
            }
        }
    }

    private static string[] GetReferencedAssemblyNames(Assembly assembly)
    {
        return [.. assembly
            .GetReferencedAssemblies()
            .Select(reference => reference.Name!)];
    }

    private static Assembly LoadOutputAssembly(string assemblyName)
    {
        var assemblyPath = Path.Combine(
            AppContext.BaseDirectory,
            $"{assemblyName}.dll");

        Assert.True(
            File.Exists(assemblyPath),
            $"Expected assembly output was not found: {assemblyPath}");

        return Assembly.LoadFrom(assemblyPath);
    }
}
