using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace ExperimentalCountWords
{
    class Program
    {
        static void Main(string[] args)
        {
            // Pride and Prejudice, by Jane Austin

            long totalGcBytesAllocated = GC.GetAllocatedBytesForCurrentThread();
            Utf8String sampleText = File.ReadAllTextUtf8("1342.txt");

            int hashSetCount = 0;
            var sw = Stopwatch.StartNew();
            for (int i = 0; i < 100; i++)
            {
                HashSet<Utf8String> hashSet = new HashSet<Utf8String>();
                foreach (Utf8String word in SplitIntoWords(sampleText))
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

        private static IEnumerable<Utf8String> SplitIntoWords(Utf8String text)
        {
            int startIndex = 0;

            while (true)
            {
                var span = MemoryMarshal.CreateReadOnlySpan(ref Unsafe.Add(ref Unsafe.AsRef<byte>(in text.GetPinnableReference()), startIndex), text.Length - startIndex);
                int indexOfSpace = span.IndexOf((byte)' ');
                if (indexOfSpace < 0)
                {
                    yield return text.Substring(startIndex).Trim(); // return last of the text
                    yield break;
                }

                yield return text.Substring(startIndex, indexOfSpace).Trim();
                startIndex += indexOfSpace + 1; // we know we can skip over a space character
            }
        }
    }
}
