namespace Ama.CRDT.ShowCase.LargerThanMemory.Models;

using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;
using Ama.CRDT.Models;
using Ama.CRDT.ShowCase.LargerThanMemory.Services;

[JsonSerializable(typeof(BlogPost))]
[JsonSerializable(typeof(Comment))]
[JsonSerializable(typeof(CrdtDocument<BlogPost>))]
[JsonSerializable(typeof(Dictionary<DateTimeOffset, Comment>))]
[JsonSerializable(typeof(List<string>))]
[JsonSerializable(typeof(List<JournaledOperation>))]
[JsonSerializable(typeof(Dictionary<string, UiService.DvvStateDto>))]
public sealed partial class LargerThanMemoryJsonContext : JsonSerializerContext
{
}