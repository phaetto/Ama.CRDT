namespace Ama.CRDT.Services.Providers;

using Ama.CRDT.Models.Aot;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;

/// <inheritdoc/>
internal sealed class ElementComparerProvider : IElementComparerProvider
{
    private readonly IEnumerable<IElementComparer> comparers;
    private readonly ObjectDeepEqualityComparer defaultComparer;

    /// <summary>
    /// Initializes a new instance of the <see cref="ElementComparerProvider"/> class.
    /// </summary>
    /// <param name="comparers">An enumerable of registered <see cref="IElementComparer"/> instances.</param>
    /// <param name="aotContexts">An enumerable of registered <see cref="CrdtAotContext"/> instances.</param>
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="comparers"/> or <paramref name="aotContexts"/> is null.</exception>
    public ElementComparerProvider(IEnumerable<IElementComparer> comparers, IEnumerable<CrdtAotContext> aotContexts)
    {
        this.comparers = comparers ?? throw new ArgumentNullException(nameof(comparers));
        defaultComparer = new ObjectDeepEqualityComparer(aotContexts ?? throw new ArgumentNullException(nameof(aotContexts)));
    }

    /// <inheritdoc/>
    public IEqualityComparer<object> GetComparer([DisallowNull] Type elementType)
    {
        ArgumentNullException.ThrowIfNull(elementType);
        return (IEqualityComparer<object>?)comparers.FirstOrDefault(c => c.CanCompare(elementType)) ?? defaultComparer;
    }
    
    private sealed class ObjectDeepEqualityComparer : IEqualityComparer<object>
    {
        private readonly IEnumerable<CrdtAotContext> aotContexts;

        public ObjectDeepEqualityComparer(IEnumerable<CrdtAotContext> aotContexts)
        {
            this.aotContexts = aotContexts;
        }

        public new bool Equals(object? x, object? y)
        {
            return DeepEquals(x, y, new HashSet<ObjectPair>());
        }

        public int GetHashCode(object obj)
        {
            ArgumentNullException.ThrowIfNull(obj);
            return DeepGetHashCode(obj, new HashSet<object>());
        }

        private bool DeepEquals(object? x, object? y, ISet<ObjectPair> comparedPairs)
        {
            if (ReferenceEquals(x, y)) return true;
            if (x is null || y is null) return false;
            
            var pair = new ObjectPair(x, y);
            var reversePair = new ObjectPair(y, x);
            if (comparedPairs.Contains(pair) || comparedPairs.Contains(reversePair))
            {
                return true;
            }
            comparedPairs.Add(pair);

            var typeX = x.GetType();
            if (typeX != y.GetType()) return false;

            if (typeX.IsPrimitive || typeX == typeof(string) || x is decimal)
            {
                return x.Equals(y);
            }

            if (x is IEnumerable enumerableX && y is IEnumerable enumerableY)
            {
                var enumX = enumerableX.GetEnumerator();
                var enumY = enumerableY.GetEnumerator();
                try
                {
                    while (true)
                    {
                        var hasNextX = enumX.MoveNext();
                        var hasNextY = enumY.MoveNext();

                        if (hasNextX != hasNextY) return false;
                        if (!hasNextX) break;

                        if (!DeepEquals(enumX.Current, enumY.Current, comparedPairs))
                        {
                            return false;
                        }
                    }
                    return true;
                }
                finally
                {
                    if (enumX is IDisposable dispX) dispX.Dispose();
                    if (enumY is IDisposable dispY) dispY.Dispose();
                }
            }
            
            CrdtTypeInfo? typeInfo = null;
            foreach (var context in aotContexts)
            {
                typeInfo = context.GetTypeInfo(typeX);
                if (typeInfo != null) break;
            }

            if (typeInfo != null)
            {
                foreach (var property in typeInfo.Properties.Values)
                {
                    if (!property.CanRead) continue;
                    
                    var valueX = property.Getter!(x);
                    var valueY = property.Getter!(y);

                    if (!DeepEquals(valueX, valueY, comparedPairs))
                    {
                        return false;
                    }
                }

                return true;
            }
            
            return x.Equals(y);
        }

        private int DeepGetHashCode(object obj, ISet<object> visited)
        {
            ArgumentNullException.ThrowIfNull(obj);
            
            if (visited.Contains(obj))
            {
                return 0;
            }
            
            var type = obj.GetType();
            
            if (type.IsPrimitive || type == typeof(string) || obj is decimal)
            {
                return obj.GetHashCode();
            }

            visited.Add(obj);

            if (obj is IEnumerable enumerable)
            {
                var hashCode = new HashCode();
                foreach (var item in enumerable)
                {
                    hashCode.Add(item is not null ? DeepGetHashCode(item, visited) : 0);
                }
                return hashCode.ToHashCode();
            }
            
            CrdtTypeInfo? typeInfo = null;
            foreach (var context in aotContexts)
            {
                typeInfo = context.GetTypeInfo(type);
                if (typeInfo != null) break;
            }

            if (typeInfo != null)
            {
                var properties = typeInfo.Properties.Values
                    .Where(p => p.CanRead)
                    .OrderBy(p => p.Name, StringComparer.Ordinal);
            
                var combinedHashCode = new HashCode();
                foreach (var property in properties)
                {
                    var value = property.Getter!(obj);
                    combinedHashCode.Add(value is not null ? DeepGetHashCode(value, visited) : 0);
                }

                return combinedHashCode.ToHashCode();
            }
            
            return obj.GetHashCode();
        }

        private readonly record struct ObjectPair(object Left, object Right);
    }
}