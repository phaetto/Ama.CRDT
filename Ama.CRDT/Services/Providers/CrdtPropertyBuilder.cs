namespace Ama.CRDT.Services.Providers;

using Ama.CRDT.Models;
using Ama.CRDT.Services.Strategies;
using System;
using System.Linq.Expressions;

/// <summary>
/// A fluent builder to configure CRDT strategies and decorators for a specific property.
/// </summary>
/// <typeparam name="T">The type of the entity containing the property.</typeparam>
/// <typeparam name="TProperty">The type of the property being configured.</typeparam>
public sealed class CrdtPropertyBuilder<T, TProperty> where T : class
{
    private readonly CrdtModelBuilder builder;
    private readonly CrdtEntityBuilder<T> entityBuilder;
    private readonly CrdtPropertyKey propertyKey;

    internal CrdtPropertyBuilder(CrdtModelBuilder builder, CrdtEntityBuilder<T> entityBuilder, CrdtPropertyKey propertyKey)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(entityBuilder);

        this.builder = builder;
        this.entityBuilder = entityBuilder;
        this.propertyKey = propertyKey;
    }

    /// <summary>
    /// Assigns the core CRDT strategy to the property.
    /// </summary>
    /// <typeparam name="TStrategy">The CRDT strategy type to apply (e.g., <see cref="LwwStrategy"/>).</typeparam>
    /// <returns>The same builder instance so that multiple calls can be chained.</returns>
    public CrdtPropertyBuilder<T, TProperty> HasStrategy<TStrategy>() where TStrategy : class, ICrdtStrategy
    {
        this.builder.AddStrategy(this.propertyKey, typeof(TStrategy));
        return this;
    }

    /// <summary>
    /// Adds a CRDT decorator strategy to the property. Decorators are applied in the order they are registered.
    /// </summary>
    /// <typeparam name="TDecorator">The CRDT decorator type to apply (e.g., <see cref="Strategies.Decorators.ApprovalQuorumStrategy"/>).</typeparam>
    /// <returns>The same builder instance so that multiple calls can be chained.</returns>
    public CrdtPropertyBuilder<T, TProperty> HasDecorator<TDecorator>() where TDecorator : class, ICrdtStrategy
    {
        this.builder.AddDecorator(this.propertyKey, typeof(TDecorator));
        return this;
    }

    /// <summary>
    /// Selects another property on the same entity to configure its CRDT strategy, allowing for fluent chaining.
    /// </summary>
    /// <typeparam name="TNextProperty">The type of the next property being configured.</typeparam>
    /// <param name="expression">An expression targeting the property (e.g., <c>x => x.MyProperty</c> or deep paths like <c>x => x.Config.Setting</c>).</param>
    /// <returns>A builder to apply strategies and decorators to the newly selected property.</returns>
    public CrdtPropertyBuilder<T, TNextProperty> Property<TNextProperty>(Expression<Func<T, TNextProperty>> expression)
    {
        return this.entityBuilder.Property(expression);
    }
}