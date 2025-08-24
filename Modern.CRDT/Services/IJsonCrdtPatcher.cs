namespace Modern.CRDT.Services;

using Modern.CRDT.Models;
using System;
using System.Collections.Generic;
using System.Text.Json.Nodes;

/// <summary>
/// Defines the contract for a service that compares two versions of a data model
/// and generates a CRDT patch based on Last-Writer-Wins (LWW) semantics and property-specific strategies.
/// </summary>
public interface IJsonCrdtPatcher
{
    /// <summary>
    /// Compares two instances of a POCO and generates a CRDT patch.
    /// It recursively traverses the object graph, using reflection to find properties
    /// and applying the appropriate CRDT strategy for each one.
    /// </summary>
    /// <typeparam name="T">The type of the data model.</typeparam>
    /// <param name="from">The original document, containing the original state of the data and its metadata.</param>
    /// <param name="to">The modified document, containing the new state of the data and its metadata.</param>
    /// <returns>A <see cref="CrdtPatch"/> containing the operations to transform 'from' into 'to'.</returns>
    CrdtPatch GeneratePatch<T>(CrdtDocument<T> from, CrdtDocument<T> to) where T : class;

    /// <summary>
    /// Compares two JsonObject nodes property by property based on the given type's schema and generates CRDT operations.
    /// This method is typically used by strategies for recursive diffing.
    /// </summary>
    /// <param name="path">The base JSON path for the comparison.</param>
    /// <param name="type">The CLR type that defines the schema of the objects.</param>
    /// <param name="fromData">The original JsonObject.</param>
    /// <param name="fromMeta">The JsonObject representation of the metadata for the original object.</param>
    /// <param name="toData">The modified JsonObject.</param>
    /// <param name="toMeta">The JsonObject representation of the metadata for the modified object.</param>
    /// <param name="operations">The list to which generated operations will be added.</param>
    void DifferentiateObject(string path, Type type, JsonObject? fromData, JsonObject? fromMeta, JsonObject? toData, JsonObject? toMeta, List<CrdtOperation> operations);
}