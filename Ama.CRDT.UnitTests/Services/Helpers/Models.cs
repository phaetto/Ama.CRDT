namespace Ama.CRDT.UnitTests.Services.Helpers;

using System;
using System.Collections.Generic;

public enum TestStatus
{
    Pending,
    Active,
    Closed
}

public sealed class TestAddress
{
    public string? Street { get; set; }
}

public sealed class TestUser
{
    public string? Name { get; set; }
    public int Age { get; set; }
    public TestAddress? Address { get; set; }
}

public sealed class TestRootWithReadOnly
{
    public string? ReadOnlyProp { get; }

    public TestRootWithReadOnly(string? readOnlyProp)
    {
        ReadOnlyProp = readOnlyProp;
    }
}

public sealed class TestNested
{
    public string Name { get; set; } = string.Empty;
    public int Value { get; set; }
}

public sealed class TestRoot
{
    public string TopLevelProperty { get; set; } = string.Empty;
    public TestNested NestedObject { get; set; } = new();
    public List<string> SimpleList { get; set; } = [];
    public List<TestNested> ComplexList { get; set; } = [];
    public int[] SimpleArray { get; set; } = [];
    public TestNested[] ComplexArray { get; set; } = [];

    // Properties used for extensive path resolution and type conversion tests
    public Guid Id { get; set; }
    public TestStatus Status { get; set; }
    public string? SimpleProp { get; set; }
    public TestUser? User { get; set; }
    public List<TestUser>? Users { get; set; }
    public List<string>? Tags { get; set; }
    public ISet<string>? UniqueTags { get; set; }
    public string? SpecialNameProp { get; set; }
    public int[]? Scores { get; set; }
    public IDictionary<string, string>? Settings { get; set; }
    public Dictionary<string, TestUser>? UsersMap { get; set; }
}