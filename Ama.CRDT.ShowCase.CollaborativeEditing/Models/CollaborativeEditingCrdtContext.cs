namespace Ama.CRDT.ShowCase.CollaborativeEditing.Models;

using Ama.CRDT.Attributes;
using Ama.CRDT.Models.Aot;

/// <summary>
/// AOT reflection context for the showcase models.
/// </summary>
[CrdtAotType(typeof(SharedDocument))]
[CrdtAotType(typeof(IList<string>))]
[CrdtAotType(typeof(List<string>))]
internal partial class CollaborativeEditingCrdtAotContext : CrdtAotContext
{
}