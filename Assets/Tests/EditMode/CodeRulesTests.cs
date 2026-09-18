using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using NUnit.Framework;

namespace LastGround.Tests
{
    /// <summary>
    /// Static checks for project rules that the compiler cannot enforce (AGENTS.md, TDD_02 §24A.4):
    /// culture-sensitive string/number APIs break on Turkish devices (I/İ, decimal comma).
    /// </summary>
    public class CodeRulesTests
    {
        static readonly (Regex pattern, string rule)[] Rules =
        {
            (new Regex(@"\.To(Upper|Lower)\(\s*\)"), "ToUpper/ToLower without culture — use ToUpperInvariant or put upper-case text in the string table"),
            (new Regex(@"\b(float|double|int|long|decimal)\.Parse\([^,)]*\)"), "Parse without CultureInfo.InvariantCulture"),
            (new Regex(@"\bstring\.Compare\([^,]+,[^,]+\)"), "string.Compare without StringComparison"),
            (new Regex(@"\bUnityEngine\.Random\b|\bnew System\.Random\b"), "Non-deterministic RNG — use DeterministicRandom"),
        };

        [Test]
        public void Scripts_FollowCultureAndDeterminismRules()
        {
            var violations = new List<string>();
            foreach (string file in Directory.GetFiles("Assets/Scripts", "*.cs", SearchOption.AllDirectories))
            {
                string[] lines = File.ReadAllLines(file);
                for (int i = 0; i < lines.Length; i++)
                {
                    string line = lines[i];
                    if (line.TrimStart().StartsWith("//")) continue;
                    foreach (var (pattern, rule) in Rules)
                    {
                        if (pattern.IsMatch(line))
                            violations.Add($"{file}:{i + 1}: {rule}");
                    }
                }
            }
            Assert.IsEmpty(violations, string.Join("\n", violations));
        }
    }
}
