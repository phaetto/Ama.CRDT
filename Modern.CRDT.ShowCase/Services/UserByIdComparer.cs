using System.Text.Json;
using System.Text.Json.Nodes;
using Modern.CRDT.Services.Strategies;
using Modern.CRDT.ShowCase.Models;

namespace Modern.CRDT.ShowCase.Services;

/// <summary>
/// A custom implementation of <see cref="IJsonNodeComparer"/> that allows the <see cref="ArrayLcsStrategy"/>
/// to identify unique <see cref="User"/> objects based on their <c>Id</c> property, rather than by object reference.
/// </summary>
public sealed class UserByIdComparer : IJsonNodeComparer
{
    private static readonly string IdPropertyName = JsonNamingPolicy.CamelCase.ConvertName(nameof(User.Id));

    public bool CanCompare(Type type) => type == typeof(User);

    public bool Equals(JsonNode? x, JsonNode? y)
    {
        if (ReferenceEquals(x, y)) return true;
        if (x is null || y is null) return false;

        var xObj = x.AsObject();
        var yObj = y.AsObject();

        if (xObj.TryGetPropertyValue(IdPropertyName, out var xIdNode) &&
            yObj.TryGetPropertyValue(IdPropertyName, out var yIdNode) &&
            xIdNode is not null && yIdNode is not null)
        {
            try
            {
                return xIdNode.GetValue<Guid>() == yIdNode.GetValue<Guid>();
            }
            catch (InvalidOperationException)
            {
                // Fallback for when the Guid is stored as a string in the JsonNode
                if (Guid.TryParse(xIdNode.ToString(), out var xGuid) && Guid.TryParse(yIdNode.ToString(), out var yGuid))
                {
                    return xGuid == yGuid;
                }
            }
        }

        return false;
    }

    public int GetHashCode(JsonNode obj)
    {
        ArgumentNullException.ThrowIfNull(obj);

        if (obj.AsObject().TryGetPropertyValue(IdPropertyName, out var idNode) && idNode is not null)
        {
            try
            {
                return idNode.GetValue<Guid>().GetHashCode();
            }
            catch (InvalidOperationException)
            {
                // Fallback for when the Guid is stored as a string in the JsonNode
                if (Guid.TryParse(idNode.ToString(), out var guid))
                {
                    return guid.GetHashCode();
                }
            }
        }

        return obj.GetHashCode();
    }
}