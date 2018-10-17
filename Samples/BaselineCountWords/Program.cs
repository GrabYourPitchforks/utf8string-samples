using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;

namespace BaselineCountWords
{
    class Program
    {
        static void Main(string[] args)
        {
            // War & Peace

            long totalGcBytesAllocated = GC.GetAllocatedBytesForCurrentThread();
            string sampleText = File.ReadAllText("1342.txt");

            int hashSetCount = 0;
            var sw = Stopwatch.StartNew();
            for (int i = 0; i < 100; i++)
            {
                HashSet<string> hashSet = new HashSet<string>();
                foreach (string word in SplitIntoWords(sampleText))
                {
                    hashSet.Add(word);
                }
                hashSetCount = hashSet.Count;
            }

            totalGcBytesAllocated = GC.GetAllocatedBytesForCurrentThread() - totalGcBytesAllocated;

            Console.WriteLine($"Total word count: {hashSetCount}");
            Console.WriteLine($"Total GC bytes allocated: {totalGcBytesAllocated:N0}");
            Console.WriteLine($"Time elapsed: {sw.ElapsedMilliseconds} ms");
            Console.WriteLine($"Gen0 collection count: {GC.CollectionCount(0)}");
        }

        private static IEnumerable<string> SplitIntoWords(string text)
        {
            int startIndex = 0;

            while (true)
            {
                int indexOfSpace = text.IndexOf(' ', startIndex);
                if (indexOfSpace < 0)
                {
                    yield return text.Substring(startIndex).Trim(); // return last of the text
                    yield break;
                }

                yield return text.Substring(startIndex, indexOfSpace - startIndex).Trim();
                startIndex = indexOfSpace + 1; // we know we can skip over a space character
            }
        }
    }
}
