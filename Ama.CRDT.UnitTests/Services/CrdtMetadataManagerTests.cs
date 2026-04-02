namespace Ama.CRDT.UnitTests.Services;

using Ama.CRDT.Models;
using Ama.CRDT.Models.Aot;
using Ama.CRDT.Services;
using Ama.CRDT.Services.GarbageCollection;
using Ama.CRDT.Services.Providers;
using Ama.CRDT.Services.Strategies;
using Moq;
using Shouldly;
using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;

public sealed class CrdtMetadataManagerTests
{
    private readonly CrdtMetadataManager manager;
    private readonly Mock<ICrdtStrategyProvider> strategyProviderMock;
    private readonly Mock<ICrdtTimestampProvider> timestampProviderMock;
    private readonly Mock<IElementComparerProvider> elementComparerProviderMock;

    public CrdtMetadataManagerTests()
    {
        strategyProviderMock = new Mock<ICrdtStrategyProvider>();
        timestampProviderMock = new Mock<ICrdtTimestampProvider>();
        elementComparerProviderMock = new Mock<IElementComparerProvider>();
        manager = new CrdtMetadataManager(
            strategyProviderMock.Object, 
            timestampProviderMock.Object, 
            elementComparerProviderMock.Object,
            new ReplicaContext { ReplicaId = "replica" },
            [new ServicesTestCrdtContext()]);
            
        timestampProviderMock.Setup(p => p.Create(It.IsAny<long>())).Returns<long>(v => new EpochTimestamp(v));
    }
    
    [Theory]
    [InlineData(true, false, false, false, false)]
    [InlineData(false, true, false, false, false)]
    [InlineData(false, false, true, false, false)]
    [InlineData(false, false, false, true, false)]
    [InlineData(false, false, false, false, true)]
    public void PublicMethods_WithNullArguments_ShouldThrowArgumentNullException(bool testInitialize, bool testReset, bool testCompact, bool testAdvanceVector, bool testLocking)
    {
        // Arrange
        var doc = new object();
        var metadata = new CrdtMetadata();
        var timestamp = timestampProviderMock.Object.Create(1);
        var operation = new CrdtOperation();
        var policy = Mock.Of<ICompactionPolicy>();
        
        // Act & Assert
        if (testInitialize)
        {
            // Test Initialize<T>(T document) and Initialize<T>(T document, ICrdtTimestamp timestamp)
            Should.Throw<ArgumentNullException>(() => manager.Initialize<object>(null!));
            Should.Throw<ArgumentNullException>(() => manager.Initialize<object>(null!, timestamp));
            Should.Throw<ArgumentNullException>(() => manager.Initialize(doc, null!));
            
            // Test Initialize<T>(CrdtDocument<T> document) and Initialize<T>(CrdtDocument<T> document, ICrdtTimestamp timestamp)
            Should.Throw<ArgumentNullException>(() => manager.Initialize(new CrdtDocument<object>(null!, metadata)));
            Should.Throw<ArgumentNullException>(() => manager.Initialize(new CrdtDocument<object>(doc, null!)));
            Should.Throw<ArgumentNullException>(() => manager.Initialize(new CrdtDocument<object>(null!, metadata), timestamp));
            Should.Throw<ArgumentNullException>(() => manager.Initialize(new CrdtDocument<object>(doc, null!), timestamp));
            Should.Throw<ArgumentNullException>(() => manager.Initialize(new CrdtDocument<object>(doc, metadata), null!));
        }
        
        if (testReset)
        {
            // Test Reset<T>(CrdtDocument<T> document) and Reset<T>(CrdtDocument<T> document, ICrdtTimestamp timestamp)
            Should.Throw<ArgumentNullException>(() => manager.Reset(new CrdtDocument<object>(null!, metadata)));
            Should.Throw<ArgumentNullException>(() => manager.Reset(new CrdtDocument<object>(doc, null!)));
            Should.Throw<ArgumentNullException>(() => manager.Reset(new CrdtDocument<object>(null!, metadata), timestamp));
            Should.Throw<ArgumentNullException>(() => manager.Reset(new CrdtDocument<object>(doc, null!), timestamp));
            Should.Throw<ArgumentNullException>(() => manager.Reset(new CrdtDocument<object>(doc, metadata), null!));
        }
        
        if (testCompact)
        {
            // Test Compact<T>(CrdtDocument<T> document, ICompactionPolicy policy)
            Should.Throw<ArgumentNullException>(() => manager.Compact<object>(default, policy));
            Should.Throw<ArgumentNullException>(() => manager.Compact(new CrdtDocument<object>(null!, metadata), policy));
            Should.Throw<ArgumentNullException>(() => manager.Compact(new CrdtDocument<object>(doc, null!), policy));
            Should.Throw<ArgumentNullException>(() => manager.Compact(new CrdtDocument<object>(doc, metadata), null!));
        }
        
        if (testAdvanceVector)
        {
            Should.Throw<ArgumentNullException>(() => manager.AdvanceVersionVector(null!, operation));
            Should.Throw<ArgumentException>(() => manager.AdvanceVersionVector(metadata, null!, 1L));
            Should.Throw<ArgumentException>(() => manager.AdvanceVersionVector(metadata, " ", 1L));
        }
    }

