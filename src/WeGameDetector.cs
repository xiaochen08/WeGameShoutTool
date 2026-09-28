using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using Microsoft.Win32;

namespace WeGameShoutTool
{
    public sealed class WeGameStatus
    {
        public bool IsRunning;
        public string ExecutablePath;
        public string Version;
        public string AssistantExecutablePath;
        public string AssistantVersion;

        public string Signature()
        {
            return IsRunning.ToString()
                + "|" + (ExecutablePath ?? string.Empty)
                + "|" + (Version ?? string.Empty)
                + "|" + (AssistantExecutablePath ?? string.Empty)
                + "|" + (AssistantVersion ?? string.Empty);
        }
    }

    public static class ToolCompatibility
    {
        public const string ToolVersion = "6.22";
        public const string ToolAssemblyVersion = "6.22.0.0";
        public const string SupportedWeGameVersion = "6.6.0.9080";
        public const string NewlySupportedWeGameVersion = "6.6.1.9201";
        public const string SupportedAssistantVersion = "5.15.1.1018";
        public const string SupportedWeGameVersionsDisplay = SupportedWeGameVersion + "、" + NewlySupportedWeGameVersion;
        public const string UnsupportedMessage = "软件已更新，当前工具版本不适用，请联系作者。";

        public static int Evaluate(WeGameStatus status)
        {
            if (status == null)
            {
                return 0;
            }
            return EvaluateVersionsForTesting(status.Version, status.AssistantVersion);
        }

        public static int EvaluateVersionsForTesting(string weGameVersion, string assistantVersion)
        {
            string normalizedWeGame = NormalizeVersion(weGameVersion);
            string normalizedAssistant = NormalizeVersion(assistantVersion);
            if (normalizedWeGame == null || normalizedAssistant == null)
            {
                return 0;
            }
            bool supportedWeGame = string.Equals(normalizedWeGame, SupportedWeGameVersion, StringComparison.OrdinalIgnoreCase)
                || string.Equals(normalizedWeGame, NewlySupportedWeGameVersion, StringComparison.OrdinalIgnoreCase);
            return supportedWeGame
                && string.Equals(normalizedAssistant, SupportedAssistantVersion, StringComparison.OrdinalIgnoreCase)
                ? 1
                : -1;
        }

        public static string BuildAlertMessage(WeGameStatus status)
        {
            string weGameVersion = status == null ? "未知" : DisplayVersion(status.Version);
            string assistantVersion = status == null ? "未知" : DisplayVersion(status.AssistantVersion);
            return UnsupportedMessage
                + "\r\n\r\n工具版本：V" + ToolVersion
                + "\r\nWeGame版本：" + weGameVersion
                + "\r\n助手版本：" + assistantVersion;
        }

        public static void EnsureSupported(WeGameStatus status)
        {
            int compatibility = Evaluate(status);
            if (compatibility == 1)
            {
                return;
            }
            if (compatibility < 0)
            {
                throw new InvalidOperationException(BuildAlertMessage(status));
            }
            throw new InvalidOperationException(
                "无法确认 WeGame 或 WeGame 助手版本，已停止修改。\r\n\r\n请确认 WeGame 安装完整后重试；仍无法检测请联系作者。");
        }

        private static string NormalizeVersion(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return null;
            }
            string normalized = value.Trim();
            return normalized == "未知" || normalized == "未找到" ? null : normalized;
        }

