using System;
using System.Diagnostics;
using System.Security.Principal;
using System.Text;

namespace WeGameShoutTool
{
    public static class PrivilegeManager
    {
        public static bool IsAdministrator()
        {
            using (WindowsIdentity identity = WindowsIdentity.GetCurrent())
            {
                WindowsPrincipal principal = new WindowsPrincipal(identity);
                return principal.IsInRole(WindowsBuiltInRole.Administrator);
            }
        }

        public static bool ShouldShowRelaunchButton(bool isAdministrator)
        {
            return !isAdministrator;
        }

        public static bool EnsureAdministrator()
        {
            if (IsAdministrator())
            {
                return true;
            }

            string[] commandLine = Environment.GetCommandLineArgs();
            string[] arguments = new string[Math.Max(0, commandLine.Length - 1)];
            if (arguments.Length > 0)
            {
                Array.Copy(commandLine, 1, arguments, 0, arguments.Length);
            }
            Process.Start(BuildRelaunchInfoForTesting(ApplicationExecutablePath(), arguments));
            return false;
        }

        public static ProcessStartInfo BuildRelaunchInfoForTesting(string executablePath, string[] arguments)
        {
            if (string.IsNullOrWhiteSpace(executablePath))
            {
                throw new ArgumentException("程序路径不能为空。", "executablePath");
            }
            StringBuilder joinedArguments = new StringBuilder();
            if (arguments != null)
            {
                foreach (string argument in arguments)
                {
                    if (joinedArguments.Length > 0)
                    {
                        joinedArguments.Append(' ');
                    }
                    joinedArguments.Append(QuoteArgument(argument ?? string.Empty));
                }
            }
            return new ProcessStartInfo
            {
                FileName = executablePath,
                Arguments = joinedArguments.ToString(),
                WorkingDirectory = AppDomain.CurrentDomain.BaseDirectory,
                UseShellExecute = true,
                Verb = "runas"
            };
        }

        private static string ApplicationExecutablePath()
        {
            return Process.GetCurrentProcess().MainModule.FileName;
        }

        private static string QuoteArgument(string value)
        {
            return "\"" + value.Replace("\"", "\\\"") + "\"";
        }
    }
}
