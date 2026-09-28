using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Web.Script.Serialization;

namespace WeGameShoutTool
{
    public sealed class TemplateTheme
    {
        public string Name;
        public List<TemplatePack> Packs;
    }

    public sealed class TemplatePack
    {
        public string Name;
        public List<string> Lines;
    }

    internal sealed class TemplateFile
    {
        public string Version { get; set; }
        public List<TemplateTheme> Themes { get; set; }
    }

    public sealed class TemplateLibrary
    {
        private readonly List<TemplateTheme> themes;
        private readonly List<TemplatePack> packs;
        private readonly string[] allPhrases;

        private TemplateLibrary(List<TemplateTheme> loadedThemes, string[] loadedPhrases)
        {
            themes = loadedThemes;
            packs = new List<TemplatePack>();
            foreach (TemplateTheme theme in loadedThemes)
            {
                packs.AddRange(theme.Packs);
            }
            allPhrases = loadedPhrases;
        }

        public int ThemeCount { get { return themes.Count; } }
        public int PackCount { get { return packs.Count; } }
        public int PhraseCount { get { return allPhrases.Length; } }
        public string[] AllPhrases { get { return (string[])allPhrases.Clone(); } }

        public static TemplateLibrary Load(string path, Func<string, string> safetyCheck)
        {
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
            {
                throw new FileNotFoundException("模板库文件不存在。", path);
            }
            if (safetyCheck == null)
            {
                throw new ArgumentNullException("safetyCheck");
            }

            TemplateFile file = Deserialize(File.ReadAllText(path, Encoding.UTF8));
            return CreateValidated(file, safetyCheck);
        }

        public static TemplateLibrary ImportAndReplace(string sourcePath, string destinationPath, Func<string, string> safetyCheck)
        {
            if (string.IsNullOrWhiteSpace(sourcePath) || !File.Exists(sourcePath))
            {
                throw new FileNotFoundException("导入的模板文件不存在。", sourcePath);
            }
            if (string.IsNullOrWhiteSpace(destinationPath))
            {
                throw new ArgumentException("模板保存位置不能为空。", "destinationPath");
            }
            if (safetyCheck == null)
            {
                throw new ArgumentNullException("safetyCheck");
            }

            TemplateLibrary imported;
            string extension = Path.GetExtension(sourcePath);
            if (string.Equals(extension, ".txt", StringComparison.OrdinalIgnoreCase))
            {
                imported = CreateValidated(CreateFromText(sourcePath), safetyCheck);
            }
            else if (string.Equals(extension, ".json", StringComparison.OrdinalIgnoreCase))
            {
                imported = CreateValidated(Deserialize(File.ReadAllText(sourcePath, Encoding.UTF8)), safetyCheck);
            }
            else
            {
                throw new InvalidDataException("只支持 UTF-8 的 TXT 或 JSON 模板文件。");
            }

            string directory = Path.GetDirectoryName(Path.GetFullPath(destinationPath));
            Directory.CreateDirectory(directory);
            string incomingPath = destinationPath + ".incoming";
            string backupPath = destinationPath + ".backup";
            File.WriteAllText(incomingPath, Serialize(imported.themes), new UTF8Encoding(false));
            if (File.Exists(destinationPath))
            {
                File.Replace(incomingPath, destinationPath, backupPath, true);
            }
            else
            {
                File.Move(incomingPath, destinationPath);
            }
            return imported;
        }

        private static TemplateFile Deserialize(string json)
        {
            return new JavaScriptSerializer
            {
                MaxJsonLength = int.MaxValue,
                RecursionLimit = 100
            }.Deserialize<TemplateFile>(json);
        }

        private static string Serialize(List<TemplateTheme> loadedThemes)
        {
            return new JavaScriptSerializer
            {
                MaxJsonLength = int.MaxValue,
                RecursionLimit = 100
            }.Serialize(new TemplateFile { Version = "6", Themes = loadedThemes });
        }

        private static TemplateFile CreateFromText(string path)
        {
            List<string> lines = new List<string>();
            foreach (string rawLine in File.ReadAllLines(path, Encoding.UTF8))
            {
                string line = (rawLine ?? string.Empty).Trim();
                if (line.Length > 0)
                {
                    lines.Add(line);
                }
            }
            if (lines.Count < 10 || lines.Count > 5000 || lines.Count % 10 != 0)
            {
                throw new InvalidDataException("TXT模板必须每行一条，总数为10的倍数，范围10至5000条。");
            }

            TemplateFile file = new TemplateFile { Version = "6", Themes = new List<TemplateTheme>() };
            int packCount = lines.Count / 10;
            for (int packIndex = 0; packIndex < packCount; packIndex++)
            {
                int themeIndex = packIndex / 10;
                while (file.Themes.Count <= themeIndex)
                {
                    file.Themes.Add(new TemplateTheme
                    {
                        Name = "导入主题" + (file.Themes.Count + 1).ToString("D2"),
                        Packs = new List<TemplatePack>()
                    });
                }
                TemplatePack pack = new TemplatePack
                {
                    Name = "阵容" + ((packIndex % 10) + 1).ToString("D2"),
                    Lines = lines.GetRange(packIndex * 10, 10)
                };
                file.Themes[themeIndex].Packs.Add(pack);
            }
            return file;
        }

