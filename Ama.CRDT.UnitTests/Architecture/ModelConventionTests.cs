using Ama.CRDT.Models;
using Shouldly;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;

namespace Ama.CRDT.UnitTests.Architecture;

public sealed class ModelConventionTests
{
    [Fact]
    public void AllModels_ShouldBeTestedForSerialization()
    {
        // 1. Find all Data Model types via reflection inside the main CRDT assembly
        var modelTypes = typeof(CrdtMetadata).Assembly.GetTypes()
            .Where(t => t.Namespace != null && t.Namespace.StartsWith("Ama.CRDT.Models"))
            .Where(t => !t.Namespace!.StartsWith("Ama.CRDT.Models.Serialization")) // Exclude serialization infra
            .Where(t => !t.Namespace!.StartsWith("Ama.CRDT.Models.Aot")) // Exclude Aot infra
            .Where(t => t.IsClass || (t.IsValueType && !t.IsEnum))
            .Where(t => !t.IsInterface && !typeof(Delegate).IsAssignableFrom(t))
            .Where(t => !(t.IsAbstract && t.IsSealed)) // Exclude static classes
            .Where(t => !t.IsAbstract) // Exclude abstract base classes
            .Where(t => !t.GetCustomAttributes(typeof(CompilerGeneratedAttribute), true).Any())
            .Where(t => !t.Name.Contains('<')) // Exclude compiler generated hidden types
            .Where(t => !t.Name.EndsWith("Exception") 
                        && !t.Name.EndsWith("Converter") 
                        && !t.Name.EndsWith("Context") 
                        && !t.Name.EndsWith("Resolver")
                        && !t.Name.EndsWith("Builder")
                        && !t.Name.EndsWith("CrdtPropertyKey"))
            .Distinct()
            .ToList();

        var solutionDir = GetSolutionDirectory();
        var unitTestsDir = Path.Combine(solutionDir, "Ama.CRDT.UnitTests");
        
        // 2. Load all Serialization test files to check for model inclusion
        var testFiles = Directory.GetFiles(unitTestsDir, "*.cs", SearchOption.AllDirectories)
            .Where(f => f.Contains("Serialization", StringComparison.OrdinalIgnoreCase) || 
                        f.EndsWith("JsonConverterTests.cs", StringComparison.OrdinalIgnoreCase))
            .ToList();

        var allTestContent = string.Join(Environment.NewLine, testFiles.Select(File.ReadAllText));
        var missingItems = new List<string>();

        foreach (var model in modelTypes)
        {
            var name = model.Name;
            
            // Clean up generic type names (e.g. MyModel`1 -> MyModel)
            if (name.Contains('`'))
            {
                name = name[..name.IndexOf('`')];
            }

            // 3. Use regex to ensure whole word match so "Node" doesn't falsely match "AddNodeIntent"
            var regex = new Regex($@"\b{name}\b");
            if (!regex.IsMatch(allTestContent))
            {
                missingItems.Add($"[{model.FullName}] Missing serialization test. The model name '{name}' was not found in any Serialization test file.");
            }
        }

        var errorMessage = $"Found missing serialization tests for models. Please ensure they are serialized and deserialized in a test inside the Serialization folder:\n\n{string.Join(Environment.NewLine, missingItems)}";
        
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