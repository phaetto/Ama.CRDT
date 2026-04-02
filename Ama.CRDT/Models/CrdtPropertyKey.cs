namespace Ama.CRDT.Models;

using System;

/// <summary>
/// A unique identifier for a property, used to configure and resolve strategies without relying on System.Reflection.PropertyInfo.
/// </summary>
/// <param name="DeclaringType">The type that declares the property.</param>
/// <param name="PropertyName">The precise name of the property.</param>
public readonly record struct CrdtPropertyKey(Type DeclaringType, string PropertyName) : IEquatable<CrdtPropertyKey>;