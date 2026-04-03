namespace Ama.CRDT.Attributes;

using System;

#pragma warning disable CS0419 // Ambiguous reference in cref attribute
/// <summary>
/// Specifies that a CRDT strategy supports a specific explicit operation intent.
/// Used by Roslyn analyzers to validate <see cref="Services.ICrdtPatcher.GenerateOperation"/> calls at compile time.
/// </summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = true, Inherited = true)]
#pragma warning restore CS0419 // Ambiguous reference in cref attribute
public sealed class CrdtSupportedIntentAttribute : Attribute
{
    /// <summary>
    /// Gets the type of the supported intent.
    /// </summary>
    public Type IntentType { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="CrdtSupportedIntentAttribute"/> class.
    /// </summary>
    /// <param name="intentType">The type of the intent (must implement <see cref="Models.Intents.IOperationIntent"/>).</param>
    public CrdtSupportedIntentAttribute(Type intentType)
    {
        ArgumentNullException.ThrowIfNull(intentType);
        IntentType = intentType;
    }
}