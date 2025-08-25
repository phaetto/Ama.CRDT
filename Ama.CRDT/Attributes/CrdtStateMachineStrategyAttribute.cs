namespace Ama.CRDT.Attributes;

using Ama.CRDT.Services.Strategies;
using System;
using System.Linq;

/// <summary>
/// Marks a property to be managed by the State Machine strategy.
/// This strategy enforces valid state transitions based on a provided validator.
/// Conflicts are resolved using a Last-Writer-Wins (LWW) approach for valid transitions.
/// </summary>
[AttributeUsage(AttributeTargets.Property, Inherited = true, AllowMultiple = false)]
public sealed class CrdtStateMachineStrategyAttribute : CrdtStrategyAttribute
{
    /// <summary>
    /// Gets the type of the validator that implements <see cref="IStateMachine{TState}"/>,
    /// which defines the valid state transitions. This type must be registered in the dependency injection container.
    /// </summary>
    public Type ValidatorType { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="CrdtStateMachineStrategyAttribute"/> class.
    /// </summary>
    /// <param name="validatorType">The type of the validator that implements <see cref="IStateMachine{TState}"/>.</param>
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="validatorType"/> is null.</exception>
    /// <exception cref="ArgumentException">Thrown if <paramref name="validatorType"/> does not implement the <see cref="IStateMachine{TState}"/> interface.</exception>
    public CrdtStateMachineStrategyAttribute(Type validatorType) : base(typeof(StateMachineStrategy))
    {
        ArgumentNullException.ThrowIfNull(validatorType);

        if (!validatorType.GetInterfaces().Any(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IStateMachine<>)))
        {
            throw new ArgumentException($"The provided type must implement the IStateMachine<T> interface.", nameof(validatorType));
        }
        ValidatorType = validatorType;
    }
}