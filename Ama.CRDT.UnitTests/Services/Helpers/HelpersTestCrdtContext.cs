namespace Ama.CRDT.UnitTests.Services.Helpers;

using System.Collections.Generic;
using Ama.CRDT.Attributes;
using Ama.CRDT.Models.Aot;

/// <summary>
/// A dedicated CrdtAotContext for the PocoPathHelper unit tests to provide AOT-compatible 
/// property metadata for test-specific models and collections.
/// </summary>
[CrdtAotType(typeof(TestRoot))]
[CrdtAotType(typeof(TestNested))]
[CrdtAotType(typeof(TestUser))]
[CrdtAotType(typeof(TestAddress))]
[CrdtAotType(typeof(TestRootWithReadOnly))]
[CrdtAotType(typeof(Dictionary<string, string>))]
[CrdtAotType(typeof(Dictionary<string, TestUser>))]
[CrdtAotType(typeof(List<TestUser>))]
[CrdtAotType(typeof(List<string>))]
[CrdtAotType(typeof(List<int>))]
[CrdtAotType(typeof(List<TestNested>))]
[CrdtAotType(typeof(HashSet<string>))]
[CrdtAotType(typeof(int[]))]
[CrdtAotType(typeof(TestNested[]))]
[CrdtAotType(typeof(KeyValuePair<string, int>))]
[CrdtAotType(typeof(Dictionary<string, object>))]
[CrdtAotType(typeof(IEnumerable<string>))]
[CrdtAotType(typeof(ISet<string>))]
[CrdtAotType(typeof(IDictionary<string, string>))]
[CrdtAotType(typeof(IDictionary<string, TestUser>))]
[CrdtAotType(typeof(IDictionary<string, object>))]
[CrdtAotType(typeof(object))]
internal partial class HelpersTestCrdtAotContext : CrdtAotContext
{
}