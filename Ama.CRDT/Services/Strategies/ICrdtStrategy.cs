namespace Ama.CRDT.Services.Strategies;

using Ama.CRDT.Models;
using System;
using System.Collections.Generic;

/// <summary>
/// Defines the contract for a strategy that handles a specific type of CRDT data.
/// Each strategy is responsible for generating property-specific patch operations and applying them to a document.
/// Strategies are stateless and should rely on the document's metadata for any stateful logic.
/// </summary>
public interface ICrdtStrategy
{
    /// <summary>
    /// Compares a property's value between two document states and generates a list of CRDT operations.
    /// This method is called by the <see cref="ICrdtPatcher"/> during patch generation.
    /// </summary>
    /// <param name="context">The context for the patch generation operation, containing the original and modified values, metadata, and other relevant information.</param>
    void GeneratePatch(GeneratePatchContext context);

    /// <summary>
    /// Explicitly generates a single CRDT operation based on a specified user intent, bypassing state-based diffing.
    /// By default, throws <see cref="NotSupportedException"/>. Strategies that support explicit commands should override this.
    /// </summary>
    /// <param name="context">The context for the intent generation, containing metadata, paths, and the requested intent.</param>
    /// <returns>An explicitly generated <see cref="CrdtOperation"/>.</returns>
    CrdtOperation GenerateOperation(GenerateOperationContext context)
    {
        throw new NotSupportedException($"Explicit operation generation is not supported for {this.GetType().Name}.");
    }

    /// <summary>
    /// Applies a single CRDT operation to a POCO document.
    /// This method performs the direct data manipulation on the target object and can also modify the associated CRDT metadata.
    /// </summary>
    /// <param name="context">The context for the apply operation, containing the target document, metadata, and the operation to apply.</param>
    void ApplyOperation(ApplyOperationContext context);
}