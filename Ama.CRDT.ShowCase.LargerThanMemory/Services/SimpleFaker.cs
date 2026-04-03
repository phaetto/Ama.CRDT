namespace Ama.CRDT.ShowCase.LargerThanMemory.Services;

using System;
using System.Collections.Generic;
using System.Linq;

public sealed class SimpleFaker
{
    private readonly Random _random;

    private static readonly string[] FirstNames = ["John", "Jane", "Alice", "Bob", "Charlie", "Diana", "Eve", "Frank"];
    private static readonly string[] LastNames = ["Smith", "Doe", "Johnson", "Brown", "Williams", "Jones", "Miller", "Davis"];
    private static readonly string[] Words = ["lorem", "ipsum", "dolor", "sit", "amet", "consectetur", "adipiscing", "elit", "sed", "do", "eiusmod", "tempor", "incididunt", "ut", "labore", "et", "dolore", "magna", "aliqua", "enim", "ad", "minim", "veniam", "quis", "nostrud", "exercitation", "ullamco", "laboris", "nisi", "aliquip", "ex", "ea", "commodo", "consequat"];

    public SimpleFaker()
    {
        _random = new Random();
    }

    public string FullName() => $"{FirstNames[_random.Next(FirstNames.Length)]} {LastNames[_random.Next(LastNames.Length)]}";

    public string LoremSentence(int wordCount)
    {
        var words = Enumerable.Range(0, wordCount).Select(_ => Words[_random.Next(Words.Length)]).ToArray();
        if (words.Length == 0) return string.Empty;
        
        words[0] = char.ToUpper(words[0][0]) + words[0].Substring(1);
        return string.Join(" ", words) + ".";
    }

    public string LoremParagraphs(int count)
    {
        var paragraphs = new List<string>();
        for (int i = 0; i < count; i++)
        {
            int sentenceCount = _random.Next(3, 7);
            var sentences = Enumerable.Range(0, sentenceCount).Select(_ => LoremSentence(_random.Next(5, 10)));
            paragraphs.Add(string.Join(" ", sentences));
        }
        return string.Join("\n\n", paragraphs);
    }

    public List<string> LoremWords(int count)
    {
        return Enumerable.Range(0, count).Select(_ => Words[_random.Next(Words.Length)]).ToList();
    }
}