namespace Ama.CRDT.UnitTests.Services.Strategies;

using Ama.CRDT.Attributes;
using Ama.CRDT.Extensions;
using Ama.CRDT.Models;
using Ama.CRDT.Services;
using Ama.CRDT.Services.Providers;
using Ama.CRDT.Services.Strategies;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Shouldly;
using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;

public sealed class GCounterStrategyTests : IDisposable
{
    private sealed class TestModel { [CrdtGCounterStrategy] public int Count { get; set; } }

    private readonly Mock<ICrdtPatcher> mockPatcher = new();
    private readonly List<CrdtOperation> operations = new();

    private readonly IServiceScope scopeA;
    private readonly GCounterStrategy strategy;
    private readonly ICrdtApplicator applicatorA;
    private readonly ICrdtMetadataManager metadataManagerA;
    private readonly ICrdtTimestampProvider timestampProvider;

    public GCounterStrategyTests()
    {
        var serviceProvider = new ServiceCollection()
            .AddCrdt()
            .AddSingleton<ICrdtTimestampProvider, SequentialTimestampProvider>()
            .BuildServiceProvider();

        scopeA = serviceProvider.GetRequiredService<ICrdtScopeFactory>().CreateScope("A");

        strategy = scopeA.ServiceProvider.GetRequiredService<GCounterStrategy>();
        applicatorA = scopeA.ServiceProvider.GetRequiredService<ICrdtApplicator>();
        metadataManagerA = scopeA.ServiceProvider.GetRequiredService<ICrdtMetadataManager>();
        timestampProvider = scopeA.ServiceProvider.GetRequiredService<ICrdtTimestampProvider>();
    }

    public void Dispose()
    {
        scopeA.Dispose();
    }

    [Fact]
    public void GeneratePatch_ShouldCreateIncrement_WhenValueIncreases()
    {
        // Arrange
        var property = typeof(TestModel).GetProperty(nameof(TestModel.Count))!;
        var context = new GeneratePatchContext(
            mockPatcher.Object, operations, "$.count", property, 10, 15, new TestModel { Count = 10 }, new TestModel { Count = 15 }, new CrdtMetadata(), timestampProvider.Now());

        // Act
        strategy.GeneratePatch(context);

        // Assert
        var op = operations.ShouldHaveSingleItem();
        op.Type.ShouldBe(OperationType.Increment);
        op.Value.ShouldBe(5m);
    }

    [Fact]
    public void GeneratePatch_ShouldDoNothing_WhenValueDecreases()
    {
        // Arrange
        var property = typeof(TestModel).GetProperty(nameof(TestModel.Count))!;
        var context = new GeneratePatchContext(
            mockPatcher.Object, operations, "$.count", property, 10, 5, new TestModel { Count = 10 }, new TestModel { Count = 5 }, new CrdtMetadata(), timestampProvider.Now());
        
        // Act
        strategy.GeneratePatch(context);

        // Assert
        operations.ShouldBeEmpty();
    }

    [Fact]
    public void ApplyOperation_ShouldIncrementValue_WithPositiveDelta()
    {
        // Arrange
        var model = new TestModel { Count = 10 };
        var operation = new CrdtOperation(Guid.NewGuid(), "r", "$.count", OperationType.Increment, 5m, timestampProvider.Create(2L));
        var context = new ApplyOperationContext(model, new CrdtMetadata(), operation);

        // Act
        strategy.ApplyOperation(context);

        // Assert
        model.Count.ShouldBe(15);
    }
    
    [Fact]
    public void ApplyOperation_ShouldIgnore_NegativeDelta()
    {
        // Arrange
        var model = new TestModel { Count = 10 };
        var operation = new CrdtOperation(Guid.NewGuid(), "r", "$.count", OperationType.Increment, -5m, timestampProvider.Create(2L));
        var context = new ApplyOperationContext(model, new CrdtMetadata(), operation);

        // Act
        strategy.ApplyOperation(context);

        // Assert
        model.Count.ShouldBe(10);
    }
    
    [Fact]
    public void ApplyOperation_ShouldThrow_ForNonIncrementOperation()
    {
        // Arrange
        var model = new TestModel { Count = 10 };
        var operation = new CrdtOperation(Guid.NewGuid(), "r", "$.count", OperationType.Upsert, 15, timestampProvider.Create(2L));
        var context = new ApplyOperationContext(model, new CrdtMetadata(), operation);

        // Act & Assert
        Should.Throw<InvalidOperationException>(() => strategy.ApplyOperation(context));
    }
    
    [Fact]
    public void ApplyPatch_IsIdempotent()
    {
        // Arrange
        var model = new TestModel { Count = 10 };
        var meta = metadataManagerA.Initialize(model);
        var document = new CrdtDocument<TestModel>(model, meta);
        var patch = new CrdtPatch(new List<CrdtOperation>
        {
            new(Guid.NewGuid(), "r1", "$.Count", OperationType.Increment, 5m, timestampProvider.Create(1L))
        });

        // Act
        applicatorA.ApplyPatch(document, patch);
        var countAfterFirst = model.Count;
        applicatorA.ApplyPatch(document, patch);

        // Assert
        model.Count.ShouldBe(countAfterFirst);
        model.Count.ShouldBe(15);
    }

    [Fact]
    public void ApplyPatch_IsCommutativeAndAssociative()
    {
        // Arrange
        var patch1 = new CrdtPatch(new List<CrdtOperation> { new(Guid.NewGuid(), "r1", "$.Count", OperationType.Increment, 10m, timestampProvider.Create(1L)) });
        var patch2 = new CrdtPatch(new List<CrdtOperation> { new(Guid.NewGuid(), "r2", "$.Count", OperationType.Increment, 5m, timestampProvider.Create(2L)) });
        var patch3 = new CrdtPatch(new List<CrdtOperation> { new(Guid.NewGuid(), "r3", "$.Count", OperationType.Increment, 20m, timestampProvider.Create(3L)) });

        var patches = new[] { patch1, patch2, patch3 };
        var permutations = GetPermutations(patches, 3);
        var finalCounts = new List<int>();

        // Act
        foreach (var p in permutations)
        {
            var model = new TestModel { Count = 10 };
            var meta = metadataManagerA.Initialize(model);
            var document = new CrdtDocument<TestModel>(model, meta);
            foreach (var patch in p)
            {
                applicatorA.ApplyPatch(document, patch);
            }
            finalCounts.Add(model.Count);
        }

        // Assert
        // Expected: 10 + 10 + 5 + 20 = 45
        finalCounts.ShouldAllBe(s => s == 45);
    }
    
    private IEnumerable<IEnumerable<T>> GetPermutations<T>(IEnumerable<T> list, int length)
    {
        if (length == 1) return list.Select(t => new T[] { t });
        var enumerable = list as T[] ?? list.ToArray();
        return GetPermutations(enumerable, length - 1)
            .SelectMany(t => enumerable.Where(e => !t.Contains(e)),
                (t1, t2) => t1.Concat(new T[] { t2 }));
    }
}