    [Fact]
    public void Reset_ShouldClearAllState()
    {
        // Arrange
        var metadata = new CrdtMetadata();
        var causalTs = new CausalTimestamp(timestampProviderMock.Object.Create(100), "replica", 1);

        metadata.Lww["$.a"] = causalTs;
        metadata.Fww["$.a"] = causalTs;
        metadata.PositionalTrackers["$.b"] = [];
        metadata.AverageRegisters["$.c"] = new Dictionary<string, AverageRegisterValue>();
        metadata.PriorityQueues["$.d"] = new LwwSetState(new Dictionary<object, ICrdtTimestamp>(), new Dictionary<object, CausalTimestamp>());
        metadata.LwwSets["$.e"] = new LwwSetState(new Dictionary<object, ICrdtTimestamp>(), new Dictionary<object, CausalTimestamp>());
        metadata.FwwSets["$.e2"] = new LwwSetState(new Dictionary<object, ICrdtTimestamp>(), new Dictionary<object, CausalTimestamp>());
        metadata.LwwMaps["$.f"] = new Dictionary<object, CausalTimestamp>();
        metadata.FwwMaps["$.f2"] = new Dictionary<object, CausalTimestamp>();
        metadata.OrMaps["$.g"] = new OrSetState(new Dictionary<object, ISet<Guid>>(), new Dictionary<object, IDictionary<Guid, CausalTimestamp>>());
        metadata.CounterMaps["$.h"] = new Dictionary<object, PnCounterState> { { "key", new PnCounterState(1, 1) } };

        var doc = new object();
        timestampProviderMock.Setup(p => p.Now()).Returns(timestampProviderMock.Object.Create(200));
        
        // Act
        manager.Reset(new CrdtDocument<object>(doc, metadata));
        
        // Assert
        metadata.Lww.ShouldBeEmpty();
        metadata.Fww.ShouldBeEmpty();
        metadata.PositionalTrackers.ShouldBeEmpty();
        metadata.AverageRegisters.ShouldBeEmpty();
        metadata.PriorityQueues.ShouldBeEmpty();
        metadata.LwwSets.ShouldBeEmpty();
        metadata.FwwSets.ShouldBeEmpty();
        metadata.LwwMaps.ShouldBeEmpty();
        metadata.FwwMaps.ShouldBeEmpty();
        metadata.OrMaps.ShouldBeEmpty();
        metadata.CounterMaps.ShouldBeEmpty();
    }

    [Fact]
    public void Compact_ShouldRemoveSeenExceptionsBasedOnPolicy()
    {
        // Arrange
        var metadata = new CrdtMetadata();
        var doc = new CrdtDocument<object>(new object(), metadata);

        var op1 = CreateOp("rep1", 100); // Policy says safe to compact
        var op2 = CreateOp("rep1", 200); // Policy says NOT safe to compact

        metadata.SeenExceptions.Add(op1);
        metadata.SeenExceptions.Add(op2);

        var policyMock = new Mock<ICompactionPolicy>();
        
        // Setup matching based on the new CompactionCandidate structure
        policyMock.Setup(p => p.IsSafeToCompact(It.Is<CompactionCandidate>(c => 
            c.Timestamp == op1.Timestamp && 
            c.ReplicaId == op1.ReplicaId && 
            c.Version == op1.Clock)))
            .Returns(true);
            
        policyMock.Setup(p => p.IsSafeToCompact(It.Is<CompactionCandidate>(c => 
            c.Timestamp == op2.Timestamp && 
            c.ReplicaId == op2.ReplicaId && 
            c.Version == op2.Clock)))
            .Returns(false);

        strategyProviderMock.Setup(p => p.GetStrategy(It.IsAny<Type>(), It.IsAny<CrdtPropertyInfo>())).Returns(Mock.Of<ICrdtStrategy>());

        // Act
        manager.Compact(doc, policyMock.Object);

        // Assert
        metadata.SeenExceptions.Count.ShouldBe(1);
        metadata.SeenExceptions.ShouldNotContain(op1);
        metadata.SeenExceptions.ShouldContain(op2);
    }

