namespace Ama.CRDT.UnitTests.Services.Helpers;

using System.Collections.Generic;

public sealed class TestRoot
{
    public string TopLevelProperty { get; set; } = string.Empty;
    public TestNested NestedObject { get; set; } = new();
    public List<string> SimpleList { get; set; } = [];
    public List<TestNested> ComplexList { get; set; } = [];
    public int[] SimpleArray { get; set; } = [];
    public TestNested[] ComplexArray { get; set; } = [];
}

public sealed class TestNested
{
    public string Name { get; set; } = string.Empty;
    public int Value { get; set; }
}