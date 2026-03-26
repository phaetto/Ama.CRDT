namespace Ama.CRDT.Services.GarbageCollection;

using System;
using System.Collections.Generic;

/// <summary>
/// A factory for dynamically generating instances of <see cref="GlobalMinimumVersionPolicy"/>.
/// This allows the global minimum version vector (GMVV) to be evaluated lazily at the time of policy application.
/// </summary>
public sealed class GlobalMinimumVersionPolicyFactory : ICompactionPolicyFactory
{
    private readonly Func<IReadOnlyDictionary<string, long>> globalMinimumVersionsProvider;

    /// <summary>
    /// Initializes a new instance of the <see cref="GlobalMinimumVersionPolicyFactory"/> class.
    /// </summary>
    /// <param name="globalMinimumVersionsProvider">A delegate that provides the current global minimum version vector mapping.</param>
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="globalMinimumVersionsProvider"/> is null.</exception>
    public GlobalMinimumVersionPolicyFactory(Func<IReadOnlyDictionary<string, long>> globalMinimumVersionsProvider)
    {
        this.globalMinimumVersionsProvider = globalMinimumVersionsProvider ?? throw new ArgumentNullException(nameof(globalMinimumVersionsProvider));
    }

    /// <inheritdoc/>
    public ICompactionPolicy CreatePolicy()
    {
        var versions = this.globalMinimumVersionsProvider();
        return new GlobalMinimumVersionPolicy(versions);
    }
}