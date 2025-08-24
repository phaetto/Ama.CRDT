namespace Ama.CRDT.Services.Strategies;

using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
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
    /// This method is called by the <see cref="ICrdtPatcher"/> during patch generation.
    /// </summary>
    /// <param name="patcher">The orchestrating patcher service, which can be used for recursive diffing of nested objects.</param>
    /// <param name="operations">The list of operations to which new operations should be added.</param>
    /// <param name="path">The JSON Path to the property being compared.</param>
    /// <param name="property">The reflection info for the property.</param>
    /// <param name="originalValue">The value of the property in the original document.</param>
    /// <param name="modifiedValue">The value of the property in the modified document.</param>
    /// <param name="originalMeta">The metadata associated with the original document.</param>
    /// <param name="modifiedMeta">The metadata associated with the modified document.</param>
    void GeneratePatch([DisallowNull] ICrdtPatcher patcher, [DisallowNull] List<CrdtOperation> operations, [DisallowNull] string path, [DisallowNull] PropertyInfo property, object? originalValue, object? modifiedValue, [DisallowNull] CrdtMetadata originalMeta, [DisallowNull] CrdtMetadata modifiedMeta);
    
    /// <summary>
    /// Applies a single CRDT operation to a POCO document.
    /// This method performs the direct data manipulation and can modify document metadata.
    /// </summary>
    /// <param name="root">The root POCO of the document to be modified.</param>
    /// <param name="metadata">The metadata associated with the document, which the strategy can read from or write to.</param>
    /// <param name="operation">The CRDT operation to apply.</param>
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="root"/> or <paramref name="metadata"/> is null.</exception>
    void ApplyOperation([DisallowNull] object root, [DisallowNull] CrdtMetadata metadata, CrdtOperation operation);
}