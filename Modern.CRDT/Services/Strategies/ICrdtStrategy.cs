namespace Modern.CRDT.Services.Strategies;

using System.Collections.Generic;
using System.Reflection;
using System.Text.Json.Nodes;
using Modern.CRDT.Models;

/// <summary>
/// Defines the contract for a strategy that handles CRDT patch generation and application for a property.
/// Each strategy is responsible for a specific type of data (e.g., LWW for simple values, Counter for numbers).
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
    /// <param name="originalMetadata">The metadata associated with the original value.</param>
    /// <param name="modifiedMetadata">The metadata associated with the modified value.</param>
    void GeneratePatch(IJsonCrdtPatcher patcher, List<CrdtOperation> operations, string path, PropertyInfo property, JsonNode? originalValue, JsonNode? modifiedValue, JsonNode? originalMetadata, JsonNode? modifiedMetadata);
    
    /// <summary>
    /// Applies a single CRDT operation to a JSON document, modifying it in place.
    /// This method should only perform the data manipulation logic without any state or conflict checks.
    /// </summary>
    /// <param name="rootNode">The root JsonNode of the document to modify.</param>
    /// <param name="operation">The CRDT operation to apply.</param>
    void ApplyOperation(JsonNode rootNode, CrdtOperation operation);
}