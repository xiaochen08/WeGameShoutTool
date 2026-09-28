using System;
using System.IO;
using System.Reflection;
using System.Net;
using System.Text;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO.Compression;
using System.Web.Script.Serialization;
using System.Drawing;
using System.Windows.Forms;

internal static class SourcePatchTests
{
    private static int failures;

    private static void Check(bool condition, string name)
    {
        if (condition)
        {
            Console.WriteLine("PASS " + name);
            return;
        }

        failures++;
        Console.WriteLine("FAIL " + name);
    }

    private static string OriginalSource()
    {
        return "import{j as je,k as Kt,l as zt,m as Gt,n as ft}from\"./_plugin-vue_export-helper.D4x_7wIb.js\";"
            + "const S=()=>{const a=je(()=>Kt(),[]),b=je(()=>zt(),null,{});return[a,b]},Ln=!1;"
            + "function setup(){const A={value:[]},e=p=>!0,c={value:{title:'x',key:1,contents:['a']}},qe={info:x=>x};"
            + "const I=()=>{const p={},x={title:c.value.title,key:c.value.key,...c.value.contents.reduce((W,ee,we)=>({...W,[we]:ee}),p)};ft(x)};return[e,I]}"
            + "ct(()=>{no(\"setting\"),U(),o()});";
    }

    private static string CurrentWeGameSource()
    {
        return OriginalSource()
            .Replace("c.value", "d.value")
            .Replace("ct(()=>{no(\"setting\"),U(),o()});", "dt(()=>{no(\"setting\"),U(),o()});");
    }


    private static string InvokePatch(MethodInfo method, string source)
    {
        return (string)method.Invoke(null, new object[] { source });
    }

    private static bool ThrowsInvocation(MethodInfo method, object[] arguments)
    {
        try
        {
            method.Invoke(null, arguments);
            return false;
        }
        catch (TargetInvocationException)
        {
            return true;
        }
    }

