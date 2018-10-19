using System;
using System.Diagnostics;
using System.IO;
using System.Text;

namespace BaselineFormParse
{
    class Program
    {
        static void Main(string[] args)
        {
            byte[] countryCodes = ReadCountryCodesUrlEncoded();

            var sw = new Stopwatch();
            while (true)
            {
                int gcCount = GC.CollectionCount(0);
                for (int i = 0; i < 100_000; i++)
                {
                    var dict = FormParse.Parse(countryCodes, isQueryString: false);
                }
                Console.WriteLine(GC.CollectionCount(0) - gcCount);
            }
        }

        private static byte[] ReadCountryCodesUrlEncoded()
        {
            var allLines = File.ReadAllLines("CountryCodes.txt");

            StringBuilder sb = new StringBuilder();

            foreach (var line in allLines)
            {
                var split = line.Split(',');
                var code = split[0];
                var countryName = split[1].Trim('\"');

                sb.Append(Uri.EscapeDataString(code));
                sb.Append('=');
                sb.Append(Uri.EscapeDataString(countryName));
                sb.Append('&');
            }

            sb.Length--;

            return Encoding.UTF8.GetBytes(sb.ToString());
        }
    }
}
