namespace Ama.CRDT.ShowCase.Models;

using Ama.CRDT.Attributes;
using Ama.CRDT.Models.Aot;

/// <summary>
/// AOT reflection context for the showcase models to be used by the internal reflection-free routines.
/// </summary>
[CrdtAotType(typeof(UserStats))]
[CrdtAotType(typeof(List<string>))]
public partial class ShowcaseCrdtAotContext : CrdtAotContext
{
}