    private static void WriteGzipCacheFile(string path, string source)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path));
        using (FileStream file = File.Create(path))
        using (GZipStream gzip = new GZipStream(file, CompressionMode.Compress))
        using (StreamWriter writer = new StreamWriter(gzip, new UTF8Encoding(false)))
        {
            writer.Write(source);
        }
    }

    public static int Main(string[] args)
    {
        string toolPath = args.Length > 0
            ? args[0]
            : Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "dist", "WeGame喊话自定义工具.exe"));

        if (!File.Exists(toolPath))
        {
            Console.WriteLine("FAIL tool executable missing: " + toolPath);
            return 1;
        }

        Assembly assembly = Assembly.LoadFrom(toolPath);
        Check(assembly.GetName().Version.ToString() == "6.22.0.0", "executable assembly version matches tool version");
        Check(FileVersionInfo.GetVersionInfo(toolPath).FileVersion == "6.22.0.0", "Windows file version matches tool version");
        Type patcher = assembly.GetType("WeGameShoutTool.Patcher", false);
        Check(patcher != null, "public patch core exists");
        if (patcher == null)
        {
            return 1;
        }
        FieldInfo noSaveResultCode = patcher.GetField("NoSaveNeededResultCode", BindingFlags.Public | BindingFlags.Static);
        Check(noSaveResultCode != null && (int)noSaveResultCode.GetValue(null) == -2, "no-save result code is explicit");

        MethodInfo method = patcher.GetMethod("PatchSourceForTesting", BindingFlags.Public | BindingFlags.Static);
        Check(method != null, "testable patch entry exists");
        if (method == null)
        {
            return 1;
        }

        string patched = null;
        try
        {
            patched = InvokePatch(method, OriginalSource());
        }
        catch (TargetInvocationException ex)
        {
            failures++;
            Exception inner = ex.InnerException;
            Console.WriteLine("FAIL compatible source patch threw: " + (inner == null ? ex.GetType().FullName : inner.GetType().FullName + " - " + inner.Message));
        }
        if (patched == null)
        {
            return 1;
        }
        Check(patched.Contains("Ln=!0"), "custom button enabled");
        Check(patched.Contains("e=p=>A.value[p]"), "input mode enabled");
        Check(patched.Contains("g as WgAuth"), "login initializer imported");
        Check(patched.Contains("je(async()=>{await WgAuth();return zt()}"), "login initialized before read");
        Check(patched.Contains("await WgAuth();const W=await ft(x)"), "login initialized before save");
        Check(patched.Contains("喊话已保存"), "success feedback included");
        Check(patched.Contains("保存失败，错误码："), "error code feedback included");
        Check(patched.Contains("http://127.0.0.1:27777/phrases"), "local tool endpoint injected");
        Check(patched.Contains("r(X);") && patched.Contains("/sync-result?code="), "automatic sync result is reported to the local tool");
        Check(patched.Contains("__WG_SHOUT_FORCE_V1__") && patched.Contains("setInterval"), "strong override repeats during the WeGame session");
        Check(patched.Contains("/sync-result?code="), "strong override reports the authenticated save result");
        Check(patched.Contains("__WG_SHOUT_PAYLOAD_V4__") && !patched.Contains("__WG_SHOUT_PAYLOAD_V3__"), "payload compatibility marker upgraded to V4");
        Check(patched.Contains("x={...q,...w,title:q.title||w.title,key:q.key||w.key}"), "sync preserves the current WeGame scheme fields");
        Check(patched.Contains("if(globalThis.__wgstBusy)return") && patched.Contains("finally{globalThis.__wgstBusy=0}") && patched.Contains("setInterval(s,10000)"), "strong override prevents overlapping sync while retaining polling");
        Check(patched.Contains("same=[0,1,2,3,4,5,6,7,8,9].every") && patched.Contains("if(same){r(-2);globalThis.__WG_SHOUT_FORCE_V1__=1;return}"), "identical phrases skip WeGame save");
        Check(patched.Contains("__WG_SHOUT_PAYLOAD_V4__") && !patched.Contains("__WG_SHOUT_PAYLOAD_V3__"), "payload compatibility marker upgrades to V4");
        Check(patched.Contains("Gt.decode") && patched.Contains("shout_message"), "sync extracts the current announce object before saving");
        Check(!patched.Contains("sync-result?code=?code="), "sync result callback uses a valid query string");
        string currentPatched = null;
        try
        {
            currentPatched = InvokePatch(method, CurrentWeGameSource() + "shout_message __name:\"others\" 一键喊话");
        }
        catch (TargetInvocationException ex)
        {
            failures++;
            Exception inner = ex.InnerException;
            Console.WriteLine("FAIL current WeGame source patch threw: " + (inner == null ? ex.GetType().FullName : inner.GetType().FullName + " - " + inner.Message));
        }
        Check(currentPatched != null && currentPatched.Contains("d.value.title") && currentPatched.Contains("dt(()=>{const s=async()=>"), "current WeGame source with renamed save state is patched");
        Check(currentPatched != null && currentPatched.Contains("if(globalThis.__wgstBusy)return") && currentPatched.Contains("if(same){r(-2);globalThis.__WG_SHOUT_FORCE_V1__=1;return}"), "current page uses the same save guard and no-op result");
        MethodInfo isSettingPage = patcher.GetMethod("IsSettingPageSourceForTesting", BindingFlags.Public | BindingFlags.Static);
        Check(isSettingPage != null, "setting page fingerprint entry exists");
        if (isSettingPage != null)
        {
            Check((bool)isSettingPage.Invoke(null, new object[] { OriginalSource() + "shout_message __name:\"others\" 一键喊话" }), "setting page fingerprint accepts current page");
            Check(!(bool)isSettingPage.Invoke(null, new object[] { OriginalSource() }), "setting page fingerprint rejects unrelated page");
        }
        MethodInfo buildNoCompatibleCacheReport = patcher.GetMethod("BuildNoCompatibleCacheReportForTesting", BindingFlags.Public | BindingFlags.Static);
        Check(buildNoCompatibleCacheReport != null, "cache failure report builder exists");
        if (buildNoCompatibleCacheReport != null)
        {
            string missingPageReport = (string)buildNoCompatibleCacheReport.Invoke(null, new object[] { 2, 8, 0, 0, 1 });
            Check(missingPageReport.Contains("本次没有修改 WeGame 缓存") && missingPageReport.Contains("辅助设置页面") && missingPageReport.Contains("缓存文件仍被占用"), "cache failure report explains missing page and occupied files");
            string changedPageReport = (string)buildNoCompatibleCacheReport.Invoke(null, new object[] { 2, 8, 1, 0, 0 });
            Check(changedPageReport.Contains("已识别到喊话设置页面") && changedPageReport.Contains("保存接口结构已变化") && changedPageReport.Contains("不要反复强制应用"), "cache failure report explains likely page update safely");
        }
        MethodInfo selectCurrentCacheCandidate = patcher.GetMethod("SelectCurrentCacheCandidateIndexForTesting", BindingFlags.Public | BindingFlags.Static);
        Check(selectCurrentCacheCandidate != null, "current cache candidate selector exists");
        if (selectCurrentCacheCandidate != null)
        {
            DateTime oldCacheWrite = new DateTime(2026, 9, 15, 18, 40, 22, DateTimeKind.Utc);
            DateTime newCacheWrite = new DateTime(2026, 9, 15, 18, 42, 11, DateTimeKind.Utc);
            int staleOnlySelection = (int)selectCurrentCacheCandidate.Invoke(null, new object[]
            {
                new DateTime[] { oldCacheWrite },
                newCacheWrite
            });
            Check(staleOnlySelection == -1, "newer shout settings page never falls back to an older compatible cache");

            int currentSelection = (int)selectCurrentCacheCandidate.Invoke(null, new object[]
            {
                new DateTime[] { oldCacheWrite.AddMinutes(-3), newCacheWrite.AddSeconds(2) },
                newCacheWrite
            });
            Check(currentSelection == 1, "newest recognized shout settings page is selected");
        }
        MethodInfo findNewestSettingPageWriteTime = patcher.GetMethod("FindNewestSettingPageWriteTimeForTesting", BindingFlags.Public | BindingFlags.Static);
        Check(findNewestSettingPageWriteTime != null, "setting page freshness scan is testable");
        if (findNewestSettingPageWriteTime != null)
        {
            string cacheRoot = Path.Combine(Path.GetTempPath(), "WeGameShoutTool-cache-scope-" + Guid.NewGuid().ToString("N"));
            string settingCacheData = Path.Combine(cacheRoot, "qbcore109", "cache", "qbcore_cache_browser.exe_130005", "Cache", "Cache_Data");
            string unrelatedCacheData = Path.Combine(cacheRoot, "qbcore109", "cache", "qbcore_cache_browser.exe_500000", "Cache", "Cache_Data");
            string settingPageFile = Path.Combine(settingCacheData, "f_000091");
            DateTime settingPageWriteTime = new DateTime(2026, 9, 23, 20, 41, 0, DateTimeKind.Utc);
            DateTime unrelatedCacheWriteTime = settingPageWriteTime.AddMinutes(2);
            StringBuilder pageSource = new StringBuilder(OriginalSource());
            pageSource.Append(" shout_message __name:\"others\" 一键喊话 ");
            for (int i = 0; i < 80; i++)
            {
                pageSource.Append(Guid.NewGuid().ToString("N"));
            }
            WriteGzipCacheFile(settingPageFile, pageSource.ToString());
            File.SetLastWriteTimeUtc(settingPageFile, settingPageWriteTime);
            Directory.SetLastWriteTimeUtc(settingCacheData, settingPageWriteTime);
            Directory.CreateDirectory(unrelatedCacheData);
            File.WriteAllText(Path.Combine(unrelatedCacheData, "data_0"), "unrelated browser cache activity", new UTF8Encoding(false));
            Directory.SetLastWriteTimeUtc(unrelatedCacheData, unrelatedCacheWriteTime);
            DateTime newestSettingPageWriteTime = (DateTime)findNewestSettingPageWriteTime.Invoke(null, new object[] { cacheRoot });
            Check(newestSettingPageWriteTime == settingPageWriteTime, "unrelated newer browser cache does not invalidate the current shout settings page");
        }
        Check(!patched.Contains("Ln=!1"), "old custom flag removed");
        Check(!patched.Contains("e=p=>!0"), "old select-only mode removed");
        Check(!patched.Contains(";ft(x)}"), "fire-and-forget save removed");


        string patchedAgain = InvokePatch(method, patched);
        Check(patchedAgain == patched, "already patched source remains unchanged");

        string oldToolPath = Path.Combine(Path.GetDirectoryName(toolPath), "WeGame喊话自定义工具.exe");
        if (!File.Exists(oldToolPath) || string.Equals(Path.GetFullPath(oldToolPath), Path.GetFullPath(toolPath), StringComparison.OrdinalIgnoreCase))
        {
            oldToolPath = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(toolPath), "..", "dist", "WeGame喊话自定义工具-v6.16-candidate.exe"));
        }
        if (File.Exists(oldToolPath) && !string.Equals(Path.GetFullPath(oldToolPath), Path.GetFullPath(toolPath), StringComparison.OrdinalIgnoreCase))
        {
            Assembly oldAssembly = Assembly.LoadFrom(oldToolPath);
            Type oldPatcher = oldAssembly.GetType("WeGameShoutTool.Patcher", false);
            MethodInfo oldPatchMethod = oldPatcher == null ? null : oldPatcher.GetMethod("PatchSourceForTesting", BindingFlags.Public | BindingFlags.Static);
            if (oldPatchMethod != null)
            {
                string oldPatched = InvokePatch(oldPatchMethod, OriginalSource());
                string upgraded = InvokePatch(method, oldPatched);
                Check(oldPatched.Contains("__WG_SHOUT_FORCE_V1__") && !oldPatched.Contains("__WG_SHOUT_PAYLOAD_V2__"), "V6.16 fixture is recognized as legacy payload");
                Check(upgraded.Contains("__WG_SHOUT_PAYLOAD_V4__") && upgraded.Contains("x={...q,...w,title:q.title||w.title,key:q.key||w.key}"), "V6.16 patch upgrades to the current payload format");
                Check(InvokePatch(method, upgraded) == upgraded, "upgraded payload remains idempotent");
            }
            else
            {
                Check(false, "V6.16 patch fixture entry exists");
            }
        }
        else
        {
            Check(false, "V6.16 patch fixture executable is available");
        }

        string v617ToolPath = Path.Combine(Path.GetDirectoryName(toolPath), "WeGame喊话自定义工具-v6.17.exe");
        if (!File.Exists(v617ToolPath))
        {
            v617ToolPath = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(toolPath), "..", "dist", "WeGame喊话自定义工具-v6.17.exe"));
        }
        if (File.Exists(v617ToolPath) && !string.Equals(Path.GetFullPath(v617ToolPath), Path.GetFullPath(toolPath), StringComparison.OrdinalIgnoreCase))
        {
            Assembly v617Assembly = Assembly.LoadFrom(v617ToolPath);
            Type v617Patcher = v617Assembly.GetType("WeGameShoutTool.Patcher", false);
            MethodInfo v617Patch = v617Patcher == null ? null : v617Patcher.GetMethod("PatchSourceForTesting", BindingFlags.Public | BindingFlags.Static);
            if (v617Patch != null)
            {
                string v617Legacy = InvokePatch(v617Patch, OriginalSource() + "shout_message __name:\"others\" 一键喊话");
                string v617Current = InvokePatch(v617Patch, CurrentWeGameSource() + "shout_message __name:\"others\" 一键喊话");
                Check(v617Legacy.Contains("__WG_SHOUT_PAYLOAD_V2__") && v617Current.Contains("__WG_SHOUT_PAYLOAD_V2__"), "V6.17 fixtures use the V2 payload marker");
                string upgradedLegacy = InvokePatch(method, v617Legacy);
                string upgradedCurrent = InvokePatch(method, v617Current);
                Check(upgradedLegacy.Contains("__WG_SHOUT_PAYLOAD_V4__") && !upgradedLegacy.Contains("__WG_SHOUT_PAYLOAD_V3__") && !upgradedLegacy.Contains("__WG_SHOUT_PAYLOAD_V2__"), "V6.17 legacy mount upgrades to V4");
                Check(upgradedCurrent.Contains("__WG_SHOUT_PAYLOAD_V4__") && !upgradedCurrent.Contains("__WG_SHOUT_PAYLOAD_V3__") && !upgradedCurrent.Contains("__WG_SHOUT_PAYLOAD_V2__"), "V6.17 current mount upgrades to V4");
                Check(InvokePatch(method, upgradedLegacy) == upgradedLegacy && InvokePatch(method, upgradedCurrent) == upgradedCurrent, "V6.17 upgrades remain idempotent");
            }
            else
            {
                Check(false, "V6.17 patch fixture entry exists");
            }
        }
        else
        {
            Check(false, "V6.17 patch fixture executable is available");
        }

        bool incompatibleRejected = false;
        try
        {
            InvokePatch(method, "console.log('different WeGame version')");
        }
        catch (TargetInvocationException ex)
        {
            incompatibleRejected = ex.InnerException is InvalidOperationException;
        }
        Check(incompatibleRejected, "incompatible source rejected");

        MethodInfo validate = patcher.GetMethod("ValidatePhrasesForTesting", BindingFlags.Public | BindingFlags.Static);
        Check(validate != null, "phrase validation entry exists");
        if (validate != null)
        {
            string[] valid = new string[] { "1", "2", "3", "4", "5", "6", "7", "8", "9", "10" };
            string[] shortList = new string[] { "1", "2" };
            string[] acceptedLimitLine = new string[] { new string('a', 150), "2", "3", "4", "5", "6", "7", "8", "9", "10" };
            string[] longLine = new string[] { new string('a', 151), "2", "3", "4", "5", "6", "7", "8", "9", "10" };
            string[] commandOnly = new string[] { "/all", "2", "3", "4", "5", "6", "7", "8", "9", "10" };
            Check(validate.Invoke(null, new object[] { valid }) == null, "ten valid phrases accepted");
            Check(validate.Invoke(null, new object[] { shortList }) != null, "non-ten phrase list rejected");
            Check(validate.Invoke(null, new object[] { acceptedLimitLine }) == null, "150-character phrase accepted");
            Check(validate.Invoke(null, new object[] { longLine }) != null, "overlong phrase rejected");
            Check(validate.Invoke(null, new object[] { commandOnly }) != null, "all command without message rejected");
        }

        FieldInfo weGameMaxLength = patcher.GetField("WeGameMaxPhraseLength", BindingFlags.Public | BindingFlags.Static);
        Check(weGameMaxLength != null && (int)weGameMaxLength.GetValue(null) == 50, "WeGame save limit matches the current WeGame page");
        MethodInfo validateForWeGame = patcher.GetMethod("ValidatePhrasesForWeGameForTesting", BindingFlags.Public | BindingFlags.Static);
        Check(validateForWeGame != null, "WeGame save validation entry exists");
        if (validateForWeGame != null)
        {
            string[] acceptedWeGameLine = new string[] { new string('a', 50), "2", "3", "4", "5", "6", "7", "8", "9", "10" };
            string[] rejectedWeGameLine = new string[] { new string('a', 51), "2", "3", "4", "5", "6", "7", "8", "9", "10" };
            Check(validateForWeGame.Invoke(null, new object[] { acceptedWeGameLine }) == null, "50-character phrase accepted by WeGame validation");
            Check(validateForWeGame.Invoke(null, new object[] { rejectedWeGameLine }) != null, "51-character phrase rejected before WeGame save");
        }

        MethodInfo buildJson = patcher.GetMethod("BuildPhrasesJson", BindingFlags.Public | BindingFlags.Static);
        string[] jsonPhrases = new string[] { "a\"b", "2", "3", "4", "5", "6", "7", "8", "9", "10" };
        string json = (string)buildJson.Invoke(null, new object[] { jsonPhrases });
        Check(json.Contains("\"0\":\"a\\\"b\""), "phrase JSON escapes quotes");

        Type serverType = assembly.GetType("WeGameShoutTool.PhraseServer", false);
        Check(serverType != null, "local phrase server exists");
        object server = null;
        if (serverType != null)
        {
            Func<string> provider = delegate { return json; };
            server = Activator.CreateInstance(serverType, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null, new object[] { 27778, provider }, null);
            MethodInfo start = serverType.GetMethod("Start", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            MethodInfo stop = serverType.GetMethod("Stop", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            PropertyInfo phraseRequestCount = serverType.GetProperty("PhraseRequestCount", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            PropertyInfo lastSyncResultCode = serverType.GetProperty("LastSyncResultCode", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            Check(phraseRequestCount != null && lastSyncResultCode != null, "local server exposes strong override verification state");
            try
            {
                start.Invoke(server, null);
                HttpWebRequest request = (HttpWebRequest)WebRequest.Create("http://127.0.0.1:27778/phrases");
                request.Timeout = 3000;
                using (HttpWebResponse response = (HttpWebResponse)request.GetResponse())
                using (StreamReader reader = new StreamReader(response.GetResponseStream()))
                {
                    Check(response.StatusCode == HttpStatusCode.OK, "local phrase server returns OK");
                    Check(response.Headers["Access-Control-Allow-Origin"] == "https://wegame.gtimg.com", "local phrase server returns WeGame CORS header");
                    Check(reader.ReadToEnd() == json, "local phrase server returns current phrases");
                }
                if (phraseRequestCount != null)
                {
                    Check((int)phraseRequestCount.GetValue(server, null) == 1, "local server records WeGame phrase reads");
                }
                if (lastSyncResultCode != null)
                {
                    HttpWebRequest resultRequest = (HttpWebRequest)WebRequest.Create("http://127.0.0.1:27778/sync-result?code=0");
                    resultRequest.Timeout = 3000;
                    using (HttpWebResponse resultResponse = (HttpWebResponse)resultRequest.GetResponse())
                    {
                        Check(resultResponse.StatusCode == HttpStatusCode.NoContent, "local server accepts WeGame save result");
                    }
                    Check((int)lastSyncResultCode.GetValue(server, null) == 0, "local server records authenticated save success");
                    HttpWebRequest noSaveRequest = (HttpWebRequest)WebRequest.Create("http://127.0.0.1:27778/sync-result?code=-2");
                    noSaveRequest.Timeout = 3000;
                    using (HttpWebResponse noSaveResponse = (HttpWebResponse)noSaveRequest.GetResponse())
                    {
                        Check(noSaveResponse.StatusCode == HttpStatusCode.NoContent, "local server accepts no-save result");
                    }
                    Check((int)lastSyncResultCode.GetValue(server, null) == -2, "local server records no-save result");
                }
            }
            finally
            {
                if (server != null)
                {
                    stop.Invoke(server, null);
                }
            }
        }

        MethodInfo reorder = patcher.GetMethod("ReorderPhrasesForTesting", BindingFlags.Public | BindingFlags.Static);
        Check(reorder != null, "phrase reorder entry exists");
        if (reorder != null)
        {
            string[] original = new string[] { "1", "2", "3", "4" };
            string[] movedDown = (string[])reorder.Invoke(null, new object[] { original, 0, 2 });
            Check(string.Join(",", movedDown) == "2,3,1,4", "dragging down preserves relative order");
            Check(string.Join(",", original) == "1,2,3,4", "reorder does not mutate source array");
            string[] movedUp = (string[])reorder.Invoke(null, new object[] { original, 3, 1 });
            Check(string.Join(",", movedUp) == "1,4,2,3", "dragging up preserves relative order");
        }

        MethodInfo saveFile = patcher.GetMethod("SavePhrasesFileForTesting", BindingFlags.Public | BindingFlags.Static);
        Check(saveFile != null, "immediate phrase persistence entry exists");
        if (reorder != null && saveFile != null)
        {
            string[] phrases = new string[] { "1", "2", "3", "4", "5", "6", "7", "8", "9", "10" };
            string[] reordered = (string[])reorder.Invoke(null, new object[] { phrases, 0, 9 });
            string savePath = Path.Combine(Path.GetTempPath(), "WeGameShoutTool-test-" + Guid.NewGuid().ToString("N") + ".txt");
            saveFile.Invoke(null, new object[] { savePath, reordered });
            string[] saved = File.ReadAllLines(savePath);
            Check(saved.Length == 10 && saved[0] == "2" && saved[9] == "1", "reordered phrases are persisted immediately");
        }

        MethodInfo audience = patcher.GetMethod("ApplyAudienceForTesting", BindingFlags.Public | BindingFlags.Static);
        Check(audience != null, "audience prefix entry exists");
        if (audience != null)
        {
            Check((string)audience.Invoke(null, new object[] { "你好", true }) == "/all 你好", "all audience adds prefix");
            Check((string)audience.Invoke(null, new object[] { "/all 你好", true }) == "/all 你好", "existing all prefix is not duplicated");
            Check((string)audience.Invoke(null, new object[] { "/ALL 你好", true }) == "/ALL 你好", "all prefix detection is case insensitive");
            Check((string)audience.Invoke(null, new object[] { "/all 你好", false }) == "你好", "team audience removes leading all prefix");
            Check((string)audience.Invoke(null, new object[] { "你好 /all", true }) == "/all 你好 /all", "non-leading all text still receives command prefix");
        }

        MethodInfo audienceAll = patcher.GetMethod("ApplyAudienceToAllForTesting", BindingFlags.Public | BindingFlags.Static);
        Check(audienceAll != null, "atomic global audience entry exists");
        if (audienceAll != null)
        {
            string[] original = new string[] { "一", "二", "三", "四", "五", "六", "七", "八", "九", "十" };
            string[] originalCopy = (string[])original.Clone();
            string[] allRows = (string[])audienceAll.Invoke(null, new object[] { original, true });
            bool everyAll = true;
            for (int i = 0; i < allRows.Length; i++)
            {
                everyAll = everyAll && allRows[i].StartsWith("/all ", StringComparison.OrdinalIgnoreCase);
            }
            Check(everyAll, "global all applies every row");
            Check(string.Join("|", original) == string.Join("|", originalCopy), "global audience does not mutate source");
            string[] tooLong = (string[])original.Clone();
            tooLong[4] = new string('长', 146);
            bool atomicRejected = false;
            try
            {
                audienceAll.Invoke(null, new object[] { tooLong, true });
            }
            catch (TargetInvocationException ex)
            {
                atomicRejected = ex.InnerException is InvalidOperationException;
            }
            Check(atomicRejected, "global all rejects overlong batch atomically");
            Check((string)audience.Invoke(null, new object[] { new string('长', 145), true }) == "/all " + new string('长', 145), "all audience accepts 145-character body");
            bool audienceLimitRejected = false;
            try
            {
                audience.Invoke(null, new object[] { new string('长', 146), true });
            }
            catch (TargetInvocationException ex)
            {
                audienceLimitRejected = ex.InnerException is InvalidOperationException;
            }
            Check(audienceLimitRejected, "all audience rejects body over 145 characters");
        }

        MethodInfo phrasePath = patcher.GetMethod("GetLocalPhraseFileForTesting", BindingFlags.Public | BindingFlags.Static);
        Check(phrasePath != null, "documents cache path entry exists");
        if (phrasePath != null)
        {
            string documents = Path.Combine(Path.GetTempPath(), "Documents-test");
            string result = (string)phrasePath.Invoke(null, new object[] { documents });
            Check(result == Path.Combine(documents, "WeGame喊话自定义工具", "喊话内容.txt"), "phrases cache under Documents");
        }

        Type safetyType = assembly.GetType("WeGameShoutTool.SafetyTextPolicy", false);
        Check(safetyType != null, "safety policy exists");
        if (safetyType != null)
        {
            MethodInfo normalize = safetyType.GetMethod("NormalizeForTesting", BindingFlags.Public | BindingFlags.Static);
            MethodInfo checkText = safetyType.GetMethod("CheckTextForTesting", BindingFlags.Public | BindingFlags.Static);
            MethodInfo checkAll = safetyType.GetMethod("CheckAllForTesting", BindingFlags.Public | BindingFlags.Static);
            Check(normalize != null && checkText != null && checkAll != null, "safety test entries exist");
            if (normalize != null && checkText != null && checkAll != null)
            {
                string safe = Encoding.UTF8.GetString(Convert.FromBase64String("6L+Z5rOi5pyJ5oOz5rOV"));
                string directRisk = Encoding.UTF8.GetString(Convert.FromBase64String("5Y675q27"));
                string pinyinRisk = Encoding.UTF8.GetString(Convert.FromBase64String("cXVzaQ=="));
                string homophoneRisk = Encoding.UTF8.GetString(Convert.FromBase64String("5puy5Zub"));
                Check((string)normalize.Invoke(null, new object[] { "Ａ b １" }) == "abi", "safety normalization folds width case spaces and substitutions");
                Check(checkText.Invoke(null, new object[] { safe }) == null, "safe gameplay phrase accepted");
                Check(checkText.Invoke(null, new object[] { directRisk }) != null, "direct risky wording rejected");
                Check(checkText.Invoke(null, new object[] { pinyinRisk }) != null, "pinyin variant rejected");
                Check(checkText.Invoke(null, new object[] { homophoneRisk }) != null, "homophone variant rejected");
                Check(checkText.Invoke(null, new object[] { "q u s i" }) != null, "split variant rejected");

                string[] safeBatch = new string[] { safe, "继续试试", "差一点", "再来一波", "先看河道", "守住高地", "小龙集合", "大龙有眼", "稳住兵线", "技能好了" };
                Check(checkAll.Invoke(null, new object[] { safeBatch }) == null, "safe phrase batch accepted");
                safeBatch[4] = pinyinRisk;
                Check(checkAll.Invoke(null, new object[] { safeBatch }) != null, "unsafe phrase batch rejected");
            }
        }

        Type libraryType = assembly.GetType("WeGameShoutTool.TemplateLibrary", false);
        Check(libraryType != null, "template library exists");
        Type formType = assembly.GetType("WeGameShoutTool.ShoutFormV5", false);
        Check(formType != null, "main form exists");
        if (formType != null)
        {
            Check(formType.GetField("templateSourceBox", BindingFlags.NonPublic | BindingFlags.Instance) != null, "main form exposes a built-in and custom library selector");
            Check(formType.GetField("importShareButton", BindingFlags.NonPublic | BindingFlags.Instance) != null && formType.GetMethod("ImportShareClick", BindingFlags.NonPublic | BindingFlags.Instance) != null, "main form has a current-phrase share import action");
            Check(formType.GetField("exportShareButton", BindingFlags.NonPublic | BindingFlags.Instance) != null && formType.GetMethod("ExportShareClick", BindingFlags.NonPublic | BindingFlags.Instance) != null, "main form has a current-phrase share export action");
            Check(formType.GetMethod("TemplateSourceChanged", BindingFlags.NonPublic | BindingFlags.Instance) != null, "main form handles library selection changes");
            MethodInfo applyTemplatePanelLayout = formType.GetMethod("ApplyTemplatePanelLayout", BindingFlags.NonPublic | BindingFlags.Static);
            FieldInfo templateDetailsExpandedByDefault = formType.GetField("TemplateDetailsExpandedByDefault", BindingFlags.NonPublic | BindingFlags.Static);
            Check(templateDetailsExpandedByDefault != null && !(bool)templateDetailsExpandedByDefault.GetRawConstantValue(), "template options are collapsed by default");
            Check(formType.GetField("templateDetailsButton", BindingFlags.NonPublic | BindingFlags.Instance) != null && applyTemplatePanelLayout != null, "template controls have a collapsible layout");
            if (applyTemplatePanelLayout != null)
            {
                Panel templatePanel = new Panel { Size = new Size(950, 84) };
                Control[] detailControls = new Control[] { new Label(), new ComboBox() };
                Button detailsButton = new Button { Location = new Point(892, 45) };
                Control[] actionControls = new Control[] { new Button(), new Button(), new Button(), new Button(), new Button(), detailsButton };
                Label safetyLabel = new Label { Location = new Point(710, 51) };
                Control[] contentControls = new Control[]
                {
                    new Label { Location = new Point(23, 316) },
                    new Panel { Location = new Point(20, 342) }
                };
                Button rowRandomButton = new Button();
                foreach (Control detailControl in detailControls) detailControl.Visible = true;
                foreach (Control actionControl in actionControls) actionControl.Visible = true;
                safetyLabel.Visible = true;
                rowRandomButton.Visible = true;
                applyTemplatePanelLayout.Invoke(null, new object[] { templatePanel, detailControls, actionControls, safetyLabel, detailsButton, contentControls, false });
                Check(templatePanel.Height == 46 && !detailControls[0].Visible && !detailControls[1].Visible, "template selector row is hidden in collapsed layout");
                bool actionsVisible = true;
                foreach (Control actionControl in actionControls) actionsVisible = actionsVisible && actionControl.Visible && actionControl.Top == 7;
                Check(actionsVisible && safetyLabel.Visible && safetyLabel.Top == 13, "share and channel actions stay visible in collapsed layout");
                Check(contentControls[0].Top == 278 && contentControls[1].Top == 304 && rowRandomButton.Visible, "editing rows move up without hiding per-line random controls");
                applyTemplatePanelLayout.Invoke(null, new object[] { templatePanel, detailControls, actionControls, safetyLabel, detailsButton, contentControls, true });
                Check(templatePanel.Height == 84 && detailControls[0].Visible && detailControls[1].Visible && contentControls[0].Top == 316 && contentControls[1].Top == 342, "template selector row expands and restores the original layout");
            }
        }
        if (libraryType != null && safetyType != null)
        {
            MethodInfo checkText = safetyType.GetMethod("CheckTextForTesting", BindingFlags.Public | BindingFlags.Static);
            MethodInfo loadLibrary = libraryType.GetMethod("Load", BindingFlags.Public | BindingFlags.Static);
            MethodInfo importAndReplace = libraryType.GetMethod("ImportAndReplace", BindingFlags.Public | BindingFlags.Static);
            Check(loadLibrary != null, "template library load entry exists");
            Check(importAndReplace != null, "template library import and replace entry exists");
            if (checkText != null && loadLibrary != null && importAndReplace != null)
            {
                Func<string, string> safetyCheck = delegate(string value)
                {
                    return (string)checkText.Invoke(null, new object[] { value });
                };
                string templatePath = Path.Combine(Path.GetDirectoryName(toolPath), "data", "templates.v5.json");
                object library = loadLibrary.Invoke(null, new object[] { templatePath, safetyCheck });
                int themeCount = (int)libraryType.GetProperty("ThemeCount").GetValue(library, null);
                int packCount = (int)libraryType.GetProperty("PackCount").GetValue(library, null);
                int phraseCount = (int)libraryType.GetProperty("PhraseCount").GetValue(library, null);
                IEnumerable allPhrases = (IEnumerable)libraryType.GetProperty("AllPhrases").GetValue(library, null);
                HashSet<string> unique = new HashSet<string>(StringComparer.Ordinal);
                bool lengthsValid = true;
                bool safetyValid = true;
                foreach (string phrase in allPhrases)
                {
                    unique.Add(phrase);
                    lengthsValid = lengthsValid && phrase.Length >= 1 && phrase.Length <= 10;
                    safetyValid = safetyValid && safetyCheck(phrase) == null;
                }
                Check(themeCount == 10, "default template library has 10 themes");
                Check(packCount == 100, "default template library has 100 packs");
                Check(phraseCount == 1000, "default template library has 1000 phrases");
                Check(unique.Count == 1000, "default template phrases globally unique");
                Check(lengthsValid, "all template phrase lengths valid");
                Check(safetyValid, "all template phrases safety valid");

                Type phraseShareType = assembly.GetType("WeGameShoutTool.PhraseShareFile", false);
                Check(phraseShareType != null, "current phrase share format exists");
                MethodInfo exportPhrases = phraseShareType == null ? null : phraseShareType.GetMethod("Export", BindingFlags.Public | BindingFlags.Static);
                MethodInfo importPhrases = phraseShareType == null ? null : phraseShareType.GetMethod("Import", BindingFlags.Public | BindingFlags.Static);
                Check(exportPhrases != null, "current displayed phrases can be exported");
                Check(importPhrases != null, "shared phrases can be imported without replacing template library");
                if (exportPhrases != null && importPhrases != null)
                {
                    string sharePath = Path.Combine(Path.GetTempPath(), "WeGameShoutTool-share-" + Guid.NewGuid().ToString("N") + ".json");
                    string[] sharedPhrases = new string[]
                    {
                        "/all 这波又空了", "  走位还行吗  ", "技能去哪了", "又差一点呢", "这就回去了",
                        "还要再试吗", "小图看了吗", "视野又没了", "团战就这样", "下波还来吗"
                    };
                    string invalidSharePath = Path.Combine(Path.GetTempPath(), "WeGameShoutTool-share-invalid-" + Guid.NewGuid().ToString("N") + ".json");
                    try
                    {
                        exportPhrases.Invoke(null, new object[] { sharePath, sharedPhrases, safetyCheck });
                        string shareJson = File.ReadAllText(sharePath, Encoding.UTF8);
                        Check(shareJson.Contains("WeGameShoutTool.PhraseShare") && shareJson.Contains("\"Version\":1"), "shared file declares a versioned format");
                        string[] importedPhrases = (string[])importPhrases.Invoke(null, new object[] { sharePath, safetyCheck });
                        Check(string.Join("|", importedPhrases) == string.Join("|", sharedPhrases), "shared phrases preserve all ten lines and audience prefix");
                        exportPhrases.Invoke(null, new object[] { sharePath, sharedPhrases, safetyCheck });
                        Check(File.ReadAllText(sharePath, Encoding.UTF8) == shareJson, "re-export safely replaces an existing share file");
                        string[] unsafePhrases = (string[])sharedPhrases.Clone();
                        unsafePhrases[2] = Encoding.UTF8.GetString(Convert.FromBase64String("5Y675q27"));
                        bool unsafeExportRejected = ThrowsInvocation(exportPhrases, new object[] { sharePath, unsafePhrases, safetyCheck });
                        Check(unsafeExportRejected && File.ReadAllText(sharePath, Encoding.UTF8) == shareJson, "unsafe export is rejected without changing an existing file");
                        string[] invalidPhrases = new string[] { new string('a', 151), "line02", "line03", "line04", "line05", "line06", "line07", "line08", "line09", "line10" };
                        File.WriteAllText(invalidSharePath, new JavaScriptSerializer().Serialize(new { Format = "WeGameShoutTool.PhraseShare", Version = 1, Phrases = invalidPhrases }), new UTF8Encoding(false));
                        bool invalidShareRejected = ThrowsInvocation(importPhrases, new object[] { invalidSharePath, safetyCheck });
                        Check(invalidShareRejected, "invalid shared phrases are rejected before editor changes");
                        Check((int)libraryType.GetProperty("PhraseCount").GetValue(library, null) == 1000, "phrase import leaves built-in library unchanged");
                    }
                    finally
                    {
                        if (File.Exists(sharePath)) File.Delete(sharePath);
                        if (File.Exists(invalidSharePath)) File.Delete(invalidSharePath);
                    }
                }

                MethodInfo getPack = libraryType.GetMethod("GetPack", BindingFlags.Public | BindingFlags.Instance);
                MethodInfo getRandomPack = libraryType.GetMethod("GetRandomPack", BindingFlags.Public | BindingFlags.Instance);
                MethodInfo getMixed = libraryType.GetMethod("GetMixedRandom", BindingFlags.Public | BindingFlags.Instance);
                MethodInfo getRandomLine = libraryType.GetMethod("GetRandomLine", BindingFlags.Public | BindingFlags.Instance);
                string[] packA = (string[])getPack.Invoke(library, new object[] { 0, 0 });
                string[] packB = (string[])getPack.Invoke(library, new object[] { 0, 0 });
                Check(string.Join("|", packA) == string.Join("|", packB), "fixed pack order deterministic");
                Check(packA[0] == "上路就这？" && packA[1] == "上路还来吗？", "fixed pack is opponent-facing game banter");
                string[] randomPack = (string[])getRandomPack.Invoke(library, new object[] { new Random(7) });
                Check(randomPack.Length == 10, "random fixed pack has ten lines");
                string[] mixed = (string[])getMixed.Invoke(library, new object[] { 10, new Random(7) });
                Check(new HashSet<string>(mixed).Count == 10, "mixed random lines unique");
                string randomLine = (string)getRandomLine.Invoke(library, new object[] { new Random(7), mixed });
                Check(Array.IndexOf(mixed, randomLine) < 0, "single random line avoids exclusions");

                string importArtifactDirectory = Path.Combine(Path.GetDirectoryName(toolPath), "test-artifacts");
                Directory.CreateDirectory(importArtifactDirectory);
                string importSource = Path.Combine(importArtifactDirectory, "template-import-source.txt");
                string importedLibraryPath = Path.Combine(importArtifactDirectory, "custom-templates.json");
                string[] importedLines = new string[]
                {
                    "这波又空了", "走位还行吗", "技能去哪了", "又差一点呢", "这就回去了",
                    "还要再试吗", "小图看了吗", "视野又没了", "团战就这样", "下波还来吗",
                    "闪现交早了", "位置没站好", "这波白来了", "龙也想拿吗", "塔下还敢来",
                    "判断挺快呢", "路线看懂没", "还在等机会", "又被看见了", "这就撤了吗"
                };
                File.WriteAllLines(importSource, importedLines, new UTF8Encoding(false));
                File.WriteAllText(importedLibraryPath, "ORIGINAL", new UTF8Encoding(false));
                object importedLibrary = importAndReplace.Invoke(null, new object[] { importSource, importedLibraryPath, safetyCheck });
                Check(File.ReadAllText(importedLibraryPath, Encoding.UTF8).Contains("\"Version\":\"6\""), "text import replaces destination with canonical json");
                Check((int)libraryType.GetProperty("PhraseCount").GetValue(importedLibrary, null) == 20, "imported template count is flexible");
                Check((int)libraryType.GetProperty("PackCount").GetValue(importedLibrary, null) == 2, "text import groups every ten lines into a pack");
                object reloadedLibrary = loadLibrary.Invoke(null, new object[] { importedLibraryPath, safetyCheck });
                Check((int)libraryType.GetProperty("PhraseCount").GetValue(reloadedLibrary, null) == 20, "imported replacement reloads after restart");

                string invalidSource = Path.Combine(importArtifactDirectory, "template-import-invalid.txt");
                File.WriteAllLines(invalidSource, new string[] { "重复内容", "重复内容", "不足十条" }, new UTF8Encoding(false));
                string beforeInvalidImport = File.ReadAllText(importedLibraryPath, Encoding.UTF8);
                bool invalidImportRejected = ThrowsInvocation(importAndReplace, new object[] { invalidSource, importedLibraryPath, safetyCheck });
                Check(invalidImportRejected, "invalid template import is rejected");
                Check(File.ReadAllText(importedLibraryPath, Encoding.UTF8) == beforeInvalidImport, "invalid import leaves current template library unchanged");
            }
        }

        Type detectorType = assembly.GetType("WeGameShoutTool.WeGameDetector", false);
        Check(detectorType != null, "WeGame detector exists");
        if (detectorType != null)
        {
            MethodInfo detectForTesting = detectorType.GetMethod("DetectForTesting", BindingFlags.Public | BindingFlags.Static);
            MethodInfo detectFromExecutable = detectorType.GetMethod("DetectFromExecutablePath", BindingFlags.Public | BindingFlags.Static);
            Check(detectForTesting != null, "WeGame detector test entry exists");
            Check(detectFromExecutable != null, "captured WeGame path can be reused after process exit");
            if (detectForTesting != null)
            {
                object status = detectForTesting.Invoke(null, new object[] { new string[] { toolPath }, new string[0] });
                Type statusType = status.GetType();
                Check((bool)statusType.GetField("IsRunning").GetValue(status), "detector reports process presence");
                Check(string.Equals((string)statusType.GetField("ExecutablePath").GetValue(status), toolPath, StringComparison.OrdinalIgnoreCase), "detector selects verified process executable");
                string detectedVersion = (string)statusType.GetField("Version").GetValue(status);
                Check(!string.IsNullOrWhiteSpace(detectedVersion), "detector returns real version or unknown");
                FieldInfo assistantVersionField = statusType.GetField("AssistantVersion");
                Check(assistantVersionField != null, "detector exposes WeGame assistant version");
            }
            if (detectFromExecutable != null)
            {
                string customInstall = Path.Combine(Path.GetDirectoryName(toolPath), "test-artifacts", "custom-wegame-install");
                string customWeGame = Path.Combine(customInstall, "wegame.exe");
                string customAssistantDirectory = Path.Combine(customInstall, "apps", "Pallas");
                string customAssistant = Path.Combine(customAssistantDirectory, "pallas.exe");
                Directory.CreateDirectory(customAssistantDirectory);
                File.Copy(toolPath, customWeGame, true);
                File.Copy(toolPath, customAssistant, true);
                object capturedStatus = detectFromExecutable.Invoke(null, new object[] { customWeGame });
                Type capturedStatusType = capturedStatus.GetType();
                Check(string.Equals((string)capturedStatusType.GetField("ExecutablePath").GetValue(capturedStatus), customWeGame, StringComparison.OrdinalIgnoreCase), "custom install path survives process shutdown");
                Check(string.Equals((string)capturedStatusType.GetField("AssistantExecutablePath").GetValue(capturedStatus), customAssistant, StringComparison.OrdinalIgnoreCase), "assistant is resolved under captured custom install path");
            }
        }

        Type compatibilityType = assembly.GetType("WeGameShoutTool.ToolCompatibility", false);
        Check(compatibilityType != null, "tool compatibility policy exists");
        if (compatibilityType != null)
        {
            FieldInfo toolVersionField = compatibilityType.GetField("ToolVersion", BindingFlags.Public | BindingFlags.Static);
            FieldInfo supportedWeGameField = compatibilityType.GetField("SupportedWeGameVersion", BindingFlags.Public | BindingFlags.Static);
            FieldInfo newlySupportedWeGameField = compatibilityType.GetField("NewlySupportedWeGameVersion", BindingFlags.Public | BindingFlags.Static);
            FieldInfo supportedAssistantField = compatibilityType.GetField("SupportedAssistantVersion", BindingFlags.Public | BindingFlags.Static);
            FieldInfo unsupportedMessageField = compatibilityType.GetField("UnsupportedMessage", BindingFlags.Public | BindingFlags.Static);
            MethodInfo evaluateVersions = compatibilityType.GetMethod("EvaluateVersionsForTesting", BindingFlags.Public | BindingFlags.Static);
            Check(toolVersionField != null && (string)toolVersionField.GetValue(null) == "6.22", "tool version is centralized at 6.22");
            Check(supportedWeGameField != null && (string)supportedWeGameField.GetValue(null) == "6.6.0.9080", "supported WeGame version is explicit");
            Check(newlySupportedWeGameField != null && (string)newlySupportedWeGameField.GetValue(null) == "6.6.1.9201", "new supported WeGame version is explicit");
            Check(supportedAssistantField != null && (string)supportedAssistantField.GetValue(null) == "5.15.1.1018", "supported assistant version is explicit");
            Check(unsupportedMessageField != null && ((string)unsupportedMessageField.GetValue(null)).Contains("软件已更新") && ((string)unsupportedMessageField.GetValue(null)).Contains("请联系作者"), "unsupported version message is explicit");
            Check(evaluateVersions != null, "compatibility evaluator is testable");
            if (evaluateVersions != null)
            {
                Check((int)evaluateVersions.Invoke(null, new object[] { "6.6.0.9080", "5.15.1.1018" }) == 1, "known WeGame and assistant pair is supported");
                Check((int)evaluateVersions.Invoke(null, new object[] { "6.6.1.9201", "5.15.1.1018" }) == 1, "new WeGame 6.6.1.9201 with supported assistant is accepted");
                Check((int)evaluateVersions.Invoke(null, new object[] { "6.6.1.9201", "5.15.2.1" }) == -1, "new WeGame still rejects an unknown assistant version");
                Check((int)evaluateVersions.Invoke(null, new object[] { "6.6.0.9999", "5.15.1.1018" }) == -1, "updated WeGame is rejected");
                Check((int)evaluateVersions.Invoke(null, new object[] { "6.6.0.9080", "5.15.2.1" }) == -1, "updated assistant is rejected");
                Check((int)evaluateVersions.Invoke(null, new object[] { "未知", "5.15.1.1018" }) == 0, "unknown version is not reported as compatible");
            }
        }

        Type workflowType = assembly.GetType("WeGameShoutTool.WorkflowGuide", false);
        Check(workflowType != null, "workflow guidance exists");
        if (workflowType != null)
        {
            MethodInfo getInstruction = workflowType.GetMethod("GetInstructionForTesting", BindingFlags.Public | BindingFlags.Static);
            MethodInfo getApplyBlock = workflowType.GetMethod("GetApplyBlockMessageForTesting", BindingFlags.Public | BindingFlags.Static);
            Check(getInstruction != null && getApplyBlock != null, "workflow guidance test entries exist");
            if (getInstruction != null && getApplyBlock != null)
            {
                string initialGuide = (string)getInstruction.Invoke(null, new object[] { false, false, false, false, false, false });
                Check(initialGuide == "第1步：目前未检测到 WeGame 正在运行，请先打开 WeGame。", "initial workflow is short and explicit");
                string runningGuide = (string)getInstruction.Invoke(null, new object[] { true, true, true, true, false, false });
                Check(runningGuide == "第3步：点击②，工具会安装强力覆盖补丁并重新打开 WeGame。", "ready workflow explains strong override install and restart");
                string readyGuide = (string)getInstruction.Invoke(null, new object[] { false, true, true, true, false, false });
                Check(readyGuide == "第3步：点击②，工具会安装强力覆盖补丁并重新打开 WeGame。", "stopped workflow explains strong override install and restart");
                string doneGuide = (string)getInstruction.Invoke(null, new object[] { false, true, true, true, false, true });
                Check(doneGuide.Contains("已打开 WeGame 自定义修改功能") && doneGuide.Contains("如果软件未生效，请在 WeGame 页面手动修改并保存即可"), "completed workflow gives the manual WeGame fallback");

                string notSeenBlock = (string)getApplyBlock.Invoke(null, new object[] { false, false, true, true, false });
                Check(notSeenBlock == "目前未检测到 WeGame 正在运行，请先打开 WeGame。\r\n\r\n检测到以后，请退出 WeGame，再点击②应用。", "first apply block is short and explicit");
                string runningBlock = (string)getApplyBlock.Invoke(null, new object[] { true, true, true, true, false });
                Check(runningBlock == null, "running WeGame can continue through automatic close flow");
                Check(getApplyBlock.Invoke(null, new object[] { false, true, true, true, false }) == null, "apply is allowed only at correct workflow state");

                FieldInfo signatureField = workflowType.GetField("ProductSignature", BindingFlags.Public | BindingFlags.Static);
                Check(signatureField != null && (string)signatureField.GetValue(null) == "B站:bia哩bia哩", "product signature is present");
                FieldInfo homeUrlField = workflowType.GetField("ProductHomeUrl", BindingFlags.Public | BindingFlags.Static);
                Check(homeUrlField != null && (string)homeUrlField.GetValue(null) == "https://space.bilibili.com/53391408?spm_id_from=333.788.0.0", "product signature link uses the requested Bilibili page");
                FieldInfo securityNoticeField = workflowType.GetField("SecuritySoftwareNotice", BindingFlags.Public | BindingFlags.Static);
                string securityNotice = securityNoticeField == null ? string.Empty : (string)securityNoticeField.GetValue(null);
                Check(securityNoticeField != null && securityNotice.Contains("安全软件") && securityNotice.Contains("仅允许本工具") && securityNotice.Contains("不要退出或长期关闭全部防护"), "security software notice gives a scoped safe action");
            }
        }

        Type privilegeType = assembly.GetType("WeGameShoutTool.PrivilegeManager", false);
        Check(privilegeType != null, "automatic administrator elevation exists");
        if (privilegeType != null)
        {
            MethodInfo buildElevationStart = privilegeType.GetMethod("BuildRelaunchInfoForTesting", BindingFlags.Public | BindingFlags.Static);
            MethodInfo isAdministrator = privilegeType.GetMethod("IsAdministrator", BindingFlags.Public | BindingFlags.Static);
            MethodInfo showRelaunchButton = privilegeType.GetMethod("ShouldShowRelaunchButton", BindingFlags.Public | BindingFlags.Static);
            Check(buildElevationStart != null && isAdministrator != null && showRelaunchButton != null, "administrator elevation test entries exist");
            if (showRelaunchButton != null)
            {
                Check((bool)showRelaunchButton.Invoke(null, new object[] { false }), "non-admin mode shows the administrator relaunch button");
                Check(!(bool)showRelaunchButton.Invoke(null, new object[] { true }), "administrator mode hides the relaunch button");
            }
            if (buildElevationStart != null)
            {
                ProcessStartInfo elevationStart = (ProcessStartInfo)buildElevationStart.Invoke(null, new object[]
                {
                    @"L:\Tools\WeGame喊话自定义工具.exe",
                    new string[] { "--sample" }
                });
                Check(elevationStart.UseShellExecute && string.Equals(elevationStart.Verb, "runas", StringComparison.OrdinalIgnoreCase), "startup requests Windows administrator approval");
                Check(elevationStart.FileName == @"L:\Tools\WeGame喊话自定义工具.exe", "elevation relaunches the same executable");
            }
        }

        Type processControllerType = assembly.GetType("WeGameShoutTool.WeGameProcessController", false);
        Check(processControllerType != null, "verified WeGame process controller exists");
        if (processControllerType != null)
        {
            MethodInfo selectProcessIds = processControllerType.GetMethod("SelectVerifiedProcessIdsForTesting", BindingFlags.Public | BindingFlags.Static);
            Check(selectProcessIds != null, "verified process selection test entry exists");
            if (selectProcessIds != null)
            {
                string expectedWeGame = @"L:\Program Files (x86)\WeGame\wegame.exe";
                int[] selectedIds = (int[])selectProcessIds.Invoke(null, new object[]
                {
                    expectedWeGame,
                    new int[] { 10, 20, 30 },
                    new string[] { expectedWeGame, @"C:\Other\wegame.exe", @"L:\Program Files (x86)\WeGame\WeGameMiniLoader.exe" }
                });
                Check(selectedIds.Length == 1 && selectedIds[0] == 10, "automatic close targets only the exact detected WeGame executable");
                MethodInfo isUnderDirectory = processControllerType.GetMethod("IsUnderDirectory", BindingFlags.NonPublic | BindingFlags.Static);
                Check(isUnderDirectory != null, "automatic close has WeGame install-root boundary");
                if (isUnderDirectory != null)
                {
                    Check((bool)isUnderDirectory.Invoke(null, new object[] { @"L:\Program Files (x86)\WeGame\apps\Pallas\pallas.exe", @"L:\Program Files (x86)\WeGame" }), "automatic close includes verified WeGame helper process");
                    Check(!(bool)isUnderDirectory.Invoke(null, new object[] { @"L:\Program Files (x86)\Other\pallas.exe", @"L:\Program Files (x86)\WeGame" }), "automatic close excludes same-name process outside WeGame");
                }
            }
        }

        Type loggerType = assembly.GetType("WeGameShoutTool.RuntimeLogger", false);
        Type stateType = assembly.GetType("WeGameShoutTool.ToolState", false);
        Type stateStoreType = assembly.GetType("WeGameShoutTool.ToolStateStore", false);
        Check(loggerType != null && stateType != null && stateStoreType != null, "runtime persistence types exist");
        if (loggerType != null && stateType != null && stateStoreType != null)
        {
            string artifactDirectory = Path.Combine(Path.GetDirectoryName(toolPath), "test-artifacts");
            Directory.CreateDirectory(artifactDirectory);
            string logPath = Path.Combine(artifactDirectory, "运行日志.txt");
            object logger = Activator.CreateInstance(loggerType, new object[] { logPath });
            loggerType.GetMethod("Write").Invoke(logger, new object[] { "信息", "工具启动" });
            Check(File.ReadAllText(logPath, Encoding.UTF8).Contains("[信息] 工具启动"), "runtime log persisted");
            PropertyInfo logPathProperty = loggerType.GetProperty("LogPath", BindingFlags.Public | BindingFlags.Instance);
            PropertyInfo sessionIdProperty = loggerType.GetProperty("SessionId", BindingFlags.Public | BindingFlags.Instance);
            MethodInfo writeException = loggerType.GetMethod("WriteException", BindingFlags.Public | BindingFlags.Instance);
            Check(logPathProperty != null && string.Equals((string)logPathProperty.GetValue(logger, null), logPath, StringComparison.OrdinalIgnoreCase), "runtime logger exposes its file path");
            Check(sessionIdProperty != null && !string.IsNullOrWhiteSpace((string)sessionIdProperty.GetValue(logger, null)), "runtime logger assigns a session id");
            Check(writeException != null, "runtime logger supports full exception diagnostics");
            if (writeException != null)
            {
                writeException.Invoke(logger, new object[] { "测试操作", new InvalidOperationException("外层错误", new IOException("内部错误")) });
                string diagnosticLog = File.ReadAllText(logPath, Encoding.UTF8);
                Check(diagnosticLog.Contains("InvalidOperationException") && diagnosticLog.Contains("IOException") && diagnosticLog.Contains("测试操作"), "runtime log preserves exception type and inner cause");
            }

            string statePath = Path.Combine(artifactDirectory, "工具状态.json");
            object state = Activator.CreateInstance(stateType);
            stateType.GetField("ThemeIndex").SetValue(state, 2);
            stateType.GetField("PackIndex").SetValue(state, 3);
            FieldInfo templateSourceField = stateType.GetField("TemplateSource");
            Check(templateSourceField != null, "template source state exists");
            if (templateSourceField != null) templateSourceField.SetValue(state, "custom");
            stateStoreType.GetMethod("Save").Invoke(null, new object[] { statePath, state });
            object loadedState = stateStoreType.GetMethod("Load").Invoke(null, new object[] { statePath });
            Check((int)stateType.GetField("ThemeIndex").GetValue(loadedState) == 2, "tool theme state round trips");
            Check((int)stateType.GetField("PackIndex").GetValue(loadedState) == 3, "tool pack state round trips");
            MethodInfo resolveTemplateSource = stateStoreType.GetMethod("ResolveTemplateSource", BindingFlags.Public | BindingFlags.Static);
            Check(resolveTemplateSource != null, "template source migration resolver exists");
            if (templateSourceField != null && resolveTemplateSource != null)
            {
                Check((string)templateSourceField.GetValue(loadedState) == "custom", "selected template source round trips");
                object legacyState = Activator.CreateInstance(stateType);
                Check((string)resolveTemplateSource.Invoke(null, new object[] { legacyState, true }) == "custom", "legacy users keep their existing custom library active");
                Check((string)resolveTemplateSource.Invoke(null, new object[] { legacyState, false }) == "builtin", "users without a custom library default to built-ins");
                object savedBuiltInState = Activator.CreateInstance(stateType);
                templateSourceField.SetValue(savedBuiltInState, "builtin");
                Check((string)resolveTemplateSource.Invoke(null, new object[] { savedBuiltInState, true }) == "builtin", "built-in library remains selectable when a custom library exists");
            }
            File.WriteAllText(statePath, "{broken", new UTF8Encoding(false));
            object fallbackState = stateStoreType.GetMethod("Load").Invoke(null, new object[] { statePath });
            Check((int)stateType.GetField("ThemeIndex").GetValue(fallbackState) == 0, "corrupt tool state falls back safely");
        }

        MethodInfo progressApply = patcher.GetMethod(
            "ApplyToCurrentWeGame",
            BindingFlags.Public | BindingFlags.Static,
            null,
            new Type[] { typeof(Action<string>), typeof(Action<int, string>) },
            null);
        Check(progressApply != null, "patch operation exposes progress callback");
        MethodInfo safeProgressApply = patcher.GetMethod(
            "ApplyToCurrentWeGame",
            BindingFlags.Public | BindingFlags.Static,
            null,
            new Type[] { typeof(string[]), typeof(Action<string>), typeof(Action<int, string>) },
            null);
        Check(safeProgressApply != null, "patch operation exposes phrase safety preflight");
        MethodInfo capturedPathApply = patcher.GetMethod(
            "ApplyToCurrentWeGame",
            BindingFlags.Public | BindingFlags.Static,
            null,
            new Type[] { typeof(string[]), typeof(string), typeof(Action<string>), typeof(Action<int, string>) },
            null);
        Check(capturedPathApply != null, "patch operation accepts the pre-close WeGame path");

        Console.WriteLine(failures == 0 ? "ALL TESTS PASSED" : (failures + " TEST(S) FAILED"));
        return failures == 0 ? 0 : 1;
    }
}
