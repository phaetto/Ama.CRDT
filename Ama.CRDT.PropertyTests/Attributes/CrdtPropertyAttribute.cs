namespace Ama.CRDT.PropertyTests.Attributes;

using FsCheck.Xunit;

/// <summary>
/// A global property attribute for FsCheck tests that allows centralizing the configuration.
/// Replaces the default [CrdtProperty] attribute to control generation permutations globally.
/// </summary>
public sealed class CrdtPropertyAttribute : PropertyAttribute
{
    public CrdtPropertyAttribute()
    {
        // 1. Set your global default for all tests here
        MaxTest = 10000; 
        
        // 2. (Optional) Read from an environment variable to run more permutations in CI/CD pipelines
        var envMaxTest = Environment.GetEnvironmentVariable("FSCHECK_MAX_TESTS");
        if (int.TryParse(envMaxTest, out var parsedMaxTest) && parsedMaxTest > 0)
        {
            MaxTest = parsedMaxTest;
        }
    }
}