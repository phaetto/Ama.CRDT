namespace Ama.CRDT.Attributes;

using Ama.CRDT.Services.Strategies;
using System;

/// <summary>
/// A decorator attribute that wraps another CRDT strategy with an "Epoch" or "Generation" counter.
/// This strategy intercepts operations, and upon a <see cref="Models.Intents.ClearIntent"/>, increments the epoch.
/// Operations belonging to older epochs are safely discarded, solving issues like "ghost items" in shopping carts.
/// </summary>
[AttributeUsage(AttributeTargets.Property, Inherited = true, AllowMultiple = false)]
public sealed class CrdtEpochBoundAttribute : CrdtStrategyDecoratorAttribute
{
    /// <summary>
    /// Initializes a new instance of the <see cref="CrdtEpochBoundAttribute"/> class.
    /// </summary>
    public CrdtEpochBoundAttribute() : base(typeof(EpochBoundStrategy))
    {
    }
}