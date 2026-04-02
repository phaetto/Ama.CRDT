namespace Ama.CRDT.UnitTests.Services.Helpers;

using System.Collections.Generic;
using Ama.CRDT.Attributes;
using Ama.CRDT.Models.Aot;

/// <summary>
/// A dedicated CrdtContext for the PocoPathHelper unit tests to provide AOT-compatible 
/// property metadata for test-specific models and collections.
/// </summary>
[CrdtSerializable(typeof(TestRoot))]
[CrdtSerializable(typeof(TestNested))]
[CrdtSerializable(typeof(TestUser))]
[CrdtSerializable(typeof(TestAddress))]
[CrdtSerializable(typeof(TestRootWithReadOnly))]
[CrdtSerializable(typeof(Dictionary<string, string>))]
[CrdtSerializable(typeof(Dictionary<string, TestUser>))]
[CrdtSerializable(typeof(List<TestUser>))]
[CrdtSerializable(typeof(List<string>))]
[CrdtSerializable(typeof(List<TestNested>))]
[CrdtSerializable(typeof(HashSet<string>))]
[CrdtSerializable(typeof(int[]))]
[CrdtSerializable(typeof(TestNested[]))]
[CrdtSerializable(typeof(KeyValuePair<string, int>))]
internal partial class HelpersTestCrdtContext : CrdtContext
{
}