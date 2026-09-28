using System;
using System.IO;
using System.Text;
using System.Web.Script.Serialization;

namespace WeGameShoutTool
{
    public sealed class RuntimeLogger
    {
        private readonly object syncRoot = new object();
        private readonly string path;
        private readonly string sessionId;

        public RuntimeLogger(string logPath)
        {
            if (string.IsNullOrWhiteSpace(logPath))
            {
                throw new ArgumentException("日志路径不能为空。", "logPath");
            }
            path = Path.GetFullPath(logPath);
            sessionId = Guid.NewGuid().ToString("N").Substring(0, 8);
        }

        public Action<string> FailureSink { get; set; }

        public string LogPath
        {
            get { return path; }
        }

        public string SessionId
        {
            get { return sessionId; }
        }

        public void Write(string level, string message)
        {
            string safeLevel = string.IsNullOrWhiteSpace(level) ? "信息" : level.Trim();
            string safeMessage = (message ?? string.Empty).Replace("\r", " ").Replace("\n", " ").Trim();
            string line = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff")
                + " [" + safeLevel + "] " + safeMessage
                + " [会话:" + sessionId + "] [线程:" + System.Threading.Thread.CurrentThread.ManagedThreadId + "]"
                + Environment.NewLine;
            try
            {
                lock (syncRoot)
                {
                    string directory = Path.GetDirectoryName(Path.GetFullPath(path));
                    Directory.CreateDirectory(directory);
                    File.AppendAllText(path, line, new UTF8Encoding(false));
                }
            }
            catch (Exception ex)
            {
                Action<string> sink = FailureSink;
                if (sink != null)
                {
                    sink("运行日志写入失败：" + ex.Message);
                }
            }
        }

        public void WriteException(string operation, Exception exception)
        {
            Write("异常", (operation ?? "未命名操作") + " | " + FormatException(exception));
        }

        private static string FormatException(Exception exception)
        {
            if (exception == null)
            {
                return "异常对象为空";
            }
            StringBuilder builder = new StringBuilder();
            int depth = 0;
            Exception current = exception;
            while (current != null)
            {
                if (depth > 0)
                {
                    builder.Append(" | 内部异常 ");
                }
                builder.Append(current.GetType().FullName);
                builder.Append(": ");
                builder.Append(current.Message);
                if (!string.IsNullOrWhiteSpace(current.StackTrace))
                {
                    builder.Append(" | 堆栈 ");
                    builder.Append(current.StackTrace);
                }
                current = current.InnerException;
                depth++;
            }
            return builder.ToString();
        }
    }

    public sealed class ToolState
    {
        public int ThemeIndex;
        public int PackIndex;
        public string TemplateSource;
    }

    public static class ToolStateStore
    {
        public static string ResolveTemplateSource(ToolState state, bool hasCustomLibrary)
        {
            if (state != null && string.Equals(state.TemplateSource, "builtin", StringComparison.OrdinalIgnoreCase))
            {
                return "builtin";
            }
            if (hasCustomLibrary && state != null && string.Equals(state.TemplateSource, "custom", StringComparison.OrdinalIgnoreCase))
            {
                return "custom";
            }
            return hasCustomLibrary ? "custom" : "builtin";
        }

        public static ToolState Load(string path)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
                {
                    return new ToolState();
                }
                ToolState state = new JavaScriptSerializer().Deserialize<ToolState>(File.ReadAllText(path, Encoding.UTF8));
                if (state == null || state.ThemeIndex < 0 || state.PackIndex < 0)
                {
                    return new ToolState();
                }
                return state;
            }
            catch
            {
                return new ToolState();
            }
        }

        public static void Save(string path, ToolState state)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                throw new ArgumentException("状态路径不能为空。", "path");
            }
            if (state == null)
            {
                throw new ArgumentNullException("state");
            }
            string directory = Path.GetDirectoryName(Path.GetFullPath(path));
            Directory.CreateDirectory(directory);
            string json = new JavaScriptSerializer().Serialize(state);
            File.WriteAllText(path, json, new UTF8Encoding(false));
        }
    }
}
