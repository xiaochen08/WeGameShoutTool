using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;

namespace WeGameShoutTool
{
    public static class WeGameProcessController
    {
        public static int[] SelectVerifiedProcessIdsForTesting(string executablePath, int[] processIds, string[] processPaths)
        {
            if (processIds == null || processPaths == null || processIds.Length != processPaths.Length)
            {
                throw new ArgumentException("进程编号和路径必须一一对应。");
            }
            string expected = NormalizeWeGameExecutable(executablePath, false);
            List<int> selected = new List<int>();
            for (int i = 0; i < processPaths.Length; i++)
            {
                string candidate;
                try
                {
                    candidate = Path.GetFullPath(processPaths[i]);
                }
                catch
                {
                    continue;
                }
                if (string.Equals(candidate, expected, StringComparison.OrdinalIgnoreCase))
                {
                    selected.Add(processIds[i]);
                }
            }
            return selected.ToArray();
        }

        public static void CloseVerifiedProcesses(string executablePath, Action<string> report)
        {
            string expected = NormalizeWeGameExecutable(executablePath, true);
            List<Process> targets = new List<Process>();
            bool sawNamedProcess = false;
            try
            {
                string installRoot = Path.GetDirectoryName(expected);
                string[] processNames = new string[] { "wegame", "wegame_env", "pallas", "browser" };
                foreach (string processName in processNames)
                {
                    foreach (Process process in Process.GetProcessesByName(processName))
                    {
                        sawNamedProcess = true;
                        bool keep = false;
                        try
                        {
                            string candidate = process.MainModule == null ? null : process.MainModule.FileName;
                            keep = IsUnderDirectory(candidate, installRoot);
                            if (report != null)
                            {
                                report("进程检查：" + processName + "，PID=" + process.Id + "，路径=" + (candidate ?? "无法读取") + "，属于当前WeGame=" + keep + "。");
                            }
                        }
                        catch (Exception ex)
                        {
                            if (report != null)
                            {
                                report("进程检查失败：" + processName + "，PID=" + process.Id + "，" + ex.GetType().Name + "：" + ex.Message);
                            }
                        }
                        if (keep)
                        {
                            targets.Add(process);
                        }
                        else
                        {
                            process.Dispose();
                        }
                    }
                }

                if (targets.Count == 0)
                {
                    if (sawNamedProcess)
                    {
                        throw new InvalidOperationException("无法确认 WeGame 子进程路径，请手动退出 WeGame 后重试。");
                    }
                    if (report != null)
                    {
                        report("WeGame 已退出，无需关闭。");
                    }
                    return;
                }

                if (report != null)
                {
                    report("正在关闭 WeGame。");
                }
                foreach (Process process in targets)
                {
                    try
                    {
                        process.CloseMainWindow();
                    }
                    catch
                    {
                    }
                }
                foreach (Process process in targets)
                {
                    try
                    {
                        if (!process.HasExited)
                        {
                            process.WaitForExit(2500);
                        }
                        if (!process.HasExited)
                        {
                            if (report != null)
                            {
                                report("进程未正常退出，准备强制结束：PID=" + process.Id + "，名称=" + process.ProcessName + "。");
                            }
                            process.Kill();
                            process.WaitForExit(8000);
                        }
                        if (!process.HasExited)
                        {
                            throw new TimeoutException("WeGame 没有完全退出。");
                        }
                    }
                    catch (InvalidOperationException)
                    {
                    }
                    catch (Exception ex)
                    {
                        throw new InvalidOperationException("无法自动关闭 WeGame，请手动从系统托盘退出后重试。", ex);
                    }
                }
                if (report != null)
                {
                    report("WeGame 已关闭。");
                }
            }
            finally
            {
                foreach (Process process in targets)
                {
                    process.Dispose();
                }
            }
        }

        public static void StartVerified(string executablePath, Action<string> report)
        {
            string expected = NormalizeWeGameExecutable(executablePath, true);
            ProcessStartInfo startInfo = new ProcessStartInfo
            {
                FileName = expected,
                WorkingDirectory = Path.GetDirectoryName(expected),
                UseShellExecute = true
            };
            Process.Start(startInfo);
            if (report != null)
            {
                report("WeGame 已重新打开。路径=" + expected + "，工作目录=" + startInfo.WorkingDirectory + "。");
            }
        }

        private static string NormalizeWeGameExecutable(string executablePath, bool requireExists)
        {
            if (string.IsNullOrWhiteSpace(executablePath))
            {
                throw new InvalidOperationException("没有找到 WeGame 程序位置，无法安全操作。");
            }
            string fullPath = Path.GetFullPath(executablePath);
            if (!string.Equals(Path.GetFileName(fullPath), "wegame.exe", StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException("检测到的程序不是 wegame.exe，已停止操作。");
            }
            if (requireExists && !File.Exists(fullPath))
            {
                throw new FileNotFoundException("WeGame 程序不存在。", fullPath);
            }
            return fullPath;
        }

        private static bool IsUnderDirectory(string candidatePath, string directory)
        {
            if (string.IsNullOrWhiteSpace(candidatePath) || string.IsNullOrWhiteSpace(directory))
            {
                return false;
            }
            string candidate = Path.GetFullPath(candidatePath).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            string root = Path.GetFullPath(directory).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            return string.Equals(candidate, root, StringComparison.OrdinalIgnoreCase)
                || candidate.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);
        }
    }
}