        private static string DisplayVersion(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? "未知" : value.Trim();
        }
    }

    public static class WeGameDetector
    {
        public static WeGameStatus Detect()
        {
            List<string> processPaths = new List<string>();
            bool running = false;
            try
            {
                foreach (Process process in Process.GetProcessesByName("wegame"))
                {
                    running = true;
                    try
                    {
                        string path = process.MainModule == null ? null : process.MainModule.FileName;
                        if (!string.IsNullOrWhiteSpace(path))
                        {
                            processPaths.Add(path);
                        }
                    }
                    catch
                    {
                    }
                    finally
                    {
                        process.Dispose();
                    }
                }
            }
            catch
            {
            }

            List<string> candidates = new List<string>();
            candidates.AddRange(ReadRegistryCandidates());
            AddCandidate(candidates, Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86));
            AddCandidate(candidates, Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles));
            candidates.Add(@"L:\Program Files (x86)\WeGame\wegame.exe");

            WeGameStatus result = DetectForTesting(processPaths.ToArray(), candidates.ToArray());
            result.IsRunning = running;
            return result;
        }

        public static WeGameStatus DetectForTesting(string[] processPaths, string[] candidatePaths)
        {
            bool running = processPaths != null && processPaths.Length > 0;
            string selected = FirstExistingExecutable(processPaths);
            if (selected == null)
            {
                selected = FirstExistingExecutable(candidatePaths);
            }

            string version = ReadExecutableVersion(selected, "未知");
            string assistantExecutable = GetAssistantExecutablePath(selected);
            string assistantVersion = ReadExecutableVersion(assistantExecutable, "未找到");
            return new WeGameStatus
            {
                IsRunning = running,
                ExecutablePath = selected,
                Version = version,
                AssistantExecutablePath = assistantExecutable,
                AssistantVersion = assistantVersion
            };
        }

        public static WeGameStatus DetectFromExecutablePath(string weGameExecutablePath)
        {
            if (string.IsNullOrWhiteSpace(weGameExecutablePath))
            {
                throw new ArgumentException("WeGame 程序路径不能为空。", "weGameExecutablePath");
            }
            string fullPath = Path.GetFullPath(weGameExecutablePath);
            if (!string.Equals(Path.GetFileName(fullPath), "wegame.exe", StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException("关闭前记录的程序不是 wegame.exe，已停止操作。");
            }
            if (!File.Exists(fullPath))
            {
                throw new FileNotFoundException("关闭前记录的 WeGame 程序已不存在。", fullPath);
            }
            return DetectForTesting(new string[0], new string[] { fullPath });
        }

        public static string GetAssistantExecutablePathForTesting(string weGameExecutablePath)
        {
            return GetAssistantExecutablePath(weGameExecutablePath);
        }

        private static string GetAssistantExecutablePath(string weGameExecutablePath)
        {
            if (string.IsNullOrWhiteSpace(weGameExecutablePath))
            {
                return null;
            }
            try
            {
                string installDirectory = Path.GetDirectoryName(Path.GetFullPath(weGameExecutablePath));
                if (string.IsNullOrWhiteSpace(installDirectory))
                {
                    return null;
                }
                string candidate = Path.Combine(installDirectory, "apps", "Pallas", "pallas.exe");
                return File.Exists(candidate) ? candidate : null;
            }
            catch
            {
                return null;
            }
        }

        private static string ReadExecutableVersion(string executablePath, string fallback)
        {
            if (string.IsNullOrWhiteSpace(executablePath) || !File.Exists(executablePath))
            {
                return fallback;
            }
            try
            {
                FileVersionInfo info = FileVersionInfo.GetVersionInfo(executablePath);
                string version = !string.IsNullOrWhiteSpace(info.FileVersion)
                    ? info.FileVersion
                    : info.ProductVersion;
                return string.IsNullOrWhiteSpace(version) ? fallback : version.Trim();
            }
            catch
            {
                return fallback;
            }
        }

        private static string FirstExistingExecutable(IEnumerable<string> paths)
        {
            if (paths == null)
            {
                return null;
            }
            foreach (string raw in paths)
            {
                string path = NormalizeCandidate(raw);
                if (path != null && File.Exists(path))
                {
                    return Path.GetFullPath(path);
                }
            }
            return null;
        }

        private static string NormalizeCandidate(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return null;
            }
            string path = value.Trim().Trim('"');
            int iconSuffix = path.LastIndexOf(',');
            if (iconSuffix > 1)
            {
                int ignored;
                if (int.TryParse(path.Substring(iconSuffix + 1), out ignored))
                {
                    path = path.Substring(0, iconSuffix).Trim().Trim('"');
                }
            }
            if (Directory.Exists(path))
            {
                path = Path.Combine(path, "wegame.exe");
            }
            return path;
        }

        private static IEnumerable<string> ReadRegistryCandidates()
        {
            List<string> result = new List<string>();
            RegistryHive[] hives = new RegistryHive[] { RegistryHive.LocalMachine, RegistryHive.CurrentUser };
            RegistryView[] views = new RegistryView[] { RegistryView.Registry32, RegistryView.Registry64 };
            foreach (RegistryHive hive in hives)
            {
                foreach (RegistryView view in views)
                {
                    try
                    {
                        using (RegistryKey baseKey = RegistryKey.OpenBaseKey(hive, view))
                        using (RegistryKey uninstall = baseKey.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall"))
                        {
                            if (uninstall == null)
                            {
                                continue;
                            }
                            foreach (string subName in uninstall.GetSubKeyNames())
                            {
                                using (RegistryKey subKey = uninstall.OpenSubKey(subName))
                                {
                                    if (subKey == null)
                                    {
                                        continue;
                                    }
                                    string displayName = Convert.ToString(subKey.GetValue("DisplayName"));
                                    if (displayName.IndexOf("WeGame", StringComparison.OrdinalIgnoreCase) < 0)
                                    {
                                        continue;
                                    }
                                    string location = Convert.ToString(subKey.GetValue("InstallLocation"));
                                    string icon = Convert.ToString(subKey.GetValue("DisplayIcon"));
                                    if (!string.IsNullOrWhiteSpace(location))
                                    {
                                        result.Add(location);
                                    }
                                    if (!string.IsNullOrWhiteSpace(icon))
                                    {
                                        result.Add(icon);
                                    }
                                }
                            }
                        }
                    }
                    catch
                    {
                    }
                }
            }
            return result;
        }

        private static void AddCandidate(List<string> candidates, string programFiles)
        {
            if (!string.IsNullOrWhiteSpace(programFiles))
            {
                candidates.Add(Path.Combine(programFiles, "WeGame", "wegame.exe"));
            }
        }
    }
}
