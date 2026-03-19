using Ama.CRDT.Services.Strategies;
using Shouldly;

namespace Ama.CRDT.UnitTests.Architecture;

public class StrategyConventionTests
{
    [Fact]
    public void AllStrategies_ShouldHaveRequiredTestsBenchmarksAndDocumentation()
    {
        // 1. Find all Strategy types via reflection inside the main CRDT assembly
        var strategyTypes = typeof(ICrdtStrategy).Assembly.GetTypes()
            .Where(t => t.IsClass && !t.IsAbstract)
            .Where(t => t.Name.EndsWith("Strategy") || typeof(ICrdtStrategy).IsAssignableFrom(t))
            .Distinct()
            .ToList();

        var solutionDir = GetSolutionDirectory();
        var missingItems = new List<string>();

        // Load Docs & Benchmarks to check for strategy inclusion
        var docsPath = Path.Combine(solutionDir, "docs", "strategies-reference.md");
        var docsContent = File.Exists(docsPath) ? File.ReadAllText(docsPath) : string.Empty;

        var benchPath = Path.Combine(solutionDir, "Ama.CRDT.Benchmarks", "Models", "StrategyPoco.cs");
        var benchContent = File.Exists(benchPath) ? File.ReadAllText(benchPath) : string.Empty;

        foreach (var strategy in strategyTypes)
        {
            var name = strategy.Name;
            var isDecorator = strategy.Namespace?.Contains("Decorators") == true;

            // 2. Check Unit Tests
            var unitTestPath1 = Path.Combine(solutionDir, "Ama.CRDT.UnitTests", "Services", "Strategies", $"{name}Tests.cs");
            var unitTestPath2 = Path.Combine(solutionDir, "Ama.CRDT.UnitTests", "Services", "Strategies", "Decorators", $"{name}Tests.cs");
            if (!File.Exists(unitTestPath1) && !File.Exists(unitTestPath2))
            {
                missingItems.Add($"[{name}] Missing Unit Test. Expected at {unitTestPath1} or {unitTestPath2}");
            }

            // 3. Check Property Tests (FsCheck)
            var propTestPath1 = Path.Combine(solutionDir, "Ama.CRDT.PropertyTests", "Strategies", $"{name}Properties.cs");
            var propTestPath2 = Path.Combine(solutionDir, "Ama.CRDT.PropertyTests", "Strategies", "Decorators", $"{name}Properties.cs");
            if (!File.Exists(propTestPath1) && !File.Exists(propTestPath2))
            {
                missingItems.Add($"[{name}] Missing Property Test. Expected at {propTestPath1} or {propTestPath2}");
            }

            // 4. Check Documentation
            // Decorator attributes often don't contain the suffix "Strategy", so we check for both literal match and the prefix equivalent.
            var expectedAttributeFragment = $"Crdt{name.Replace("Strategy", "")}";
            if (!docsContent.Contains(name) && !docsContent.Contains(expectedAttributeFragment))
            {
                missingItems.Add($"[{name}] Missing documentation entry in docs/strategies-reference.md");
            }

            // 5. Check Benchmarks (StrategyPoco typically decorates properties like [CrdtLwwStrategy])
            // Decorators are usually applied over other properties, so we skip them here, but require them for core strategies.
            if (!isDecorator && !benchContent.Contains(name))
            {
                missingItems.Add($"[{name}] Missing Benchmark property in Ama.CRDT.Benchmarks/Models/StrategyPoco.cs");
            }
        }

        var errorMessage = $"Found missing conventions for strategies. When adding a new strategy, please include tests, benchmarks, and docs:\n\n{string.Join(Environment.NewLine, missingItems)}";
        
        missingItems.ShouldBeEmpty(errorMessage);
    }

    private static string GetSolutionDirectory()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null && !dir.GetFiles("*.sln").Any())
        {
            dir = dir.Parent;
        }

        if (dir == null)
        {
            throw new InvalidOperationException("Could not find solution directory. Ensure the test is running within the repository.");
        }

        return dir.FullName;
    }
}