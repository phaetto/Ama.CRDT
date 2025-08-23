namespace Modern.CRDT.ShowCase.Services;

using System;
using Modern.CRDT.Services.Strategies;

/// <summary>
/// A custom implementation of <see cref="IElementComparer"/> that allows the <see cref="ArrayLcsStrategy"/>
/// to identify unique strings using a case-insensitive comparison.
/// </summary>
public sealed class CaseInsensitiveStringComparer : IElementComparer
{
    public bool CanCompare(Type type) => type == typeof(string);

    public new bool Equals(object? x, object? y)
    {
        if (ReferenceEquals(x, y)) return true;
        if (x is null || y is null) return false;

        if (x is string strX && y is string strY)
        {
            return string.Equals(strX, strY, StringComparison.OrdinalIgnoreCase);
        }

        return object.Equals(x, y);
    }

    public int GetHashCode(object obj)
    {
        ArgumentNullException.ThrowIfNull(obj);

        if (obj is string str)
        {
            return str.GetHashCode(StringComparison.OrdinalIgnoreCase);
        }

        return obj.GetHashCode();
    }
}