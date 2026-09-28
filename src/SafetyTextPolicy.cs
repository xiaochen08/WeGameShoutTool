using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Web.Script.Serialization;

namespace WeGameShoutTool
{
    public sealed class SafetyFinding
    {
        public int LineNumber;
        public string Category;
    }

    internal sealed class SafetyRuleFile
    {
        public string Version { get; set; }
        public List<SafetyRuleCategory> Categories { get; set; }
    }

    internal sealed class SafetyRuleCategory
    {
        public string Name { get; set; }
        public List<string> FormsBase64 { get; set; }
    }

    public static class SafetyTextPolicy
    {
        private sealed class CompiledRule
        {
            public string Category;
            public string Normalized;
        }

        private static readonly object SyncRoot = new object();
        private static List<CompiledRule> rules = new List<CompiledRule>();
        private static bool rulesReady;

        static SafetyTextPolicy()
        {
            string path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "data", "safety-rules.v1.json");
            if (File.Exists(path))
            {
                try
                {
                    LoadRules(path);
                }
                catch
                {
                    rulesReady = false;
                }
            }
        }

        public static bool RulesReady
        {
            get { lock (SyncRoot) { return rulesReady; } }
        }

        public static void LoadRules(string path)
        {
            lock (SyncRoot)
            {
                rules = new List<CompiledRule>();
                rulesReady = false;
            }
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
            {
                throw new FileNotFoundException("安全规则文件不存在。", path);
            }

            string json = File.ReadAllText(path, Encoding.UTF8);
            SafetyRuleFile file = new JavaScriptSerializer().Deserialize<SafetyRuleFile>(json);
            if (file == null || file.Version != "1" || file.Categories == null || file.Categories.Count == 0)
            {
                throw new InvalidDataException("安全规则文件结构或版本不兼容。");
            }

            List<CompiledRule> compiled = new List<CompiledRule>();
            foreach (SafetyRuleCategory category in file.Categories)
            {
                if (category == null || string.IsNullOrWhiteSpace(category.Name) || category.FormsBase64 == null)
                {
                    throw new InvalidDataException("安全规则类别无效。");
                }
                foreach (string encoded in category.FormsBase64)
                {
                    string decoded;
                    try
                    {
                        decoded = Encoding.UTF8.GetString(Convert.FromBase64String(encoded));
                    }
                    catch (Exception ex)
                    {
                        throw new InvalidDataException("安全规则包含无法解码的条目。", ex);
                    }
                    string normalized = NormalizeCore(decoded);
                    if (normalized.Length == 0)
                    {
                        throw new InvalidDataException("安全规则包含空条目。");
                    }
                    compiled.Add(new CompiledRule { Category = category.Name, Normalized = normalized });
                }
            }

            lock (SyncRoot)
            {
                rules = compiled;
                rulesReady = true;
            }
        }

        public static string NormalizeForTesting(string value)
        {
            return NormalizeCore(value);
        }

        public static string CheckTextForTesting(string value)
        {
            string body = RemoveAudiencePrefix(value ?? string.Empty);
            string normalized = NormalizeCore(body);
            if (normalized.Length == 0)
            {
                return null;
            }

            List<CompiledRule> snapshot;
            lock (SyncRoot)
            {
                if (!rulesReady)
                {
                    return "安全规则不可用";
                }
                snapshot = new List<CompiledRule>(rules);
            }

            foreach (CompiledRule rule in snapshot)
            {
                bool matched = rule.Normalized.Length == 1
                    ? string.Equals(normalized, rule.Normalized, StringComparison.Ordinal)
                    : normalized.IndexOf(rule.Normalized, StringComparison.Ordinal) >= 0;
                if (matched)
                {
                    return rule.Category;
                }
            }
            return null;
        }

        public static SafetyFinding CheckAllForTesting(string[] phrases)
        {
            if (phrases == null)
            {
                return new SafetyFinding { LineNumber = 0, Category = "内容不存在" };
            }
            for (int i = 0; i < phrases.Length; i++)
            {
                string category = CheckTextForTesting(phrases[i]);
                if (category != null)
                {
                    return new SafetyFinding { LineNumber = i + 1, Category = category };
                }
            }
            return null;
        }

        private static string NormalizeCore(string value)
        {
            string folded = (value ?? string.Empty).Normalize(NormalizationForm.FormKC).ToLowerInvariant();
            StringBuilder builder = new StringBuilder(folded.Length);
            foreach (char raw in folded)
            {
                char c = FoldSubstitution(raw);
                UnicodeCategory category = CharUnicodeInfo.GetUnicodeCategory(c);
                if (char.IsWhiteSpace(c)
                    || category == UnicodeCategory.ConnectorPunctuation
                    || category == UnicodeCategory.DashPunctuation
                    || category == UnicodeCategory.OpenPunctuation
                    || category == UnicodeCategory.ClosePunctuation
                    || category == UnicodeCategory.InitialQuotePunctuation
                    || category == UnicodeCategory.FinalQuotePunctuation
                    || category == UnicodeCategory.OtherPunctuation
                    || category == UnicodeCategory.MathSymbol
                    || category == UnicodeCategory.CurrencySymbol
                    || category == UnicodeCategory.ModifierSymbol
                    || category == UnicodeCategory.OtherSymbol)
                {
                    continue;
                }
                builder.Append(c);
            }
            return builder.ToString();
        }

        private static char FoldSubstitution(char c)
        {
            switch (c)
            {
                case '0': return 'o';
                case '1': return 'i';
                case '3': return 'e';
                case '4': return 'a';
                case '5': return 's';
                case '7': return 't';
                case '8': return 'b';
                case '9': return 'g';
                default: return c;
            }
        }

        private static string RemoveAudiencePrefix(string value)
        {
            string trimmed = value.Trim();
            if (trimmed.StartsWith("/all", StringComparison.OrdinalIgnoreCase)
                && trimmed.Length > 4
                && char.IsWhiteSpace(trimmed[4]))
            {
                return trimmed.Substring(5).TrimStart();
            }
            return trimmed;
        }
    }
}
