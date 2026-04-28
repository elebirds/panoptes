using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace Panoptes.Presentation.UI.Domestic
{
    public static class BuildConfigFallbackParser
    {
        private static readonly Regex NumberPairRegex = new("\"([^\"]+)\"\\s*:\\s*(-?\\d+)", RegexOptions.Compiled);

        public readonly struct CostEntry
        {
            public readonly string Key;
            public readonly int Amount;
            public readonly bool IsPoint;

            public CostEntry(string key, int amount, bool isPoint)
            {
                Key = key;
                Amount = amount;
                IsPoint = isPoint;
            }
        }

        public static void EnumerateBuildingCosts(string jsonText, Action<string, IReadOnlyList<CostEntry>> consume)
        {
            if (consume == null)
            {
                return;
            }

            EnumerateArrayObjects(jsonText, "buildings", objectText =>
            {
                if (string.IsNullOrWhiteSpace(objectText) || !TryExtractStringField(objectText, "id", out var id))
                {
                    return;
                }

                var normalizedId = NormalizeToken(id);
                if (string.IsNullOrEmpty(normalizedId))
                {
                    return;
                }

                var costs = new List<CostEntry>(8);
                if (TryExtractObjectField(objectText, "resource_costs", out var resourceCosts))
                {
                    AppendCostEntries(costs, resourceCosts, false);
                }

                if (TryExtractObjectField(objectText, "point_costs", out var pointCosts))
                {
                    AppendCostEntries(costs, pointCosts, true);
                }

                if (costs.Count > 0)
                {
                    consume(normalizedId, costs);
                }
            });
        }

        private static void EnumerateArrayObjects(string jsonText, string fieldName, Action<string> consume)
        {
            if (string.IsNullOrWhiteSpace(jsonText) || string.IsNullOrWhiteSpace(fieldName) || consume == null)
            {
                return;
            }

            var token = $"\"{fieldName}\"";
            var fieldIndex = jsonText.IndexOf(token, StringComparison.Ordinal);
            if (fieldIndex < 0)
            {
                return;
            }

            var arrayStart = jsonText.IndexOf('[', fieldIndex);
            if (arrayStart < 0)
            {
                return;
            }

            var inString = false;
            var escaped = false;
            var depth = 0;
            var objectStart = -1;
            for (var i = arrayStart + 1; i < jsonText.Length; i++)
            {
                var c = jsonText[i];
                if (inString)
                {
                    if (escaped)
                    {
                        escaped = false;
                        continue;
                    }

                    if (c == '\\')
                    {
                        escaped = true;
                        continue;
                    }

                    if (c == '"')
                    {
                        inString = false;
                    }

                    continue;
                }

                if (c == '"')
                {
                    inString = true;
                    continue;
                }

                if (c == '{')
                {
                    if (depth == 0)
                    {
                        objectStart = i;
                    }

                    depth++;
                    continue;
                }

                if (c == '}')
                {
                    if (depth <= 0)
                    {
                        continue;
                    }

                    depth--;
                    if (depth == 0 && objectStart >= 0)
                    {
                        consume(jsonText.Substring(objectStart, i - objectStart + 1));
                        objectStart = -1;
                    }

                    continue;
                }

                if (c == ']' && depth == 0)
                {
                    break;
                }
            }
        }

        private static bool TryExtractStringField(string objectText, string fieldName, out string value)
        {
            value = string.Empty;
            if (string.IsNullOrWhiteSpace(objectText) || string.IsNullOrWhiteSpace(fieldName))
            {
                return false;
            }

            var pattern = $"\"{Regex.Escape(fieldName)}\"\\s*:\\s*\"([^\"]*)\"";
            var match = Regex.Match(objectText, pattern);
            if (!match.Success || match.Groups.Count < 2)
            {
                return false;
            }

            value = match.Groups[1].Value;
            return true;
        }

        private static bool TryExtractObjectField(string objectText, string fieldName, out string nestedObject)
        {
            nestedObject = string.Empty;
            if (string.IsNullOrWhiteSpace(objectText) || string.IsNullOrWhiteSpace(fieldName))
            {
                return false;
            }

            var token = $"\"{fieldName}\"";
            var fieldIndex = objectText.IndexOf(token, StringComparison.Ordinal);
            if (fieldIndex < 0)
            {
                return false;
            }

            var startBrace = objectText.IndexOf('{', fieldIndex);
            if (startBrace < 0)
            {
                return false;
            }

            var inString = false;
            var escaped = false;
            var depth = 0;
            for (var i = startBrace; i < objectText.Length; i++)
            {
                var c = objectText[i];
                if (inString)
                {
                    if (escaped)
                    {
                        escaped = false;
                        continue;
                    }

                    if (c == '\\')
                    {
                        escaped = true;
                        continue;
                    }

                    if (c == '"')
                    {
                        inString = false;
                    }

                    continue;
                }

                if (c == '"')
                {
                    inString = true;
                    continue;
                }

                if (c == '{')
                {
                    depth++;
                    continue;
                }

                if (c == '}')
                {
                    depth--;
                    if (depth == 0)
                    {
                        nestedObject = objectText.Substring(startBrace, i - startBrace + 1);
                        return true;
                    }
                }
            }

            return false;
        }

        private static void AppendCostEntries(List<CostEntry> target, string amountObject, bool isPoint)
        {
            if (target == null || string.IsNullOrWhiteSpace(amountObject))
            {
                return;
            }

            var matches = NumberPairRegex.Matches(amountObject);
            for (var i = 0; i < matches.Count; i++)
            {
                var key = NormalizeToken(matches[i].Groups[1].Value);
                if (string.IsNullOrWhiteSpace(key))
                {
                    continue;
                }

                if (!int.TryParse(matches[i].Groups[2].Value, out var amount))
                {
                    continue;
                }

                target.Add(new CostEntry(key, amount, isPoint));
            }
        }

        private static string NormalizeToken(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim().ToLowerInvariant();
        }
    }
}
