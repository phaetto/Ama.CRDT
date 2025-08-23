namespace Modern.CRDT.ShowCase.Services;
using System;
using Modern.CRDT.Services.Strategies;
using Modern.CRDT.ShowCase.Models;

/// <summary>
/// A custom implementation of <see cref="IElementComparer"/> that allows the <see cref="ArrayLcsStrategy"/>
/// to identify unique <see cref="User"/> objects based on their <c>Id</c> property, rather than by object reference.
/// </summary>
public sealed class UserByIdComparer : IElementComparer
{
    public bool CanCompare(Type type) => type == typeof(User);

    public new bool Equals(object? x, object? y)
    {
        if (ReferenceEquals(x, y)) return true;
        if (x is null || y is null) return false;

        if (x is User userX && y is User userY)
        {
            return userX.Id == userY.Id;
        }

        return object.Equals(x, y);
    }

    public int GetHashCode(object obj)
    {
        ArgumentNullException.ThrowIfNull(obj);

        if (obj is User user)
        {
            return user.Id.GetHashCode();
        }

        return obj.GetHashCode();
    }
}