    [Fact]
    public void Compact_ShouldTraverseDocumentAndInvokeStrategyCompact()
    {
        // Arrange
        var dummyDoc = new OuterDoc();
        var doc = new CrdtDocument<OuterDoc>(dummyDoc, new CrdtMetadata());
        var policyMock = new Mock<ICompactionPolicy>();

        var mockStrategy = new Mock<ICrdtStrategy>();
        strategyProviderMock.Setup(p => p.GetStrategy(It.IsAny<Type>(), It.IsAny<CrdtPropertyInfo>())).Returns(mockStrategy.Object);

        // Act
        manager.Compact(doc, policyMock.Object);

        // Assert
        // We expect the following traversal paths:
        // $.inner
        // $.inner.value
        // $.list
        // $.list[0].value
        // $.list[1].value
        // $.dict
        // $.dict.['k1'].value
        // Total = 7 strategy invocations.
        mockStrategy.Verify(s => s.Compact(It.IsAny<CompactionContext>()), Times.Exactly(7));
    }

    [Theory]
    [InlineData(100, 50)]
    [InlineData(100, 100)]
    public void AdvanceVersionVector_WhenOperationIsOldOrSame_ShouldDoNothing(long vector, long newOp)
    {
        // Arrange
        var metadata = new CrdtMetadata();
        var replicaId = "replica-1";
        metadata.VersionVector[replicaId] = vector;

        // Act
        manager.AdvanceVersionVector(metadata, replicaId, newOp);

        // Assert
        metadata.VersionVector[replicaId].ShouldBe(vector);
    }

    [Fact]
    public void AdvanceVersionVector_WithNoExceptionsAndNoGap_ShouldAdvanceVector()
    {
        // Arrange
        var metadata = new CrdtMetadata();
        var replicaId = "replica-1";
        metadata.VersionVector[replicaId] = 100;

        // Act
        manager.AdvanceVersionVector(metadata, replicaId, 101);

        // Assert
        metadata.VersionVector[replicaId].ShouldBe(101);
    }

    [Fact]
    public void AdvanceVersionVector_WithNoExceptionsAndGap_ShouldNotAdvanceVector()
    {
        // Arrange
        var metadata = new CrdtMetadata();
        var replicaId = "replica-1";
        long initialVector = 100;
        metadata.VersionVector[replicaId] = initialVector;

        // Act
        manager.AdvanceVersionVector(metadata, replicaId, 105);

        // Assert
        metadata.VersionVector[replicaId].ShouldBe(initialVector);
    }

    [Fact]
    public void AdvanceVersionVector_WithGapInExceptions_ShouldAdvanceToLastContiguousTimestamp()
    {
        // Arrange
        var metadata = new CrdtMetadata();
        var replicaId = "replica-1";
        metadata.VersionVector[replicaId] = 100;
        
        // Exceptions with a gap at 103
        metadata.SeenExceptions.Add(CreateOp(replicaId, 102));
        metadata.SeenExceptions.Add(CreateOp(replicaId, 104));
        
        // Act - Apply the contiguous next operation (101)
        manager.AdvanceVersionVector(metadata, replicaId, 101);

        // Assert - should eat 102, stop at 103 (gap), leaving 104 in exceptions. 102 is pruned.
        metadata.VersionVector[replicaId].ShouldBe(102);
        metadata.SeenExceptions.Count.ShouldBe(1);
        metadata.SeenExceptions.Single().Clock.ShouldBe(104);
    }

    [Fact]
    public void AdvanceVersionVector_WithAllExceptionsPresent_ShouldAdvanceToNewTimestampAndPruneAll()
    {
        // Arrange
        var metadata = new CrdtMetadata();
        var replicaId = "replica-1";
        metadata.VersionVector[replicaId] = 100;
        
        // Exceptions that fill the gap after 101
        metadata.SeenExceptions.Add(CreateOp(replicaId, 102));
        metadata.SeenExceptions.Add(CreateOp(replicaId, 103));
        metadata.SeenExceptions.Add(CreateOp(replicaId, 104));
        
        // Act - Apply 101
        manager.AdvanceVersionVector(metadata, replicaId, 101);

        // Assert - should consume up to 104, completely emptying SeenExceptions
        metadata.VersionVector[replicaId].ShouldBe(104);
        metadata.SeenExceptions.ShouldBeEmpty();
    }
    
    private CrdtOperation CreateOp(string replicaId, long clockValue)
        => new(Guid.NewGuid(), replicaId, "path", OperationType.Upsert, null, timestampProviderMock.Object.Create(clockValue), clockValue);

    // Dummy classes for traversal testing
    internal class OuterDoc
    {
        public InnerDoc Inner { get; set; } = new();
        public List<InnerDoc> List { get; set; } = [new InnerDoc(), new InnerDoc()];
        public Dictionary<string, InnerDoc> Dict { get; set; } = new() { { "k1", new InnerDoc() } };
    }

    internal class InnerDoc
    {
        public string Value { get; set; } = "v";
    }
}