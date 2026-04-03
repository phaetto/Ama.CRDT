namespace Ama.CRDT.ShowCase.LargerThanMemory.Models;

using System;
using System.Collections.Generic;
using Ama.CRDT.Attributes;
using Ama.CRDT.Models.Aot;

[CrdtAotType(typeof(BlogPost))]
[CrdtAotType(typeof(Comment))]
[CrdtAotType(typeof(Dictionary<DateTimeOffset, Comment>))]
[CrdtAotType(typeof(List<string>))]
[CrdtAotType(typeof(IDictionary<DateTimeOffset, Comment>))]
[CrdtAotType(typeof(IList<string>))]
public sealed partial class LargerThanMemoryCrdtAotContext : CrdtAotContext
{
}