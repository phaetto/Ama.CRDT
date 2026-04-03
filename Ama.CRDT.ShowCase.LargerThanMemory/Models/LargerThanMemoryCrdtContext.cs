namespace Ama.CRDT.ShowCase.LargerThanMemory.Models;

using System;
using System.Collections.Generic;
using Ama.CRDT.Attributes;
using Ama.CRDT.Models.Aot;

[CrdtSerializable(typeof(BlogPost))]
[CrdtSerializable(typeof(Comment))]
[CrdtSerializable(typeof(Dictionary<DateTimeOffset, Comment>))]
[CrdtSerializable(typeof(List<string>))]
[CrdtSerializable(typeof(IDictionary<DateTimeOffset, Comment>))]
[CrdtSerializable(typeof(IList<string>))]
public sealed partial class LargerThanMemoryCrdtContext : CrdtContext
{
}