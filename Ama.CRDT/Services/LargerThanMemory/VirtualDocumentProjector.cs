namespace Ama.CRDT.Services.LargerThanMemory;

using Ama.CRDT.Models.LargerThanMemory;
using System;
using System.Threading;
using System.Threading.Tasks;

/// <summary>
/// A base implementation of <see cref="IVirtualDocumentProjector{T}"/> that provides empty virtual methods,
/// allowing you to only override the projection hooks you need for your Read Model.
/// </summary>
/// <typeparam name="T">The type of the data model managed by the CRDT.</typeparam>
public abstract class VirtualDocumentProjector<T> : IVirtualDocumentProjector<T> where T : class, new()
{
    /// <inheritdoc/>
    public virtual Task ProjectHeaderAsync(IComparable logicalKey, T header, CancellationToken cancellationToken = default) => Task.CompletedTask;

    /// <inheritdoc/>
    public virtual Task ProjectItemUpsertAsync(IComparable logicalKey, string propertyName, IComparable itemKey, object item, CancellationToken cancellationToken = default) => Task.CompletedTask;

    /// <inheritdoc/>
    public virtual Task ProjectItemDeleteAsync(IComparable logicalKey, string propertyName, IComparable itemKey, CancellationToken cancellationToken = default) => Task.CompletedTask;

    /// <inheritdoc/>
    public virtual Task ProjectChunkAsync(IComparable logicalKey, string propertyName, IChunk chunk, T chunkData, CancellationToken cancellationToken = default) => Task.CompletedTask;
}