namespace Ama.CRDT.Services.LargerThanMemory;

using System.Threading;
using System.Threading.Tasks;

/// <summary>
/// Defines common operations for managing virtualized CRDT documents regardless of their underlying storage strategy.
/// Combines read-only queries with mutative administrative commands.
/// </summary>
/// <typeparam name="T">The type of the data model managed by the CRDT.</typeparam>
public interface IVirtualDocumentManager<T> : IVirtualDocumentCollectionReader<T> where T : class, new()
{
    /// <summary>
    /// Initializes a new virtualized CRDT document.
    /// </summary>
    /// <param name="initialObject">The initial object to populate the document with. The logical key will be extracted from this object.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task that represents the asynchronous initialization operation.</returns>
    Task InitializeAsync(T initialObject, CancellationToken cancellationToken = default);

    /// <summary>
    /// Explicitly triggers a garbage collection compaction pass across the document and its virtual collections.
    /// </summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    Task CompactAsync(CancellationToken cancellationToken = default);
}