namespace Ama.CRDT.Services.Strategies;

using System.Collections.Generic;
using System.Reflection;
using Ama.CRDT.Models;

/// <summary>
/// Defines the contract for a strategy that handles a specific type of CRDT data.
/// Each strategy is responsible for generating property-specific patch operations and applying them.
/// </summary>
public interface ICrdtStrategy
{
    /// <summary>
    /// Compares a property's value between two document states and generates a list of CRDT operations.
    /// </summary>
    /// <param name="patcher">The orchestrating patcher service, which can be used for recursive diffing.</param>
    /// <param name="operations">The list of operations to which new operations should be added.</param>
    /// <param name="path">The JSON Path to the property being compared.</param>
    /// <param name="property">The reflection info for the property.</param>
    /// <param name="originalValue">The value of the property in the original document.</param>
    /// <param name="modifiedValue">The value of the property in the modified document.</param>
    /// <param name="originalMeta">The metadata associated with the original document.</param>
    /// <param name="modifiedMeta">The metadata associated with the modified document.</param>
    void GeneratePatch(ICrdtPatcher patcher, List<CrdtOperation> operations, string path, PropertyInfo property, object? originalValue, object? modifiedValue, CrdtMetadata originalMeta, CrdtMetadata modifiedMeta);
    
    /// <summary>
    /// Applies a single CRDT operation to a POCO document.
    /// This method performs the direct data manipulation and can modify document metadata.
    /// Conflict resolution checks (like LWW timestamp comparison or seen operation checks) are expected to be performed by the applicator before this method is called.
    /// </summary>
    /// <param name="root">The root POCO of the document to be modified.</param>
    /// <param name="metadata">The metadata associated with the document, which the strategy can read from or write to.</param>
    /// <param name="operation">The CRDT operation to apply.</param>
    void ApplyOperation(object root, CrdtMetadata metadata, CrdtOperation operation);
}