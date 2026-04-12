namespace Ama.CRDT.Services.Providers;

using System;
using System.Collections.Generic;
using Ama.CRDT.Models.Aot;
using Ama.CRDT.Services.Helpers;

/// <inheritdoc/>
internal sealed class DefaultDocumentIdProvider : IDocumentIdProvider
{
    private readonly IEnumerable<CrdtAotContext> aotContexts;

    public DefaultDocumentIdProvider(IEnumerable<CrdtAotContext> aotContexts)
    {
        this.aotContexts = aotContexts ?? throw new ArgumentNullException(nameof(aotContexts));
    }

    /// <inheritdoc/>
    public string GetDocumentId<T>(T? obj)
    {
        ArgumentNullException.ThrowIfNull(obj);

        var type = obj.GetType();
        var typeInfo = PocoPathHelper.GetTypeInfo(type, aotContexts);

        if (!typeInfo.Properties.TryGetValue("Id", out var prop) || !prop.CanRead)
        {
            throw new InvalidOperationException($"Cannot extract document ID. Type '{type.Name}' does not have a readable 'Id' property. Please provide a custom IDocumentIdProvider or ensure your document model has an 'Id' property.");
        }

        var val = prop.Getter?.Invoke(obj);
        if (val is null)
        {
            throw new InvalidOperationException($"The 'Id' property on type '{type.Name}' evaluated to null. Document IDs cannot be null.");
        }

        var stringVal = val.ToString();
        if (string.IsNullOrWhiteSpace(stringVal))
        {
            throw new InvalidOperationException($"The 'Id' property on type '{type.Name}' evaluated to an empty or whitespace string.");
        }

        return stringVal;
    }
}