namespace Ama.CRDT.Services.Strategies;

using System.Collections.Generic;
using System.Reflection;
using Ama.CRDT.Models;

/// <summary>
/// Defines the context for a <see cref="ICrdtStrategy.GeneratePatch"/> call, encapsulating all necessary parameters for generating CRDT operations for a single property.
/// </summary>
/// <param name="Operations">The list of <see cref="CrdtOperation"/> to which the strategy should add any generated operations.</param>
/// <param name="NestedDiffs">A collection to which the strategy can append <see cref="DifferentiateObjectContext"/>s for properties that need to be recursively evaluated by the patcher.</param>
/// <param name="Path">The JSON path to the property being compared (e.g., "$.user.name").</param>
/// <param name="Property">The <see cref="PropertyInfo"/> of the property being compared.</param>
/// <param name="OriginalValue">The value of the property in the original document.</param>
/// <param name="ModifiedValue">The value of the property in the modified document.</param>
/// <param name="OriginalRoot">The root object of the original document.</param>
/// <param name="ModifiedRoot">The root object of the modified document.</param>
/// <param name="OriginalMeta">The CRDT metadata associated with the original document.</param>
/// <param name="ChangeTimestamp">The timestamp to be assigned to any new CRDT operations.</param>
/// <param name="Clock">The monotonically increasing causal sequence number for the originating replica. All operations generated within a single atomic patch share this same clock value.</param>
public sealed record GeneratePatchContext(
    List<CrdtOperation> Operations,
    List<DifferentiateObjectContext> NestedDiffs,
    string Path,
    PropertyInfo Property,
    object? OriginalValue,
    object? ModifiedValue,
    object? OriginalRoot,
    object? ModifiedRoot,
    CrdtMetadata OriginalMeta,
    ICrdtTimestamp ChangeTimestamp,
    long Clock
);