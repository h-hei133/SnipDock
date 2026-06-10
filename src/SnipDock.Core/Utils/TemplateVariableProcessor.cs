using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using SnipDock.Core.Models;

namespace SnipDock.Core.Utils
{
    public static partial class TemplateVariableProcessor
    {
        public static IReadOnlyList<TemplateVariable> ExtractVariables(string? content)
        {
            if (string.IsNullOrEmpty(content)) return Array.Empty<TemplateVariable>();

            var names = new List<string>();
            foreach (Match match in VariableRegex().Matches(content))
            {
                var name = match.Groups["name"].Value;
                if (!names.Contains(name, StringComparer.Ordinal))
                {
                    names.Add(name);
                }
            }

            return names.Select(name => new TemplateVariable(name)).ToList();
        }

        public static bool HasVariables(string? content)
        {
            return !string.IsNullOrEmpty(content) && VariableRegex().IsMatch(content);
        }

        public static string Render(string content, IReadOnlyDictionary<string, string?> values)
        {
            if (string.IsNullOrEmpty(content)) return string.Empty;
            if (values == null) throw new ArgumentNullException(nameof(values));

            return VariableRegex().Replace(content, match =>
            {
                var name = match.Groups["name"].Value;
                return values.TryGetValue(name, out var value) ? value ?? string.Empty : match.Value;
            });
        }

        [GeneratedRegex(@"\{\{\s*(?<name>[A-Za-z_][A-Za-z0-9_]*)\s*\}\}")]
        private static partial Regex VariableRegex();
    }
}
