namespace Ama.CRDT.UnitTests.Extensions;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using Ama.CRDT.Extensions;
using Ama.CRDT.Services;
using Ama.CRDT.Services.Providers;
using Ama.CRDT.Services.Serialization;
using Ama.CRDT.Services.Strategies;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using Xunit;

public class BaseMessage { }
public class DerivedMessageA : BaseMessage { }
public class DerivedMessageB : BaseMessage { }

public class ServiceCollectionExtensionsTests
{
    [Fact]
    public void AddCrdt_WithDefaultOptions_RegistersDefaultJsonSerializer()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        services.AddCrdt();
        var sp = services.BuildServiceProvider();

        // Assert
        var serializer = sp.GetRequiredService<ICrdtSerializer>();
        serializer.ShouldBeOfType<JsonCrdtSerializer>();
    }

    [Fact]
    public void AddCrdt_WithBrotliCompression_RegistersBrotliJsonSerializer()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        services.AddCrdt(configure: null)
            .AddCrdtSystemTextJson(useBrotliCompression: true);
        var sp = services.BuildServiceProvider();

        // Assert
        var serializer = sp.GetRequiredService<ICrdtSerializer>();
        serializer.ShouldBeOfType<BrotliJsonCrdtSerializer>();
    }

    [Fact]
    public void AddCrdtSystemTextJson_ShouldInquireAllResolvers_AndMergePolymorphismOptions()
    {
        // Arrange
        var services = new ServiceCollection();
        
        // Add custom resolvers that define the same base type but different derived types
        services.AddCrdtJsonTypeInfoResolver(new MockResolverA());
        services.AddCrdtJsonTypeInfoResolver(new MockResolverB());

        // Add a global modifier to prove global modifiers apply regardless of resolver selection
        bool globalModifierExecuted = false;
        services.AddCrdtJsonModifier(ti => 
        {
            if (ti.Type == typeof(BaseMessage))
            {
                globalModifierExecuted = true;
            }
        });

        services.AddCrdt();

        var sp = services.BuildServiceProvider();
        var options = sp.GetRequiredKeyedService<JsonSerializerOptions>("Ama.CRDT");

        // Act
        var typeInfo = options.GetTypeInfo(typeof(BaseMessage));

        // Assert
        typeInfo.ShouldNotBeNull();
        typeInfo.PolymorphismOptions.ShouldNotBeNull();
        
        // Should contain derived types from BOTH Resolver A and Resolver B because they were both inquired and merged
        typeInfo.PolymorphismOptions!.DerivedTypes.Count.ShouldBe(2);
        typeInfo.PolymorphismOptions.DerivedTypes.ShouldContain(d => d.DerivedType == typeof(DerivedMessageA));
        typeInfo.PolymorphismOptions.DerivedTypes.ShouldContain(d => d.DerivedType == typeof(DerivedMessageB));

        // Ensure global modifier ran
        globalModifierExecuted.ShouldBeTrue();
    }

    [Fact]
    public void AddCrdt_ShouldThrowInvalidOperationException_WhenScopedServicesResolvedOutsideCrdtScopeFactory()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddCrdt();
        using var sp = services.BuildServiceProvider();

        // Act & Assert: Direct resolution from root container should fail validation
        Should.Throw<InvalidOperationException>(() => sp.GetRequiredService<ICrdtPatcher>())
            .Message.ShouldContain(nameof(ICrdtScopeFactory));

        Should.Throw<InvalidOperationException>(() => sp.GetRequiredService<ICrdtApplicator>())
            .Message.ShouldContain(nameof(ICrdtScopeFactory));

        Should.Throw<InvalidOperationException>(() => sp.GetRequiredService<IAsyncCrdtPatcher>())
            .Message.ShouldContain(nameof(ICrdtScopeFactory));

        Should.Throw<InvalidOperationException>(() => sp.GetRequiredService<IAsyncCrdtApplicator>())
            .Message.ShouldContain(nameof(ICrdtScopeFactory));

        // Act & Assert: Resolution from a standard non-CRDT IServiceScope should also fail validation
        using var standardScope = sp.CreateScope();
        Should.Throw<InvalidOperationException>(() => standardScope.ServiceProvider.GetRequiredService<ICrdtPatcher>())
            .Message.ShouldContain(nameof(ICrdtScopeFactory));

        Should.Throw<InvalidOperationException>(() => standardScope.ServiceProvider.GetRequiredService<ICrdtApplicator>())
            .Message.ShouldContain(nameof(ICrdtScopeFactory));
    }

    [Fact]
    public void AddCrdt_ShouldResolveScopedServicesPerReplicaScope_AndShareStatelessSingletons()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddCrdt();
        using var sp = services.BuildServiceProvider();
        var scopeFactory = sp.GetRequiredService<ICrdtScopeFactory>();

        // Act
        using var scopeA = scopeFactory.CreateScope("replica-A");
        using var scopeB = scopeFactory.CreateScope("replica-B");

        var patcherA = scopeA.ServiceProvider.GetRequiredService<ICrdtPatcher>();
        var patcherB = scopeB.ServiceProvider.GetRequiredService<ICrdtPatcher>();
        var applicatorA = scopeA.ServiceProvider.GetRequiredService<ICrdtApplicator>();
        var applicatorB = scopeB.ServiceProvider.GetRequiredService<ICrdtApplicator>();

        var strategyProviderRoot = sp.GetRequiredService<ICrdtStrategyProvider>();
        var strategyProviderA = scopeA.ServiceProvider.GetRequiredService<ICrdtStrategyProvider>();
        var strategyProviderB = scopeB.ServiceProvider.GetRequiredService<ICrdtStrategyProvider>();

        var comparerProviderRoot = sp.GetRequiredService<IElementComparerProvider>();
        var comparerProviderA = scopeA.ServiceProvider.GetRequiredService<IElementComparerProvider>();
        var comparerProviderB = scopeB.ServiceProvider.GetRequiredService<IElementComparerProvider>();

        var metadataManagerRoot = sp.GetRequiredService<ICrdtMetadataManager>();
        var metadataManagerA = scopeA.ServiceProvider.GetRequiredService<ICrdtMetadataManager>();
        var metadataManagerB = scopeB.ServiceProvider.GetRequiredService<ICrdtMetadataManager>();

        var mergerRoot = sp.GetRequiredService<ICrdtMerger>();
        var mergerA = scopeA.ServiceProvider.GetRequiredService<ICrdtMerger>();
        var mergerB = scopeB.ServiceProvider.GetRequiredService<ICrdtMerger>();

        var docIdProviderA = scopeA.ServiceProvider.GetRequiredService<IDocumentIdProvider>();
        var docIdProviderB = scopeB.ServiceProvider.GetRequiredService<IDocumentIdProvider>();

        var shardIdProviderA = scopeA.ServiceProvider.GetRequiredService<IShardIdProvider>();
        var shardIdProviderB = scopeB.ServiceProvider.GetRequiredService<IShardIdProvider>();

        var timestampProviderA = scopeA.ServiceProvider.GetRequiredService<ICrdtTimestampProvider>();
        var timestampProviderB = scopeB.ServiceProvider.GetRequiredService<ICrdtTimestampProvider>();

        var strategiesA = scopeA.ServiceProvider.GetServices<ICrdtStrategy>().ToList();
        var strategiesB = scopeB.ServiceProvider.GetServices<ICrdtStrategy>().ToList();

        // Assert: Stateful replica-bound services are distinct per scope
        patcherA.ShouldNotBeNull();
        patcherB.ShouldNotBeNull();
        patcherA.ShouldNotBeSameAs(patcherB);

        applicatorA.ShouldNotBeNull();
        applicatorB.ShouldNotBeNull();
        applicatorA.ShouldNotBeSameAs(applicatorB);

        // Assert: Stateless infrastructure and providers are shared singletons across all scopes and root
        strategyProviderA.ShouldBeSameAs(strategyProviderRoot);
        strategyProviderA.ShouldBeSameAs(strategyProviderB);

        comparerProviderA.ShouldBeSameAs(comparerProviderRoot);
        comparerProviderA.ShouldBeSameAs(comparerProviderB);

        metadataManagerA.ShouldBeSameAs(metadataManagerRoot);
        metadataManagerA.ShouldBeSameAs(metadataManagerB);

        mergerA.ShouldBeSameAs(mergerRoot);
        mergerA.ShouldBeSameAs(mergerB);

        docIdProviderA.ShouldBeSameAs(docIdProviderB);
        shardIdProviderA.ShouldBeSameAs(shardIdProviderB);
        timestampProviderA.ShouldBeSameAs(timestampProviderB);

        // Assert: All 32 CRDT strategies are registered as stateless singletons
        strategiesA.Count.ShouldBe(32);
        strategiesA.Count.ShouldBe(strategiesB.Count);
        for (int i = 0; i < strategiesA.Count; i++)
        {
            strategiesA[i].ShouldBeSameAs(strategiesB[i]);
        }
    }

    private sealed class MockResolverA : IJsonTypeInfoResolver
    {
        private readonly IJsonTypeInfoResolver _inner = new DefaultJsonTypeInfoResolver();
        public JsonTypeInfo? GetTypeInfo(Type type, JsonSerializerOptions options)
        {
            if (type == typeof(BaseMessage))
            {
                var ti = _inner.GetTypeInfo(type, options);
                if (ti != null)
                {
                    ti.PolymorphismOptions = new JsonPolymorphismOptions();
                    ti.PolymorphismOptions.DerivedTypes.Add(new JsonDerivedType(typeof(DerivedMessageA), "A"));
                }
                return ti;
            }
            return null;
        }
    }

    private sealed class MockResolverB : IJsonTypeInfoResolver
    {
        private readonly IJsonTypeInfoResolver _inner = new DefaultJsonTypeInfoResolver();
        public JsonTypeInfo? GetTypeInfo(Type type, JsonSerializerOptions options)
        {
            if (type == typeof(BaseMessage))
            {
                var ti = _inner.GetTypeInfo(type, options);
                if (ti != null)
                {
                    ti.PolymorphismOptions = new JsonPolymorphismOptions();
                    ti.PolymorphismOptions.DerivedTypes.Add(new JsonDerivedType(typeof(DerivedMessageB), "B"));
                }
                return ti;
            }
            return null;
        }
    }
}