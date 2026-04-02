namespace Ama.CRDT.Services.Providers;

using Ama.CRDT.Models;
using System;
using System.Linq.Expressions;
using System.Reflection;

/// <summary>
/// A fluent builder to configure CRDT strategies for a specific entity type.
/// </summary>
/// <typeparam name="T">The type of the entity being configured.</typeparam>
public sealed class CrdtEntityBuilder<T> where T : class
{
    private readonly CrdtModelBuilder builder;

    internal CrdtEntityBuilder(CrdtModelBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        this.builder = builder;
    }

    /// <summary>
    /// Selects a property on the entity to configure its CRDT strategy.
    /// </summary>
    /// <typeparam name="TProperty">The type of the property being configured.</typeparam>
    /// <param name="expression">An expression targeting the property (e.g., <c>x => x.MyProperty</c> or deep paths like <c>x => x.Config.Setting</c>).</param>
    /// <returns>A builder to apply strategies and decorators to the selected property.</returns>
    public CrdtPropertyBuilder<T, TProperty> Property<TProperty>(Expression<Func<T, TProperty>> expression)
    {
        ArgumentNullException.ThrowIfNull(expression);
        
        MemberExpression? me = expression.Body as MemberExpression;
        if (me == null && expression.Body is UnaryExpression ue)
        {
            me = ue.Operand as MemberExpression;
        }

        if (me == null || me.Member.MemberType != MemberTypes.Property)
        {
            throw new ArgumentException("Expression must be a property access.", nameof(expression));
        }

        var key = new CrdtPropertyKey(me.Member.DeclaringType ?? typeof(T), me.Member.Name);
        return new CrdtPropertyBuilder<T, TProperty>(this.builder, this, key);
    }
}