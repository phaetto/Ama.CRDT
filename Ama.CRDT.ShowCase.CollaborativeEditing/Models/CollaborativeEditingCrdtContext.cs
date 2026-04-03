namespace Ama.CRDT.ShowCase.CollaborativeEditing.Models;

using Ama.CRDT.Attributes;
using Ama.CRDT.Models.Aot;

/// <summary>
/// AOT reflection context for the showcase models.
/// </summary>
[CrdtSerializable(typeof(SharedDocument))]
[CrdtSerializable(typeof(IList<string>))]
[CrdtSerializable(typeof(List<string>))]
internal partial class CollaborativeEditingCrdtContext : CrdtContext
{
}