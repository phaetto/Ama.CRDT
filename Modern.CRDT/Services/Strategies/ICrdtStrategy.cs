using System.Text.Json.Nodes;
using Modern.CRDT.Models;

namespace Modern.CRDT.Services.Strategies;

/// <summary>
/// Defines the contract for a CRDT strategy, which encapsulates the logic for both
/// generating patches and applying operations for a specific property or data type.
/// </summary>
public interface ICrdtStrategy
{
    /// <summary>
    /// Compares the original and modified states of a property and generates a sequence of CRDT operations.
    /// </summary>
    /// <param name="path">The JSON path to the property being compared.</param>
    /// <param name="originalValue">The original JSON value of the property.</param>
    /// <param name="modifiedValue">The modified JSON value of the property.</param>
    /// <param name="originalMetadata">The metadata associated with the original document.</param>
    /// <param name="modifiedMetadata">The metadata associated with the modified document, which may contain updated timestamps.</param>
    /// <returns>An enumerable collection of <see cref="CrdtOperation"/> representing the changes.</returns>
    IEnumerable<CrdtOperation> GeneratePatch(string path, JsonNode? originalValue, JsonNode? modifiedValue, JsonNode? originalMetadata, JsonNode? modifiedMetadata);

    /// <summary>
    /// Applies a single CRDT operation to a target document based on the strategy's rules.
    /// </summary>
    /// <param name="rootNode">The root JsonNode of the document to which the operation will be applied.</param>
    /// <param name="metadataNode">The root JsonNode of the metadata document, used for conflict resolution (e.g., checking timestamps).</param>
    /// <param name="operation">The CRDT operation to apply.</param>
    void ApplyOperation(JsonNode rootNode, JsonNode metadataNode, CrdtOperation operation);
}