namespace Ama.CRDT.ShowCase.LargerThanMemory.Services;

using System;
using Ama.CRDT.Services.Providers;
using Ama.CRDT.ShowCase.LargerThanMemory.Models;

public sealed class BlogPostDocumentIdProvider : IDocumentIdProvider
{
    public IComparable GetDocumentId<T>(T? obj)
    {
        ArgumentNullException.ThrowIfNull(obj);

        if (obj is BlogPost blogPost)
        {
            return blogPost.Id;
        }

        throw new NotSupportedException($"Type {typeof(T).Name} is not supported by {nameof(BlogPostDocumentIdProvider)}.");
    }

    public void SetDocumentId<T>(T obj, IComparable id)
    {
        ArgumentNullException.ThrowIfNull(obj);
        ArgumentNullException.ThrowIfNull(id);

        if (obj is BlogPost blogPost)
        {
            blogPost.Id = id switch
            {
                Guid g => g,
                string s => Guid.Parse(s),
                _ => throw new ArgumentException($"ID of type {id.GetType().Name} is not supported.", nameof(id))
            };
            return;
        }

        throw new NotSupportedException($"Type {typeof(T).Name} is not supported by {nameof(BlogPostDocumentIdProvider)}.");
    }

    public T CreateDocumentWithId<T>(IComparable id)
    {
        ArgumentNullException.ThrowIfNull(id);

        if (typeof(T) == typeof(BlogPost))
        {
            var guid = id switch
            {
                Guid g => g,
                string s => Guid.Parse(s),
                _ => throw new ArgumentException($"ID of type {id.GetType().Name} is not supported.", nameof(id))
            };
            return (T)(object)new BlogPost { Id = guid };
        }

        throw new NotSupportedException($"Type {typeof(T).Name} is not supported by {nameof(BlogPostDocumentIdProvider)}.");
    }
}