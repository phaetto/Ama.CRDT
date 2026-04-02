namespace Ama.CRDT.UnitTests.Services;

using System.Collections.Generic;
using Ama.CRDT.Attributes;
using Ama.CRDT.Models.Aot;

/// <summary>
/// A dedicated CrdtContext for the Services unit tests to provide AOT-compatible 
/// property metadata for test-specific models and collections.
/// </summary>
[CrdtSerializable(typeof(CrdtApplicatorTests.TestModel))]
[CrdtSerializable(typeof(CrdtComposableArchitectureTests.TestRoot))]
[CrdtSerializable(typeof(CrdtComposableArchitectureTests.TestLevel1))]
[CrdtSerializable(typeof(CrdtComposableArchitectureTests.TestLevel2))]
[CrdtSerializable(typeof(CrdtComposableArchitectureTests.TestTag))]
[CrdtSerializable(typeof(CrdtComposableArchitectureTests.ComplexDocument))]
[CrdtSerializable(typeof(CrdtComposableArchitectureTests.NestedConfig))]
[CrdtSerializable(typeof(CrdtComposableArchitectureTests.DecoratedDocument))]
[CrdtSerializable(typeof(CrdtComposableArchitectureTests.ComplexCollectionDocument))]
[CrdtSerializable(typeof(CrdtComposableArchitectureTests.ComplexItem))]
[CrdtSerializable(typeof(CrdtPatcherTests.TestModel))]
[CrdtSerializable(typeof(CrdtPatcherTests.NestedModel))]
[CrdtSerializable(typeof(CrdtMetadataManagerTests.OuterDoc))]
[CrdtSerializable(typeof(CrdtMetadataManagerTests.InnerDoc))]
[CrdtSerializable(typeof(List<CrdtComposableArchitectureTests.TestTag>))]
[CrdtSerializable(typeof(List<string>))]
[CrdtSerializable(typeof(List<CrdtComposableArchitectureTests.ComplexItem>))]
[CrdtSerializable(typeof(List<CrdtMetadataManagerTests.InnerDoc>))]
[CrdtSerializable(typeof(Dictionary<string, int>))]
[CrdtSerializable(typeof(Dictionary<string, CrdtComposableArchitectureTests.ComplexItem>))]
[CrdtSerializable(typeof(Dictionary<string, CrdtMetadataManagerTests.InnerDoc>))]
[CrdtSerializable(typeof(object))]
internal partial class ServicesTestCrdtContext : CrdtContext
{
}