        private static TemplateLibrary CreateValidated(TemplateFile file, Func<string, string> safetyCheck)
        {
            if (file == null || (file.Version != "5" && file.Version != "6") || file.Themes == null || file.Themes.Count == 0)
            {
                throw new InvalidDataException("模板库版本或主题结构无效。");
            }

            List<string> flattened = new List<string>();
            HashSet<string> unique = new HashSet<string>(StringComparer.Ordinal);
            foreach (TemplateTheme theme in file.Themes)
            {
                if (theme == null || string.IsNullOrWhiteSpace(theme.Name) || theme.Packs == null || theme.Packs.Count == 0)
                {
                    throw new InvalidDataException("模板主题结构无效。");
                }
                foreach (TemplatePack pack in theme.Packs)
                {
                    if (pack == null || string.IsNullOrWhiteSpace(pack.Name) || pack.Lines == null || pack.Lines.Count != 10)
                    {
                        throw new InvalidDataException("模板固定阵容结构无效。");
                    }
                    foreach (string line in pack.Lines)
                    {
                        ValidateLine(line, safetyCheck);
                        if (!unique.Add(line))
                        {
                            throw new InvalidDataException("模板库包含重复内容。");
                        }
                        flattened.Add(line);
                    }
                }
            }
            if (flattened.Count < 10 || flattened.Count > 5000)
            {
                throw new InvalidDataException("模板库总量必须在10至5000条之间。");
            }
            return new TemplateLibrary(file.Themes, flattened.ToArray());
        }

        public string[] GetThemeNames()
        {
            string[] result = new string[themes.Count];
            for (int i = 0; i < themes.Count; i++)
            {
                result[i] = themes[i].Name;
            }
            return result;
        }

        public string[] GetPackNames(int themeIndex)
        {
            EnsureThemeIndex(themeIndex);
            string[] result = new string[themes[themeIndex].Packs.Count];
            for (int i = 0; i < result.Length; i++)
            {
                result[i] = themes[themeIndex].Packs[i].Name;
            }
            return result;
        }

        public string[] GetPack(int themeIndex, int packIndex)
        {
            EnsureThemeIndex(themeIndex);
            if (packIndex < 0 || packIndex >= themes[themeIndex].Packs.Count)
            {
                throw new ArgumentOutOfRangeException("packIndex");
            }
            return themes[themeIndex].Packs[packIndex].Lines.ToArray();
        }

        public string[] GetRandomPack(Random random)
        {
            if (random == null)
            {
                throw new ArgumentNullException("random");
            }
            return packs[random.Next(packs.Count)].Lines.ToArray();
        }

        public string[] GetMixedRandom(int count, Random random)
        {
            if (count < 1 || count > allPhrases.Length)
            {
                throw new ArgumentOutOfRangeException("count");
            }
            if (random == null)
            {
                throw new ArgumentNullException("random");
            }

            string[] shuffled = (string[])allPhrases.Clone();
            for (int i = 0; i < count; i++)
            {
                int selected = random.Next(i, shuffled.Length);
                string temporary = shuffled[i];
                shuffled[i] = shuffled[selected];
                shuffled[selected] = temporary;
            }
            string[] result = new string[count];
            Array.Copy(shuffled, result, count);
            return result;
        }

        public string GetRandomLine(Random random, IEnumerable<string> excluded)
        {
            if (random == null)
            {
                throw new ArgumentNullException("random");
            }
            HashSet<string> blocked = excluded == null
                ? new HashSet<string>(StringComparer.Ordinal)
                : new HashSet<string>(excluded, StringComparer.Ordinal);
            if (blocked.Count >= allPhrases.Length)
            {
                throw new InvalidOperationException("没有可用的不重复模板。");
            }
            int start = random.Next(allPhrases.Length);
            for (int offset = 0; offset < allPhrases.Length; offset++)
            {
                string candidate = allPhrases[(start + offset) % allPhrases.Length];
                if (!blocked.Contains(candidate))
                {
                    return candidate;
                }
            }
            throw new InvalidOperationException("没有可用的不重复模板。");
        }

        private static void ValidateLine(string line, Func<string, string> safetyCheck)
        {
            if (string.IsNullOrWhiteSpace(line) || line.Length > 10)
            {
                throw new InvalidDataException("模板长度不符合1至10字符限制。");
            }
            foreach (char c in line)
            {
                UnicodeCategory category = CharUnicodeInfo.GetUnicodeCategory(c);
                bool punctuation = category == UnicodeCategory.ConnectorPunctuation
                    || category == UnicodeCategory.DashPunctuation
                    || category == UnicodeCategory.OpenPunctuation
                    || category == UnicodeCategory.ClosePunctuation
                    || category == UnicodeCategory.InitialQuotePunctuation
                    || category == UnicodeCategory.FinalQuotePunctuation
                    || category == UnicodeCategory.OtherPunctuation;
                if (punctuation && c != '？')
                {
                    throw new InvalidDataException("模板包含不允许的标点。");
                }
            }
            string categoryName = safetyCheck(line);
            if (categoryName != null)
            {
                throw new InvalidDataException("模板未通过本地安全检查：" + categoryName);
            }
        }

        private void EnsureThemeIndex(int themeIndex)
        {
            if (themeIndex < 0 || themeIndex >= themes.Count)
            {
                throw new ArgumentOutOfRangeException("themeIndex");
            }
        }
    }
}
