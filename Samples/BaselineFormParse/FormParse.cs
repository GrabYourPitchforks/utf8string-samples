using System;
using System.Buffers;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;

namespace BaselineFormParse
{
    public static class FormParse
    {
        // Given input that starts with "%xx", returns the integer representation, or -1 if invalid
        private static int PercentUnescape(ReadOnlySpan<byte> input)
        {
            Debug.Assert(input.Length >= 1 && input[0] == (byte)'%');

            if (input.Length < 3)
            {
                return -1; // not enough data to percent-unescape
            }

            int retVal;

            int nextChar = input[1];
            if ((uint)(nextChar - '0') <= (uint)('9' - '0'))
            {
                retVal = (nextChar - '0') << 4;
            }
            else if ((uint)(nextChar - 'a') <= (uint)('f' - 'a'))
            {
                retVal = (nextChar - 'a' + 10) << 4;
            }
            else if ((uint)(nextChar - 'A') <= (uint)('F' - 'A'))
            {
                retVal = (nextChar - 'A' + 10) << 4;
            }
            else
            {
                return -1;
            }

            nextChar = input[2];
            if ((uint)(nextChar - '0') <= (uint)('9' - '0'))
            {
                retVal += (nextChar - '0');
            }
            else if ((uint)(nextChar - 'a') <= (uint)('f' - 'a'))
            {
                retVal += (nextChar - 'a' + 10);
            }
            else if ((uint)(nextChar - 'A') <= (uint)('F' - 'A'))
            {
                retVal += (nextChar - 'A' + 10);
            }
            else
            {
                return -1;
            }

            return retVal;
        }

        private static unsafe string MakeStringFrom(ReadOnlySpan<byte> input, ref byte[] borrowedArray, bool isQueryString)
        {
            // If there are any %xx characters in the string, unescape them now.

            int indexOfPercentChar = input.IndexOf((byte)'%');
            if (indexOfPercentChar >= 0)
            {
                if (borrowedArray == null)
                {
                    borrowedArray = ArrayPool<byte>.Shared.Rent(input.Length);
                }
                else if (borrowedArray.Length < input.Length)
                {
                    ArrayPool<byte>.Shared.Return(borrowedArray);
                    borrowedArray = ArrayPool<byte>.Shared.Rent(input.Length);
                }

                ReadOnlySpan<byte> slicedInput = input;
                int borrowedArrayOffset = 0;

                do
                {
                    // Copy everything up until the % character we saw

                    slicedInput.Slice(0, indexOfPercentChar).CopyTo(borrowedArray.AsSpan(borrowedArrayOffset));
                    slicedInput = slicedInput.Slice(indexOfPercentChar);
                    borrowedArrayOffset += indexOfPercentChar;

                    // Attempt percent-unescaping

                    int byteValue = PercentUnescape(slicedInput);
                    if (byteValue < 0)
                    {
                        // Not a valid %xx escape sequence - skip it
                        borrowedArray[borrowedArrayOffset++] = (byte)'%';
                        slicedInput = slicedInput.Slice(1);
                    }
                    else
                    {
                        Debug.Assert(byteValue <= byte.MaxValue);
                        borrowedArray[borrowedArrayOffset++] = (byte)byteValue;
                        slicedInput = slicedInput.Slice(3);
                    }

                    indexOfPercentChar = slicedInput.IndexOf((byte)'%'); // and search again
                } while (indexOfPercentChar >= 0);

                // If there's any leftover data, copy it now, then point the input span to the rented buffer

                slicedInput.CopyTo(borrowedArray.AsSpan(borrowedArrayOffset));
                input = borrowedArray.AsSpan(0, borrowedArrayOffset + slicedInput.Length);
            }

            if (isQueryString)
            {
                // Use pointers to smuggle the ref struct span across a closure
                fixed (byte* pInput = input)
                {
                    return string.Create(Encoding.UTF8.GetCharCount(input), (ptr: (IntPtr)pInput, length: input.Length), (span, state) =>
                    {
                        int charCount = Encoding.UTF8.GetChars(new Span<byte>((byte*)state.ptr, state.length), span);
                        Debug.Assert(charCount == span.Length);

                        // replace '+' with ' '
                        while (!span.IsEmpty)
                        {
                            int idxOfChar = span.IndexOf('+');
                            if (idxOfChar < 0) { break; }
                            span[idxOfChar] = ' ';
                            span = span.Slice(idxOfChar + 1);
                        }
                    });
                }
            }
            else
            {
                // no need to replace '+' with ' '
                return Encoding.UTF8.GetString(input);
            }
        }

        public static Dictionary<string, string> Parse(ReadOnlySpan<byte> input, bool isQueryString)
        {
            var retVal = new Dictionary<string, string>();

            byte[] borrowedArray = null;

            // Find all key1=value1&key2=value2&... pairs

            while (true)
            {
                int indexOfAmpersand = input.IndexOf((byte)'&');
                if (indexOfAmpersand < 0) { break; } // EOF

                int indexOfEqualChar = input.Slice(0, indexOfAmpersand).IndexOf((byte)'=');
                if (indexOfEqualChar < 0)
                {
                    // treat as key=<empty>
                    retVal[MakeStringFrom(input.Slice(0, indexOfAmpersand), ref borrowedArray, isQueryString)] = string.Empty;
                }
                else
                {
                    // key=value
                    retVal[MakeStringFrom(input.Slice(0, indexOfEqualChar), ref borrowedArray, isQueryString)]
                        = MakeStringFrom(input.Slice(indexOfEqualChar + 1, indexOfAmpersand - indexOfEqualChar - 1), ref borrowedArray, isQueryString);
                }

                input = input.Slice(indexOfAmpersand + 1);
            }

            // We've reached EOF.
            // Parse any remaining data now

            if (!input.IsEmpty)
            {
                int indexOfEqualChar = input.IndexOf((byte)'=');
                if (indexOfEqualChar < 0)
                {
                    // treat as key=<empty>
                    retVal[MakeStringFrom(input, ref borrowedArray, isQueryString)] = string.Empty;
                }
                else
                {
                    // key=value
                    retVal[MakeStringFrom(input.Slice(0, indexOfEqualChar), ref borrowedArray, isQueryString)]
                        = MakeStringFrom(input.Slice(indexOfEqualChar + 1), ref borrowedArray, isQueryString);
                }
            }

            // Clean up

            if (borrowedArray != null)
            {
                ArrayPool<byte>.Shared.Return(borrowedArray);
            }

            return retVal;
        }
    }
}
