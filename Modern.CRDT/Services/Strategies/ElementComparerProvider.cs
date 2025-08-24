namespace Modern.CRDT.Services.Strategies;

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

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
            return DeepEquals(x, y, new HashSet<Tuple<object, object>>());
        }

        public int GetHashCode(object obj)
        {
            ArgumentNullException.ThrowIfNull(obj);
            return DeepGetHashCode(obj, new HashSet<object>());
        }

        private bool DeepEquals(object? x, object? y, ISet<Tuple<object, object>> comparedPairs)
        {
            if (ReferenceEquals(x, y)) return true;
            if (x is null || y is null) return false;
            
            if (comparedPairs.Contains(Tuple.Create(x, y)) || comparedPairs.Contains(Tuple.Create(y, x)))
            {
                return true;
            }
            comparedPairs.Add(Tuple.Create(x, y));

            var typeX = x.GetType();
            if (typeX != y.GetType()) return false;

            if (typeX.IsPrimitive || typeX == typeof(string) || x is decimal)
            {
                return x.Equals(y);
            }

            if (x is IEnumerable enumerableX && y is IEnumerable enumerableY)
            {
                var listX = enumerableX.Cast<object>().ToList();
                var listY = enumerableY.Cast<object>().ToList();

                if (listX.Count != listY.Count) return false;

                for (var i = 0; i < listX.Count; i++)
                {
                    if (!DeepEquals(listX[i], listY[i], comparedPairs))
                    {
                        return false;
                    }
                }
                
                return true;
            }
            
            if (typeX.IsClass || (typeX.IsValueType && !typeX.IsEnum))
            {
                var properties = typeX.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                    .Where(p => p.CanRead);
                
                foreach (var property in properties)
                {
                    var valueX = property.GetValue(x);
                    var valueY = property.GetValue(y);

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
            
            if (type.IsClass || (type.IsValueType && !type.IsEnum))
            {
                var properties = type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                    .Where(p => p.CanRead)
                    .OrderBy(p => p.Name);
            
                var combinedHashCode = new HashCode();
                foreach (var property in properties)
                {
                    var value = property.GetValue(obj);
                    combinedHashCode.Add(value is not null ? DeepGetHashCode(value, visited) : 0);
                }

                return combinedHashCode.ToHashCode();
            }
            
            return obj.GetHashCode();
        }
    }
}