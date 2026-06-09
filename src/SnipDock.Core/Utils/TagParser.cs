using System;
using System.Collections.Generic;
using System.Linq;

namespace SnipDock.Core.Utils
{
    public static class TagParser
    {
        private static readonly char[] Delimiters = new[] { ',', '，', ';', '；', '\r', '\n' };

        public static List<string> Parse(string input)
        {
            if (string.IsNullOrWhiteSpace(input))
            {
                return new List<string>();
            }

            return input
                .Split(Delimiters, StringSplitOptions.RemoveEmptyEntries)
                .Select(t => t.Trim())
                .Where(t => !string.IsNullOrEmpty(t))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
        }
    }
}