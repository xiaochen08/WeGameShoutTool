using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Web.Script.Serialization;

namespace WeGameShoutTool
{
    public sealed class PhraseShareDocument
    {
        public string Format { get; set; }
        public int Version { get; set; }
        public List<string> Phrases { get; set; }
    }

    public static class PhraseShareFile
    {
        private const string FormatName = "WeGameShoutTool.PhraseShare";
        private const int FormatVersion = 1;
        private const long MaxFileBytes = 32768;

        public static void Export(string path, string[] phrases, Func<string, string> safetyCheck)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                throw new ArgumentException("分享文件路径不能为空。", "path");
            }
            if (safetyCheck == null)
            {
                throw new ArgumentNullException("safetyCheck");
            }
            string validation = Patcher.ValidatePhrasesForTesting(phrases);
            if (validation != null)
            {
                throw new InvalidDataException(validation);
            }
            for (int i = 0; i < phrases.Length; i++)
            {
                string category = safetyCheck(phrases[i]);
                if (category != null)
                {
                    throw new InvalidDataException("第" + (i + 1) + "条没有通过本地安全检查：" + category);
                }
            }

            string json = new JavaScriptSerializer
            {
                MaxJsonLength = 65536,
                RecursionLimit = 8
            }.Serialize(new PhraseShareDocument
            {
                Format = FormatName,
                Version = FormatVersion,
                Phrases = new List<string>(phrases)
            });
            string fullPath = Path.GetFullPath(path);
            string directory = Path.GetDirectoryName(fullPath);
            Directory.CreateDirectory(directory);
            string incomingPath = Path.Combine(directory, "." + Path.GetFileName(fullPath) + "." + Guid.NewGuid().ToString("N") + ".tmp");
            try
            {
                File.WriteAllText(incomingPath, json, new UTF8Encoding(false));
                if (File.Exists(fullPath))
                {
                    File.Replace(incomingPath, fullPath, null, true);
                }
                else
                {
                    File.Move(incomingPath, fullPath);
                }
            }
            finally
            {
                if (File.Exists(incomingPath))
                {
                    File.Delete(incomingPath);
                }
            }
        }

        public static string[] Import(string path, Func<string, string> safetyCheck)
        {
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
            {
                throw new FileNotFoundException("喊话分享文件不存在。", path);
            }
            if (safetyCheck == null)
            {
                throw new ArgumentNullException("safetyCheck");
            }
            if (new FileInfo(path).Length > MaxFileBytes)
            {
                throw new InvalidDataException("分享文件过大，无法导入。请使用工具导出的10条喊话分享文件。");
            }

            string contents = File.ReadAllText(path, Encoding.UTF8);
            PhraseShareDocument document;
            try
            {
                document = new JavaScriptSerializer
                {
                    MaxJsonLength = 65536,
                    RecursionLimit = 8
                }.Deserialize<PhraseShareDocument>(contents);
            }
            catch (ArgumentException ex)
            {
                throw new InvalidDataException("分享文件格式无效。请重新导出后再导入。", ex);
            }
            catch (InvalidOperationException ex)
            {
                throw new InvalidDataException("分享文件格式无效。请重新导出后再导入。", ex);
            }
            catch (FormatException ex)
            {
                throw new InvalidDataException("分享文件格式无效。请重新导出后再导入。", ex);
            }

            if (document == null || !string.Equals(document.Format, FormatName, StringComparison.Ordinal) || document.Version != FormatVersion)
            {
                throw new InvalidDataException("这不是受支持的喊话分享文件。请勿将模板库文件当作喊话分享文件导入。");
            }
            if (document.Phrases == null || document.Phrases.Count != 10)
            {
                throw new InvalidDataException("分享内容必须正好包含10条喊话。");
            }

            string[] phrases = document.Phrases.ToArray();
            string validation = Patcher.ValidatePhrasesForTesting(phrases);
            if (validation != null)
            {
                throw new InvalidDataException(validation);
            }
            for (int i = 0; i < phrases.Length; i++)
            {
                string category = safetyCheck(phrases[i]);
                if (category != null)
                {
                    throw new InvalidDataException("第" + (i + 1) + "条没有通过本地安全检查：" + category);
                }
            }
            return phrases;
        }
    }
}
