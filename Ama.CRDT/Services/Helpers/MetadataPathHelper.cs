namespace Ama.CRDT.Services.Helpers;

/// <summary>
/// Provides formalized methods for constructing and evaluating metadata state paths,
/// ensuring that decorator strategies can isolate their state without colliding with
/// base strategies or causing string mangling bugs.
/// </summary>
public static class MetadataPathHelper
{
    private const char DecoratorDelimiter = '|';

    /// <summary>
    /// Generates a unique state path for a decorator strategy based on the target JSON path.
    /// </summary>
    /// <param name="jsonPath">The base JSON path (e.g., "$.users[0].name").</param>
    /// <param name="decoratorKey">A unique identifier for the decorator (e.g., "Epoch").</param>
    /// <returns>A formalized metadata key string.</returns>
    public static string GetDecoratorPath(string jsonPath, string decoratorKey)
    {
        var basePath = GetBasePath(jsonPath);
        return $"{basePath}{DecoratorDelimiter}{decoratorKey}";
    }

    /// <summary>
    /// Extracts the base JSON path from a metadata state key, removing any decorator suffixes.
    /// </summary>
    /// <param name="stateKey">The metadata dictionary key.</param>
    /// <returns>The clean JSON path.</returns>
    public static string GetBasePath(string stateKey)
    {
        var idx = stateKey.IndexOf(DecoratorDelimiter);
        return idx >= 0 ? stateKey.Substring(0, idx) : stateKey;
    }

    /// <summary>
    /// Determines if a given state key represents the target base path itself, a decorator of it,
    /// or any nested child property within it.
    /// </summary>
    /// <param name="stateKey">The metadata dictionary key to evaluate.</param>
    /// <param name="targetBasePath">The base path to match against.</param>
    /// <returns>True if it is a match or a descendant; otherwise false.</returns>
    public static bool IsChildOrSelfPath(string stateKey, string targetBasePath)
    {
        var actualKeyBase = GetBasePath(stateKey);
        
        if (actualKeyBase == targetBasePath) return true;
        if (actualKeyBase.StartsWith(targetBasePath + ".")) return true;
        if (actualKeyBase.StartsWith(targetBasePath + "[")) return true;
        
        return false;
    }
}