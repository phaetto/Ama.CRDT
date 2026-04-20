namespace Ama.CRDT.UnitTests.Extensions;

using System;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using Ama.CRDT.Extensions;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using Xunit;

public class BaseMessage { }
public class DerivedMessageA : BaseMessage { }
public class DerivedMessageB : BaseMessage { }

public class ServiceCollectionExtensionsTests
{
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