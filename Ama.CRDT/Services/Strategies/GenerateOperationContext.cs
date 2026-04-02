namespace Ama.CRDT.Services.Strategies;

using Ama.CRDT.Models;
using Ama.CRDT.Models.Aot;
using Ama.CRDT.Models.Intents;

/// <summary>
/// Defines the context object for explicitly generating operations based on user intent.
/// </summary>
/// <param name="DocumentRoot">The root data document.</param>
/// <param name="Metadata">The CRDT metadata state associated with the document.</param>
/// <param name="JsonPath">The JSON path leading to the target property.</param>
/// <param name="Property">The AOT-compatible property information representing the target.</param>
/// <param name="Intent">The user intent specifying the change to perform (e.g., insert, remove).</param>
/// <param name="Timestamp">The timestamp assigned to the generated operation.</param>
/// <param name="Clock">The monotonically increasing causal sequence number for the originating replica.</param>
public readonly record struct GenerateOperationContext(
    object DocumentRoot,
    CrdtMetadata Metadata,
    string JsonPath,
    CrdtPropertyInfo Property,
    IOperationIntent Intent,
    ICrdtTimestamp Timestamp,
    long Clock);