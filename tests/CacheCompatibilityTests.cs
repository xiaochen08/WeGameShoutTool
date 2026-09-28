using System;
using System.IO;
using System.IO.Compression;
using System.Reflection;
using System.Text;

internal static class CacheCompatibilityTests
{
    public static int Main(string[] args)
    {
        if (args.Length != 3)
        {
            Console.WriteLine("FAIL expected tool, cache and 7-Zip paths");
            return 1;
        }

        string toolPath = args[0];
        string cachePath = args[1];
        string sevenZipPath = args[2];
        string source;
        using (FileStream file = File.OpenRead(cachePath))
        using (GZipStream gzip = new GZipStream(file, CompressionMode.Decompress))
        using (StreamReader reader = new StreamReader(gzip, Encoding.UTF8, true))
        {
            source = reader.ReadToEnd();
        }

        Assembly assembly = Assembly.LoadFrom(toolPath);
        Type patcher = assembly.GetType("WeGameShoutTool.Patcher", true);
        MethodInfo patch = patcher.GetMethod("PatchSourceForTesting", BindingFlags.Public | BindingFlags.Static);
        string patched = (string)patch.Invoke(null, new object[] { source });
        if (!patched.Contains("__WG_SHOUT_FORCE_V1__") || !patched.Contains("/sync-result?code="))
        {
            Console.WriteLine("FAIL current cache did not upgrade to strong override");
            return 1;
        }

        MethodInfo compress = patcher.GetMethod("BuildExactLengthGzip", BindingFlags.NonPublic | BindingFlags.Static);
        string runDirectory = Path.Combine(Path.GetTempPath(), "WeGameShoutTool-compat-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(runDirectory);
        Action<string> report = delegate(string message) { Console.WriteLine(message); };
        string candidate = (string)compress.Invoke(null, new object[]
        {
            patched,
            new FileInfo(cachePath).Length,
            sevenZipPath,
            runDirectory,
            report
        });
        long targetLength = new FileInfo(cachePath).Length;
        long candidateLength = new FileInfo(candidate).Length;
        Console.WriteLine("TargetLength=" + targetLength);
        Console.WriteLine("CandidateLength=" + candidateLength);
        string candidateSource;
        using (FileStream file = File.OpenRead(candidate))
        using (GZipStream gzip = new GZipStream(file, CompressionMode.Decompress))
        using (StreamReader reader = new StreamReader(gzip, Encoding.UTF8, true))
        {
            candidateSource = reader.ReadToEnd();
        }
        bool complete = candidateSource.Contains("__WG_SHOUT_PAYLOAD_V4__")
            && candidateSource.Contains("if(same){r(-2);globalThis.__WG_SHOUT_FORCE_V1__=1;return}")
            && candidateSource.Contains("Gt.decode");
        bool idempotent = (string)patch.Invoke(null, new object[] { candidateSource }) == candidateSource;
        Console.WriteLine(candidateLength == targetLength ? "PASS exact-length strong override candidate" : "FAIL length mismatch");
        Console.WriteLine(complete ? "PASS compressed candidate integrity" : "FAIL compressed candidate integrity");
        Console.WriteLine(idempotent ? "PASS cached V4 patch remains idempotent" : "FAIL cached V4 patch changed on reapply");
        return candidateLength == targetLength && complete && idempotent ? 0 : 1;
    }
}
