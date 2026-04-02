namespace Ama.CRDT.ShowCase.LargerThanMemory.Models;

using System.Text.Json.Serialization;
using Ama.CRDT.Models;

[JsonSerializable(typeof(BlogPost))]
[JsonSerializable(typeof(Comment))]
[JsonSerializable(typeof(CrdtDocument<BlogPost>))]
public sealed partial class LargerThanMemoryJsonContext : JsonSerializerContext
{
}