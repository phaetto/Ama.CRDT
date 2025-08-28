namespace Ama.CRDT.Services.Strategies;
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
    /// Applies a single CRDT operation to a POCO document.
    /// This method performs the direct data manipulation on the target object and can also modify the associated CRDT metadata.
    /// </summary>
    /// <param name="context">The context for the apply operation, containing the target document, metadata, and the operation to apply.</param>
    void ApplyOperation(ApplyOperationContext context);
}