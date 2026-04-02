namespace Ama.CRDT.ShowCase.CollaborativeEditing.Models;

using System.Text.Json.Serialization;
using Ama.CRDT.Models;

[JsonSerializable(typeof(SharedDocument))]
[JsonSerializable(typeof(CrdtDocument<SharedDocument>))]
[JsonSerializable(typeof(NetworkMessage))]
public sealed partial class CollaborativeEditingJsonContext : JsonSerializerContext
{
}