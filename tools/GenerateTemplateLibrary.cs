using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Web.Script.Serialization;

namespace WeGameShoutTool
{
    internal static class TemplateGeneratorProgram
    {
        private static readonly string[] Themes = new string[]
        {
            "上路", "中路", "下路", "野区", "河道", "龙坑", "团战", "高地", "塔下", "基地"
        };

        private static readonly string[] Suffixes = new string[]
        {
            "就这？", "还来吗？", "还敢来？", "又来了？", "这就走？", "不再试试？", "还想拿？", "也想要？", "看见了吗？", "没看到？",
            "来得挺快", "走得更快", "差一点呢", "又差一点", "这波可惜", "这波没了", "这波白来", "这波空了", "这波急了", "这波慢了",
            "还能继续？", "还能守吗？", "还能抢吗？", "还能接吗？", "还要再来？", "还要争吗？", "还要追吗？", "还不回去？", "先别着急", "再想一想",
            "再试一次", "再来一轮", "继续试试", "继续来吧", "可以再来", "欢迎再来", "差点成功", "差得不多", "想法不错", "结果一般",
            "动作挺快", "判断挺快", "回去挺快", "转身挺快", "撤得挺快", "跑得挺快", "技能呢？", "闪现呢？", "治疗呢？", "视野呢？",
            "兵线呢？", "节奏呢？", "位置呢？", "伤害呢？", "准备好没？", "看清楚没？", "想明白没？", "这也能空？", "这也敢接？", "这也要抢？",
            "这也要追？", "这也叫支援？", "这算埋伏？", "这也能开？", "这是在等谁？", "这是在看谁？", "这就交了？", "这就开了？", "这就撤了？", "这就放了？",
            "这就丢了？", "这就没了？", "还在犹豫？", "还在观望？", "还在排队？", "还在赶路？", "还想绕后？", "还想反蹲？", "还想偷吗？", "还想守吗？",
            "还想追吗？", "还想开吗？", "要不要再看？", "要不要再等？", "要不要重来？", "要不要继续？", "要不要接团？", "要不要回去？", "这次算了？", "下次再来",
            "下波继续", "下一轮见", "机会给过了", "时间给够了", "路线看懂了？", "小图看了吗？", "草里有惊喜", "河里挺热闹", "塔下也要来？", "再猜一次"
        };

        public static int Main(string[] args)
        {
            if (args.Length != 2)
            {
                Console.Error.WriteLine("Usage: GenerateTemplateLibrary <rules-json> <output-json>");
                return 2;
            }

            SafetyTextPolicy.LoadRules(args[0]);
            if (Themes.Length != 10 || Suffixes.Length != 100)
            {
                throw new InvalidOperationException("默认库必须固定为10个主题和100个安全句尾。");
            }

            TemplateFile output = new TemplateFile { Version = "6", Themes = new List<TemplateTheme>() };
            HashSet<string> unique = new HashSet<string>(StringComparer.Ordinal);
            foreach (string themeName in Themes)
            {
                TemplateTheme theme = new TemplateTheme { Name = themeName, Packs = new List<TemplatePack>() };
                for (int packIndex = 0; packIndex < 10; packIndex++)
                {
                    TemplatePack pack = new TemplatePack
                    {
                        Name = "阵容" + (packIndex + 1).ToString("D2"),
                        Lines = new List<string>()
                    };
                    for (int lineIndex = 0; lineIndex < 10; lineIndex++)
                    {
                        string line = themeName + Suffixes[(packIndex * 10) + lineIndex];
                        string category = SafetyTextPolicy.CheckTextForTesting(line);
                        if (line.Length < 1 || line.Length > 10 || !IsPunctuationSafe(line) || category != null || !unique.Add(line))
                        {
                            throw new InvalidOperationException("模板生成校验失败，主题=" + themeName + "，阵容=" + (packIndex + 1));
                        }
                        pack.Lines.Add(line);
                    }
                    theme.Packs.Add(pack);
                }
                output.Themes.Add(theme);
            }

            string json = new JavaScriptSerializer
            {
                MaxJsonLength = int.MaxValue,
                RecursionLimit = 100
            }.Serialize(output);
            string directory = Path.GetDirectoryName(Path.GetFullPath(args[1]));
            Directory.CreateDirectory(directory);
            File.WriteAllText(args[1], json, new UTF8Encoding(false));
            Console.WriteLine("themes=10 packs=100 phrases=1000 unique=" + unique.Count + " unsafe=0");
            return 0;
        }

        private static bool IsPunctuationSafe(string value)
        {
            foreach (char c in value)
            {
                if (char.IsPunctuation(c) && c != '？')
                {
                    return false;
                }
            }
            return true;
        }
    }
}
