namespace Modern.CRDT.Services.Strategies;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;

/// <summary>
/// Implements the provider logic to select a registered <see cref="IElementComparer"/> or a default one for use by ArrayLcsStrategy.
/// </summary>
public sealed class ElementComparerProvider : IElementComparerProvider
{
    private readonly IEnumerable<IElementComparer> comparers;
    private readonly ObjectDeepEqualityComparer defaultComparer = new();

    public ElementComparerProvider(IEnumerable<IElementComparer> comparers)
    {
        this.comparers = comparers ?? throw new ArgumentNullException(nameof(comparers));
    }

    /// <inheritdoc/>
    public IEqualityComparer<object> GetComparer(Type elementType)
    {
        return comparers.FirstOrDefault(c => c.CanCompare(elementType)) ?? defaultComparer as IEqualityComparer<object>;
    }
    
    private sealed class ObjectDeepEqualityComparer : IEqualityComparer<object>
    {
        public new bool Equals(object? x, object? y)
        {
            if (ReferenceEquals(x, y)) return true;
            if (x is null || y is null) return false;
            
            if (x.Equals(y)) return true;
            
            return JsonSerializer.Serialize(x) == JsonSerializer.Serialize(y);
        }

        public int GetHashCode(object obj)
        {
            ArgumentNullException.ThrowIfNull(obj);
            
            return JsonSerializer.Serialize(obj).GetHashCode();
        }
    }
}