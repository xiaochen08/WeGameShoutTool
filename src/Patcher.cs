using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace WeGameShoutTool
{
    public sealed class CompatibilityDiagnosisException : InvalidOperationException
    {
        public CompatibilityDiagnosisException(string report)
            : base(report)
        {
        }
    }

    public static class Patcher
    {
        private const string Endpoint = "http://127.0.0.1:27777/phrases";
        private const string ResultEndpoint = "http://127.0.0.1:27777/sync-result?code=";
        private const string StrongMarker = "__WG_SHOUT_FORCE_V1__";
        private const string PayloadMarker = "__WG_SHOUT_PAYLOAD_V4__";
        public const int NoSaveNeededResultCode = -2;
        public const int MaxPhraseLength = 150;
        public const int WeGameMaxPhraseLength = 50;
        public const int AllAudiencePrefixLength = 5;
        private const string OldImport = "j as je,k as Kt";
        private const string NewImport = "j as je,g as WgAuth,k as Kt";
        private const string OldRead = "je(()=>zt(),null,{})";
        private const string NewRead = "je(async()=>{await WgAuth();return zt()},null,{})";
        private const string OldSave = "I=()=>{const p={},x={title:c.value.title,key:c.value.key,...c.value.contents.reduce((W,ee,we)=>({...W,[we]:ee}),p)};ft(x)}";
        private const string NewSave = "I=async()=>{const p={},x={title:c.value.title,key:c.value.key,...c.value.contents.reduce((W,ee,we)=>({...W,[we]:ee}),p)};try{await WgAuth();const W=await ft(x),ee=W?.result?.error_code;return qe.info({description:ee===0?\"喊话已保存\":\"保存失败，错误码：\"+(ee??\"未知\")}),W}catch(W){qe.info({description:\"保存失败，请重试\"})}}";
        private const string CurrentOldSave = "I=()=>{const p={},x={title:d.value.title,key:d.value.key,...d.value.contents.reduce((W,ee,we)=>({...W,[we]:ee}),p)};ft(x)}";
        private const string CurrentNewSave = "I=async()=>{const p={},x={title:d.value.title,key:d.value.key,...d.value.contents.reduce((W,ee,we)=>({...W,[we]:ee}),p)};try{await WgAuth();const W=await ft(x),ee=W?.result?.error_code;return qe.info({description:ee===0?\"喊话已保存\":\"保存失败，错误码：\"+(ee??\"未知\")}),W}catch(W){qe.info({description:\"保存失败，请重试\"})}}";
        private const string OldMount = "ct(()=>{no(\"setting\"),U(),o()});";
        private const string LegacyMount = "ct(async()=>{try{const w=await fetch(\"" + Endpoint + "\",{cache:\"no-store\"}).then(v=>{if(!v.ok)throw Error(v.status);return v.json()});await WgAuth();const v=await ft(w),X=v?.result?.error_code;qe.info({description:X===0?\"已从喊话工具同步\":\"同步失败，错误码：\"+(X??\"未知\")})}catch(w){console.error(\"喊话工具同步失败\",w)}no(\"setting\"),U(),o()});";
        private const string LegacyOldMount = "ct(()=>{const s=async()=>{try{const w=await fetch(\"" + Endpoint + "\",{cache:\"no-store\"}).then(v=>{if(!v.ok)throw Error(v.status);return v.json()});await WgAuth();const v=await ft(w),X=v?.result?.error_code;fetch(\"" + ResultEndpoint + "\"+(X??-1),{cache:\"no-store\"}),globalThis.__WG_SHOUT_FORCE_V1__||(qe.info({description:X===0?\"已从喊话工具同步 强力覆盖已生效\":\"同步失败，错误码：\"+(X??\"未知\")}),globalThis.__WG_SHOUT_FORCE_V1__=1)}catch(w){console.error(\"喊话工具同步失败\",w)}};globalThis.__wgst&&clearInterval(globalThis.__wgst),s(),globalThis.__wgst=setInterval(s,10000),no(\"setting\"),U(),o()});";
        private const string V2NewMount = "ct(()=>{const s=async()=>{try{const w=await fetch(\"" + Endpoint + "\",{cache:\"no-store\"}).then(v=>{if(!v.ok)throw Error(v.status);return v.json()});await WgAuth();const b=await zt(),p=b&&typeof b===\"object\"?b:{},x={...p,...w,title:p.title??w.title,key:p.key??w.key},v=await ft(x),X=v?.result?.error_code;fetch(\"" + ResultEndpoint + "\"+(X??-1),{cache:\"no-store\"}),X===0?(qe.info({description:\"已从喊话工具同步 强力覆盖已生效\"}),globalThis.__WG_SHOUT_FORCE_V1__=1):qe.info({description:\"保存失败，错误码：\"+(X??\"未知\")})}catch(w){console.error(\"喊话工具同步失败\",w)}};globalThis.__WG_SHOUT_PAYLOAD_V2__=1,globalThis.__wgst&&clearInterval(globalThis.__wgst),s(),globalThis.__wgst=setInterval(s,10000),no(\"setting\"),U(),o()});";
        private const string CurrentMount = "dt(()=>{no(\"setting\"),U(),o()});";
        private const string LegacyOldCurrentMount = "dt(()=>{const s=async()=>{try{const w=await fetch(\"" + Endpoint + "\",{cache:\"no-store\"}).then(v=>{if(!v.ok)throw Error(v.status);return v.json()});await WgAuth();const v=await ft(w),X=v?.result?.error_code;fetch(\"" + ResultEndpoint + "\"+(X??-1),{cache:\"no-store\"}),globalThis.__WG_SHOUT_FORCE_V1__||(qe.info({description:X===0?\"已从喊话工具同步 强力覆盖已生效\":\"同步失败，错误码：\"+(X??\"未知\")}),globalThis.__WG_SHOUT_FORCE_V1__=1)}catch(w){console.error(\"喊话工具同步失败\",w)}};globalThis.__wgst&&clearInterval(globalThis.__wgst),s(),globalThis.__wgst=setInterval(s,10000),no(\"setting\"),U(),o()});";
        private const string V2CurrentNewMount = "dt(()=>{const s=async()=>{try{const w=await fetch(\"" + Endpoint + "\",{cache:\"no-store\"}).then(v=>{if(!v.ok)throw Error(v.status);return v.json()});await WgAuth();const b=await zt(),p=b&&typeof b===\"object\"?b:{},x={...p,...w,title:p.title??w.title,key:p.key??w.key},v=await ft(x),X=v?.result?.error_code;fetch(\"" + ResultEndpoint + "\"+(X??-1),{cache:\"no-store\"}),X===0?(qe.info({description:\"已从喊话工具同步 强力覆盖已生效\"}),globalThis.__WG_SHOUT_FORCE_V1__=1):qe.info({description:\"保存失败，错误码：\"+(X??\"未知\")})}catch(w){console.error(\"喊话工具同步失败\",w)}};globalThis.__WG_SHOUT_PAYLOAD_V2__=1,globalThis.__wgst&&clearInterval(globalThis.__wgst),s(),globalThis.__wgst=setInterval(s,10000),no(\"setting\"),U(),o()});";
        private const string LegacyNewMountV3 = "ct(()=>{const s=async()=>{if(globalThis.__wgstBusy)return;globalThis.__wgstBusy=1;const r=n=>fetch(\"" + ResultEndpoint + "?code=\"+n,{cache:\"no-store\"}).catch(()=>{});try{const w=await fetch(\"" + Endpoint + "\",{cache:\"no-store\"}).then(v=>{if(!v.ok)throw Error(v.status);return v.json()});await WgAuth();const b=await zt(),p=b&&typeof b===\"object\"?b:{},same=Array.from({length:10},(_,i)=>String(p[i]??\"\")===String(w[i]??\"\")).every(Boolean);if(same){r(-2);globalThis.__WG_SHOUT_FORCE_V1__=1;return}const x={...p,...w,title:p.title??w.title,key:p.key??w.key},v=await ft(x),X=v?.result?.error_code??-1;r(X);globalThis.__WG_SHOUT_FORCE_V1__=X===0?1:0}catch(w){console.error(w);r(-1);globalThis.__WG_SHOUT_FORCE_V1__=0}finally{globalThis.__wgstBusy=0}};globalThis.__WG_SHOUT_PAYLOAD_V3__=1,globalThis.__wgst&&clearInterval(globalThis.__wgst),s(),globalThis.__wgst=setInterval(s,10000),no(\"setting\"),U(),o()});";
        private const string LegacyCurrentNewMountV3 = "dt(()=>{const s=async()=>{if(globalThis.__wgstBusy)return;globalThis.__wgstBusy=1;const r=n=>fetch(\"" + ResultEndpoint + "?code=\"+n,{cache:\"no-store\"}).catch(()=>{});try{const w=await fetch(\"" + Endpoint + "\",{cache:\"no-store\"}).then(v=>{if(!v.ok)throw Error(v.status);return v.json()});await WgAuth();const b=await zt(),p=b&&typeof b===\"object\"?b:{},same=Array.from({length:10},(_,i)=>String(p[i]??\"\")===String(w[i]??\"\")).every(Boolean);if(same){r(-2);globalThis.__WG_SHOUT_FORCE_V1__=1;return}const x={...p,...w,title:p.title??w.title,key:p.key??w.key},v=await ft(x),X=v?.result?.error_code??-1;r(X);globalThis.__WG_SHOUT_FORCE_V1__=X===0?1:0}catch(w){console.error(w);r(-1);globalThis.__WG_SHOUT_FORCE_V1__=0}finally{globalThis.__wgstBusy=0}};globalThis.__WG_SHOUT_PAYLOAD_V3__=1,globalThis.__wgst&&clearInterval(globalThis.__wgst),s(),globalThis.__wgst=setInterval(s,10000),no(\"setting\"),U(),o()});";
        private const string NewMount = "ct(()=>{const s=async()=>{if(globalThis.__wgstBusy)return;globalThis.__wgstBusy=1;const r=n=>fetch(\"" + ResultEndpoint + "\"+n).catch(()=>{});try{const w=await fetch(\"" + Endpoint + "\",{cache:\"no-store\"}).then(v=>{if(!v.ok)throw Error(v.status);return v.json()});await WgAuth();const b=await zt(),p=b&&typeof b===\"object\"?b:{},q=p.shout_message?JSON.parse(Gt.decode(p.shout_message)):{},same=[0,1,2,3,4,5,6,7,8,9].every(i=>q[i]==w[i]);if(same){r(-2);globalThis.__WG_SHOUT_FORCE_V1__=1;return}const x={...q,...w,title:q.title||w.title,key:q.key||w.key},v=await ft(x),X=v?.result?.error_code??-1;r(X);globalThis.__WG_SHOUT_FORCE_V1__=X===0?1:0}catch(w){console.error(w);r(-1);globalThis.__WG_SHOUT_FORCE_V1__=0}finally{globalThis.__wgstBusy=0}};globalThis.__WG_SHOUT_PAYLOAD_V4__=1,globalThis.__wgst&&clearInterval(globalThis.__wgst),s(),globalThis.__wgst=setInterval(s,10000),no(\"setting\"),U(),o()});";
        private const string CurrentNewMount = "dt(()=>{const s=async()=>{if(globalThis.__wgstBusy)return;globalThis.__wgstBusy=1;const r=n=>fetch(\"" + ResultEndpoint + "\"+n).catch(()=>{});try{const w=await fetch(\"" + Endpoint + "\",{cache:\"no-store\"}).then(v=>{if(!v.ok)throw Error(v.status);return v.json()});await WgAuth();const b=await zt(),p=b&&typeof b===\"object\"?b:{},q=p.shout_message?JSON.parse(Gt.decode(p.shout_message)):{},same=[0,1,2,3,4,5,6,7,8,9].every(i=>q[i]==w[i]);if(same){r(-2);globalThis.__WG_SHOUT_FORCE_V1__=1;return}const x={...q,...w,title:q.title||w.title,key:q.key||w.key},v=await ft(x),X=v?.result?.error_code??-1;r(X);globalThis.__WG_SHOUT_FORCE_V1__=X===0?1:0}catch(w){console.error(w);r(-1);globalThis.__WG_SHOUT_FORCE_V1__=0}finally{globalThis.__wgstBusy=0}};globalThis.__WG_SHOUT_PAYLOAD_V4__=1,globalThis.__wgst&&clearInterval(globalThis.__wgst),s(),globalThis.__wgst=setInterval(s,10000),no(\"setting\"),U(),o()});";

        private sealed class CacheScanDiagnostics
        {
            public int BrowserCacheDirectoryCount;
            public int CacheDataDirectoryCount;
            public int ReadableGzipFileCount;
            public int SettingPageCount;
            public int SaveInterfaceCount;
            public int LockedFileCount;
            public DateTime NewestSettingPageFileWriteTime;
        }

        public static string PatchSourceForTesting(string source)
        {
            if (source == null)
            {
                throw new ArgumentNullException("source");
            }

            if (IsFullyPatched(source))
            {
                return source;
            }

            bool looksCompatible = source.Contains("_plugin-vue_export-helper")
                && (source.Contains(OldRead) || source.Contains(NewRead))
                && (source.Contains("Ln=!1") || source.Contains("Ln=!0"))
                && source.Contains("m as Gt")
                && (source.Contains(OldSave) || source.Contains(NewSave)
                    || source.Contains(CurrentOldSave) || source.Contains(CurrentNewSave));
            if (!looksCompatible)
            {
                throw new InvalidOperationException("当前 WeGame 版本与本工具的安全特征不匹配，已停止修改。");
            }

            string result = source;
            if (!result.Contains(NewImport))
            {
                result = ReplaceExactlyOnce(result, OldImport, NewImport, "登录初始化导入");
            }
            if (!result.Contains(NewRead))
            {
                result = ReplaceExactlyOnce(result, OldRead, NewRead, "读取前登录初始化");
            }
            if (result.Contains("Ln=!1"))
            {
                result = ReplaceExactlyOnce(result, "Ln=!1", "Ln=!0", "自定义按钮");
            }
            if (result.Contains("e=p=>!0"))
            {
                result = ReplaceExactlyOnce(result, "e=p=>!0", "e=p=>A.value[p]", "输入模式");
            }
            if (!result.Contains(NewSave))
            {
                if (result.Contains(OldSave))
                {
                    result = ReplaceExactlyOnce(result, OldSave, NewSave, "保存反馈");
                }
                else if (result.Contains(CurrentOldSave))
                {
                    result = ReplaceExactlyOnce(result, CurrentOldSave, CurrentNewSave, "当前版保存反馈");
                }
            }
            if (result.Contains(LegacyNewMountV3))
            {
                result = ReplaceExactlyOnce(result, LegacyNewMountV3, NewMount, "升级 V6.22 强力覆盖同步");
            }
            else if (result.Contains(LegacyCurrentNewMountV3))
            {
                result = ReplaceExactlyOnce(result, LegacyCurrentNewMountV3, CurrentNewMount, "升级 V6.22 当前版强力覆盖同步");
            }
            else if (result.Contains(V2NewMount))
            {
                result = ReplaceExactlyOnce(result, V2NewMount, NewMount, "升级 V6.17 强力覆盖同步");
            }
            else if (result.Contains(V2CurrentNewMount))
            {
                result = ReplaceExactlyOnce(result, V2CurrentNewMount, CurrentNewMount, "升级 V6.17 当前版强力覆盖同步");
            }
            else if (result.Contains(LegacyMount))
            {
                result = ReplaceExactlyOnce(result, LegacyMount, NewMount, "强力覆盖同步");
            }
            else if (result.Contains(LegacyOldMount))
            {
                result = ReplaceExactlyOnce(result, LegacyOldMount, NewMount, "升级旧版强力覆盖同步");
            }
            else if (result.Contains(CurrentMount))
            {
                result = ReplaceExactlyOnce(result, CurrentMount, CurrentNewMount, "当前版强力覆盖同步");
            }
            else if (result.Contains(LegacyOldCurrentMount))
            {
                result = ReplaceExactlyOnce(result, LegacyOldCurrentMount, CurrentNewMount, "升级当前版强力覆盖同步");
            }
            else if (!result.Contains(Endpoint))
            {
                result = ReplaceExactlyOnce(result, OldMount, NewMount, "本地同步入口");
            }

            if (!IsFullyPatched(result))
            {
                throw new InvalidOperationException("补丁结果未通过完整性检查，已停止修改。");
            }
            return result;
        }

        public static string ValidatePhrasesForTesting(string[] phrases)
        {
            if (phrases == null || phrases.Length != 10)
            {
                return "必须正好填写 10 条喊话。";
            }
            for (int i = 0; i < phrases.Length; i++)
            {
                string value = phrases[i] == null ? string.Empty : phrases[i].Trim();
                if (value.Length == 0)
                {
                    return "第 " + (i + 1).ToString(CultureInfo.InvariantCulture) + " 条不能为空。";
                }
                if (value.Length > MaxPhraseLength)
                {
                    return "第 " + (i + 1).ToString(CultureInfo.InvariantCulture) + " 条超过 " + MaxPhraseLength.ToString(CultureInfo.InvariantCulture) + " 个字符。";
                }
                if (HasAllPrefix(value) && value.Substring(4).Trim().Length == 0)
                {
                    return "第 " + (i + 1).ToString(CultureInfo.InvariantCulture) + " 条只有 /all，没有实际喊话内容。";
                }
                if (value.IndexOf('\r') >= 0 || value.IndexOf('\n') >= 0)
                {
                    return "单条喊话不能包含换行。";
                }
            }
            return null;
        }

        public static string ValidatePhrasesForWeGameForTesting(string[] phrases)
        {
            string validation = ValidatePhrasesForTesting(phrases);
            if (validation != null)
            {
                return validation;
            }
            for (int i = 0; i < phrases.Length; i++)
            {
                string value = phrases[i] == null ? string.Empty : phrases[i].Trim();
                if (value.Length > WeGameMaxPhraseLength)
                {
                    return "第 " + (i + 1).ToString(CultureInfo.InvariantCulture)
                        + " 条超过 WeGame 当前每条 " + WeGameMaxPhraseLength.ToString(CultureInfo.InvariantCulture)
                        + " 个字符的限制，请先缩短后再应用。";
                }
            }
            return null;
        }

        public static string BuildPhrasesJson(string[] phrases)
        {
            string validation = ValidatePhrasesForTesting(phrases);
            if (validation != null)
            {
                throw new InvalidOperationException(validation);
            }

            StringBuilder json = new StringBuilder();
            json.Append("{\"title\":\"自定义\",\"key\":1");
            for (int i = 0; i < phrases.Length; i++)
            {
                json.Append(",\"");
                json.Append(i.ToString(CultureInfo.InvariantCulture));
                json.Append("\":\"");
                json.Append(JsonEscape(phrases[i].Trim()));
                json.Append("\"");
            }
            json.Append("}");
            return json.ToString();
        }

        public static string[] ReorderPhrasesForTesting(string[] phrases, int sourceIndex, int targetIndex)
        {
            if (phrases == null)
            {
                throw new ArgumentNullException("phrases");
            }
            if (sourceIndex < 0 || sourceIndex >= phrases.Length)
            {
                throw new ArgumentOutOfRangeException("sourceIndex");
            }
            if (targetIndex < 0 || targetIndex >= phrases.Length)
            {
                throw new ArgumentOutOfRangeException("targetIndex");
            }

            string[] result = (string[])phrases.Clone();
            if (sourceIndex == targetIndex)
            {
                return result;
            }

            string moved = result[sourceIndex];
            if (sourceIndex < targetIndex)
            {
                for (int i = sourceIndex; i < targetIndex; i++)
                {
                    result[i] = result[i + 1];
                }
            }
            else
            {
                for (int i = sourceIndex; i > targetIndex; i--)
                {
                    result[i] = result[i - 1];
                }
            }
            result[targetIndex] = moved;
            return result;
        }

        public static void SavePhrasesFileForTesting(string path, string[] phrases)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                throw new ArgumentException("保存路径不能为空。", "path");
            }
            if (phrases == null || phrases.Length != 10)
            {
                throw new InvalidOperationException("必须正好提供 10 条喊话才能保存。 ");
            }
            string directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }
            File.WriteAllLines(path, phrases, new UTF8Encoding(false));
        }

        public static string ApplyAudienceForTesting(string phrase, bool allAudience)
        {
            string value = phrase == null ? string.Empty : phrase.TrimStart();
            bool hasAllPrefix = HasAllPrefix(value);
            if (allAudience)
            {
                if (hasAllPrefix)
                {
                    return value;
                }
                if (value.Length + AllAudiencePrefixLength > MaxPhraseLength)
                {
                    throw new InvalidOperationException("加上 /all 后会超过 " + MaxPhraseLength.ToString(CultureInfo.InvariantCulture) + " 个字符，请先缩短内容。");
                }
                return "/all " + value;
            }

            return hasAllPrefix ? value.Substring(4).TrimStart() : value;
        }

        public static bool IsAllAudienceForTesting(string phrase)
        {
            return HasAllPrefix(phrase == null ? string.Empty : phrase.TrimStart());
        }

        public static string[] ApplyAudienceToAllForTesting(string[] phrases, bool allAudience)
        {
            if (phrases == null || phrases.Length != 10)
            {
                throw new InvalidOperationException("必须正好提供 10 条喊话。");
            }
            string[] result = new string[phrases.Length];
            for (int i = 0; i < phrases.Length; i++)
            {
                result[i] = ApplyAudienceForTesting(phrases[i], allAudience);
            }
            return result;
        }

        public static string GetLocalPhraseFileForTesting(string documentsDirectory)
        {
            if (string.IsNullOrWhiteSpace(documentsDirectory))
            {
                throw new ArgumentException("文档目录不能为空。", "documentsDirectory");
            }
            return Path.Combine(documentsDirectory, "WeGame喊话自定义工具", "喊话内容.txt");
        }

        public static string GetToolDirectoryForTesting(string documentsDirectory)
        {
            if (string.IsNullOrWhiteSpace(documentsDirectory))
            {
                throw new ArgumentException("文档目录不能为空。", "documentsDirectory");
            }
            return Path.Combine(documentsDirectory, "WeGame喊话自定义工具");
        }

        public static string ApplyToCurrentWeGame(Action<string> report)
        {
            return ApplyToCurrentWeGame(report, delegate { });
        }

        public static string ApplyToCurrentWeGame(Action<string> report, Action<int, string> progress)
        {
            return ApplyToCurrentWeGameCore(null, report, progress);
        }

        public static string ApplyToCurrentWeGame(string[] phrases, Action<string> report, Action<int, string> progress)
        {
            return ApplyToCurrentWeGame(phrases, null, report, progress);
        }

        public static string ApplyToCurrentWeGame(string[] phrases, string verifiedWeGameExecutablePath, Action<string> report, Action<int, string> progress)
        {
            ReportProgress(progress, 2, "检查喊话内容安全");
            string validation = ValidatePhrasesForTesting(phrases);
            if (validation != null)
            {
                throw new InvalidOperationException(validation);
            }
            string weGameValidation = ValidatePhrasesForWeGameForTesting(phrases);
            if (weGameValidation != null)
            {
                throw new InvalidOperationException(weGameValidation);
            }
            SafetyFinding finding = SafetyTextPolicy.CheckAllForTesting(phrases);
            if (finding != null)
            {
                throw new InvalidOperationException("第 " + finding.LineNumber.ToString(CultureInfo.InvariantCulture) + " 条未通过本地安全检查：" + finding.Category + "。");
            }
            if (report != null)
            {
                report("喊话内容已通过本地安全检查。");
            }
            return ApplyToCurrentWeGameCore(verifiedWeGameExecutablePath, report, progress);
        }

        private static string ApplyToCurrentWeGameCore(string verifiedWeGameExecutablePath, Action<string> report, Action<int, string> progress)
        {
            ReportProgress(progress, 5, "检查 WeGame 运行状态");
            if (Process.GetProcessesByName("wegame").Length > 0)
            {
                throw new InvalidOperationException("请先从系统托盘彻底退出 WeGame，再点击应用。工具本身可以继续保持运行。");
            }

            ReportProgress(progress, 10, "定位 WeGame 版本");
            WeGameStatus detected;
            if (string.IsNullOrWhiteSpace(verifiedWeGameExecutablePath))
            {
                detected = WeGameDetector.Detect();
                if (report != null)
                {
                    report("未收到关闭前路径，使用安装信息重新定位 WeGame。");
                }
            }
            else
            {
                detected = WeGameDetector.DetectFromExecutablePath(verifiedWeGameExecutablePath);
                if (report != null)
                {
                    report("沿用关闭前已确认的 WeGame 路径：" + detected.ExecutablePath);
                }
            }
            if (report != null)
            {
                report("WeGame版本：" + detected.Version + "，位置：" + (detected.ExecutablePath ?? "未找到"));
                report("WeGame助手版本：" + detected.AssistantVersion + "，位置：" + (detected.AssistantExecutablePath ?? "未找到"));
            }
            ToolCompatibility.EnsureSupported(detected);

            string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            string weGameRoot = Path.Combine(appData, "Tencent", "WeGame");
            if (!Directory.Exists(weGameRoot))
            {
                throw new DirectoryNotFoundException("没有找到 WeGame 用户缓存目录：" + weGameRoot);
            }
            if (report != null)
            {
                report("WeGame缓存根目录：" + weGameRoot);
            }

            ReportProgress(progress, 20, "扫描 WeGame 缓存");
            CacheScanDiagnostics scan = new CacheScanDiagnostics();
            List<CacheCandidate> candidates = FindCandidates(weGameRoot, report, scan);
            if (report != null)
            {
                report("缓存扫描完成：兼容候选=" + candidates.Count.ToString(CultureInfo.InvariantCulture)
                    + "，可读取页面文件=" + scan.ReadableGzipFileCount.ToString(CultureInfo.InvariantCulture)
                    + "，最新喊话设置页文件时间=" + (scan.NewestSettingPageFileWriteTime == DateTime.MinValue
                        ? "未发现"
                        : scan.NewestSettingPageFileWriteTime.ToString("O", CultureInfo.InvariantCulture)) + "。");
            }
            if (candidates.Count == 0)
            {
                throw new CompatibilityDiagnosisException(BuildNoCompatibleCacheReport(scan));
            }

            int selectedIndex = SelectCurrentCacheCandidateIndexForTesting(
                candidates.Select(c => c.FileWriteTime).ToArray(),
                scan.NewestSettingPageFileWriteTime);
            if (selectedIndex < 0)
            {
                throw new CompatibilityDiagnosisException(BuildStaleCacheReport(candidates.Count, scan.NewestSettingPageFileWriteTime));
            }

            CacheCandidate selected = candidates[selectedIndex];
            report("选中当前缓存：" + selected.Path);
            ReportProgress(progress, 35, "生成补丁");

            string patched = PatchSourceForTesting(selected.Source);
            if (patched == selected.Source && IsFullyPatched(selected.Source))
            {
                report("当前缓存已经包含工具同步补丁，无需重复写入。");
                ReportProgress(progress, 100, "当前版本已经应用");
                return selected.Path;
            }

            string sevenZip = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "runtime", "7z.exe");
            if (!File.Exists(sevenZip))
            {
                throw new FileNotFoundException("工具运行组件不完整，缺少 runtime\\7z.exe。", sevenZip);
            }

            string runDirectory = Path.Combine(Path.GetTempPath(), "WeGameShoutTool", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(runDirectory);
            ReportProgress(progress, 55, "压缩并匹配缓存长度");
            string candidateGzip = BuildExactLengthGzip(patched, new FileInfo(selected.Path).Length, sevenZip, runDirectory, report);
            VerifyGzip(candidateGzip, true);
            ReportProgress(progress, 70, "压缩验证通过");

            string backup = selected.Path + ".before-wegame-shout-tool-" + DateTime.Now.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture) + ".bak";
            if (File.Exists(backup))
            {
                backup = selected.Path + ".before-wegame-shout-tool-" + Guid.NewGuid().ToString("N") + ".bak";
            }

            File.Copy(selected.Path, backup, false);
            report("已备份：" + backup);
            ReportProgress(progress, 82, "原缓存已备份");
            try
            {
                ReportProgress(progress, 90, "写入并验证");
                File.Copy(candidateGzip, selected.Path, true);
                string actual = ReadGzipText(selected.Path);
                if (!IsFullyPatched(actual) || new FileInfo(selected.Path).Length != new FileInfo(backup).Length)
                {
                    throw new InvalidDataException("写入后的缓存没有通过完整性检查。");
                }
            }
            catch
            {
                File.Copy(backup, selected.Path, true);
                throw;
            }

            report("应用完成。缓存长度和补丁特征验证通过。");
            ReportProgress(progress, 100, "应用完成");
            return selected.Path;
        }

        public static int SelectCurrentCacheCandidateIndexForTesting(
            DateTime[] candidateFileWriteTimes,
            DateTime newestSettingPageFileWriteTime)
        {
            if (candidateFileWriteTimes == null)
            {
                throw new ArgumentException("缓存候选时间必须一一对应。");
            }

            int selectedIndex = -1;
            for (int i = 0; i < candidateFileWriteTimes.Length; i++)
            {
                if (candidateFileWriteTimes[i] < newestSettingPageFileWriteTime)
                {
                    continue;
                }
                if (selectedIndex < 0
                    || candidateFileWriteTimes[i] > candidateFileWriteTimes[selectedIndex])
                {
                    selectedIndex = i;
                }
            }
            return selectedIndex;
        }

        private static List<CacheCandidate> FindCandidates(string root, Action<string> report, CacheScanDiagnostics scan)
        {
            List<CacheCandidate> result = new List<CacheCandidate>();
            foreach (string qbcore in Directory.GetDirectories(root, "qbcore*", SearchOption.TopDirectoryOnly))
            {
                string cacheRoot = Path.Combine(qbcore, "cache");
                if (!Directory.Exists(cacheRoot))
                {
                    continue;
                }
                foreach (string browserCache in Directory.GetDirectories(cacheRoot, "qbcore_cache_browser.exe_*", SearchOption.TopDirectoryOnly))
                {
                    scan.BrowserCacheDirectoryCount++;
                    string data = Path.Combine(browserCache, "Cache", "Cache_Data");
                    if (!Directory.Exists(data))
                    {
                        continue;
                    }
                    scan.CacheDataDirectoryCount++;
                    foreach (string file in Directory.GetFiles(data, "f_*", SearchOption.TopDirectoryOnly))
                    {
                        if (!Regex.IsMatch(Path.GetFileName(file), "^f_[0-9a-fA-F]+$"))
                        {
                            continue;
                        }
                        FileInfo info = new FileInfo(file);
                        if (info.Length < 1024 || info.Length > 10 * 1024 * 1024 || !HasGzipHeader(file))
                        {
                            continue;
                        }
                        try
                        {
                            string source = ReadGzipText(file);
                            scan.ReadableGzipFileCount++;
                            bool isSettingPage = IsSettingPageSource(source);
                            bool hasSaveInterface = source.Contains(OldSave)
                                || source.Contains(NewSave)
                                || source.Contains(CurrentOldSave)
                                || source.Contains(CurrentNewSave);
                            if (isSettingPage)
                            {
                                scan.SettingPageCount++;
                                if (info.LastWriteTimeUtc > scan.NewestSettingPageFileWriteTime)
                                {
                                    scan.NewestSettingPageFileWriteTime = info.LastWriteTimeUtc;
                                }
                            }
                            if (hasSaveInterface)
                            {
                                scan.SaveInterfaceCount++;
                            }
                            if (isSettingPage && hasSaveInterface)
                            {
                                result.Add(new CacheCandidate
                                {
                                    Path = file,
                                    Source = source,
                                    FileWriteTime = info.LastWriteTimeUtc
                                });
                                report("发现兼容缓存：" + file);
                            }
                        }
                        catch (InvalidDataException)
                        {
                        }
                        catch (IOException)
                        {
                            scan.LockedFileCount++;
                        }
                    }
                }
            }
            return result;
        }

        public static string BuildNoCompatibleCacheReportForTesting(
            int browserCacheDirectoryCount,
            int readableGzipFileCount,
            int settingPageCount,
            int saveInterfaceCount,
            int lockedFileCount)
        {
            CacheScanDiagnostics scan = new CacheScanDiagnostics
            {
                BrowserCacheDirectoryCount = browserCacheDirectoryCount,
                ReadableGzipFileCount = readableGzipFileCount,
                SettingPageCount = settingPageCount,
                SaveInterfaceCount = saveInterfaceCount,
                LockedFileCount = lockedFileCount
            };
            return BuildNoCompatibleCacheReport(scan);
        }

        public static DateTime FindNewestSettingPageWriteTimeForTesting(string root)
        {
            CacheScanDiagnostics scan = new CacheScanDiagnostics();
            FindCandidates(root, delegate(string message) { }, scan);
            return scan.NewestSettingPageFileWriteTime;
        }

        private static string BuildNoCompatibleCacheReport(CacheScanDiagnostics scan)
        {
            StringBuilder report = new StringBuilder();
            report.Append("本次没有修改 WeGame 缓存。\r\n\r\n");
            if (scan.SettingPageCount > 0 && scan.SaveInterfaceCount == 0)
            {
                report.Append("直接原因：已识别到喊话设置页面，但没有识别到当前版本的保存接口，工具已停止写入，避免修改无关页面。\r\n\r\n");
            }
            else
            {
                report.Append("直接原因：没有识别到当前版本的喊话设置页面，工具已停止写入，避免修改无关页面。\r\n\r\n");
            }
            report.Append("本次检查：浏览器缓存 ");
            report.Append(scan.BrowserCacheDirectoryCount.ToString(CultureInfo.InvariantCulture));
            report.Append(" 个；可读取页面文件 ");
            report.Append(scan.ReadableGzipFileCount.ToString(CultureInfo.InvariantCulture));
            report.Append(" 个；识别到喊话设置页 ");
            report.Append(scan.SettingPageCount.ToString(CultureInfo.InvariantCulture));
            report.Append(" 个；识别到可用保存结构 ");
            report.Append(scan.SaveInterfaceCount.ToString(CultureInfo.InvariantCulture));
            report.Append(" 个。\r\n");
            if (scan.LockedFileCount > 0)
            {
                report.Append("另有 ");
                report.Append(scan.LockedFileCount.ToString(CultureInfo.InvariantCulture));
                report.Append(" 个缓存文件仍被占用，未读取也未修改。\r\n");
            }
            report.Append("\r\n可能原因：\r\n");
            if (scan.BrowserCacheDirectoryCount == 0 || scan.ReadableGzipFileCount == 0)
            {
                report.Append("1. 当前电脑尚未生成 WeGame 浏览器页面缓存。\r\n");
            }
            else if (scan.SettingPageCount == 0)
            {
                report.Append("1. 尚未打开过当前 WeGame 的英雄联盟辅助设置页面，或 WeGame 已更新了该页面资源。\r\n");
            }
            else
            {
                report.Append("1. 已找到喊话设置页，但保存接口结构已变化，较可能是 WeGame 页面资源热更新。\r\n");
            }
            report.Append("\r\n现在怎么做：\r\n");
            report.Append("1. 先打开 WeGame，在客户端内进入 英雄联盟 → 辅助设置 → 其他辅助，不要启动游戏。\r\n");
            report.Append("2. 从系统托盘退出 WeGame 后，再点击②重试。\r\n");
            report.Append("3. 如果仍出现本报告，请点击“导出诊断报告”并联系作者；不要反复强制应用，也不要手动修改缓存文件。\r\n");
            return report.ToString();
        }

        private static string BuildStaleCacheReport(int compatibleCandidateCount, DateTime newestSettingPageFileWriteTime)
        {
            return "本次没有修改 WeGame 缓存。\r\n\r\n"
                + "直接原因：找到 " + compatibleCandidateCount.ToString(CultureInfo.InvariantCulture)
                + " 个兼容喊话页面，但存在更新的喊话设置页面（页面缓存时间："
                + newestSettingPageFileWriteTime.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture)
                + "）。工具不会把补丁写进旧页面，避免出现“显示成功但游戏无效”。\r\n\r\n"
                + "现在怎么做：\r\n"
                + "1. 打开 WeGame，在客户端内进入 英雄联盟 → 辅助设置 → 其他辅助，不要启动游戏。\r\n"
                + "2. 从系统托盘退出 WeGame 后，再点击②重试。\r\n"
                + "3. 如果仍出现本报告，请点击“导出诊断报告”并联系作者；不要强制套用旧缓存。\r\n";
        }

        public static bool IsSettingPageSourceForTesting(string source)
        {
            return IsSettingPageSource(source);
        }

        private static bool IsSettingPageSource(string source)
        {
            return !string.IsNullOrEmpty(source)
                && source.Contains("_plugin-vue_export-helper")
                && source.Contains("shout_message")
                && source.Contains("__name:\"others\"")
                && source.Contains("一键喊话");
        }

        private static string BuildExactLengthGzip(string source, long targetLength, string sevenZip, string runDirectory, Action<string> report)
        {
            string baseSource = Regex.Replace(source, "(?:\\r?\\n)?/\\*CACHEPAD:.*?\\*/\\s*$", string.Empty, RegexOptions.Singleline);
            string entropy = BuildEntropy(20000, "wegame-shout-tool");
            string payload = Path.Combine(runDirectory, "payload.js");
            int attempt = 0;

            Func<int, bool, CompressionCandidate> measure = delegate(int padLength, bool useWhitespacePadding)
            {
                attempt++;
                string text = baseSource;
                if (padLength > 0)
                {
                    text += useWhitespacePadding
                        ? new string(' ', padLength)
                        : "\n/*CACHEPAD:" + entropy.Substring(0, padLength) + "*/";
                }
                File.WriteAllText(payload, text, new UTF8Encoding(false));
                string name = "candidate-" + attempt.ToString("D4", CultureInfo.InvariantCulture) + "-pad-" + padLength.ToString(CultureInfo.InvariantCulture) + ".gz";
                string output = Path.Combine(runDirectory, name);
                RunSevenZip(sevenZip, runDirectory, name);
                return new CompressionCandidate { Path = output, Length = new FileInfo(output).Length, PadLength = padLength };
            };

            CompressionCandidate baseline = measure(0, false);
            if (baseline.Length > targetLength)
            {
                throw new InvalidOperationException("当前版本的缓存压缩空间不足，工具为避免损坏已停止。目标 " + targetLength + " 字节，候选 " + baseline.Length + " 字节。");
            }
            if (baseline.Length == targetLength)
            {
                return baseline.Path;
            }

            long sizeGap = targetLength - baseline.Length;
            if (sizeGap <= 32)
            {
                for (int spaces = 1; spaces <= 32; spaces++)
                {
                    CompressionCandidate whitespaceCandidate = measure(spaces, true);
                    if (whitespaceCandidate.Length == targetLength)
                    {
                        report("已使用无行为影响的尾随空格生成等长缓存候选：" + targetLength.ToString(CultureInfo.InvariantCulture) + " 字节。");
                        return whitespaceCandidate.Path;
                    }
                    if (whitespaceCandidate.Length > targetLength)
                    {
                        break;
                    }
                }
            }

            int low = 1;
            int high = 16000;
            CompressionCandidate highCandidate = measure(high, false);
            if (highCandidate.Length < targetLength)
            {
                throw new InvalidOperationException("无法生成与原缓存等长的安全候选文件。");
            }

            while (low < high)
            {
                int mid = low + ((high - low) / 2);
                CompressionCandidate current = measure(mid, false);
                if (current.Length < targetLength)
                {
                    low = mid + 1;
                }
                else
                {
                    high = mid;
                }
            }

            for (int distance = 0; distance <= 192; distance++)
            {
                int[] probes = distance == 0 ? new int[] { low } : new int[] { low - distance, low + distance };
                foreach (int probe in probes)
                {
                    if (probe < 0 || probe > entropy.Length)
                    {
                        continue;
                    }
                    CompressionCandidate current = measure(probe, false);
                    if (current.Length == targetLength)
                    {
                        report("已生成等长缓存候选：" + targetLength.ToString(CultureInfo.InvariantCulture) + " 字节。");
                        return current.Path;
                    }
                }
            }
            throw new InvalidOperationException("没有找到与原缓存完全等长的安全候选文件，已停止写入。");
        }

        private static void RunSevenZip(string sevenZip, string workingDirectory, string outputName)
        {
            ProcessStartInfo start = new ProcessStartInfo();
            start.FileName = sevenZip;
            start.WorkingDirectory = workingDirectory;
            start.Arguments = "a -tgzip -mx=9 -mmt=off \"" + outputName + "\" \"payload.js\" -y";
            start.UseShellExecute = false;
            start.CreateNoWindow = true;
            start.RedirectStandardOutput = true;
            start.RedirectStandardError = true;
            using (Process process = Process.Start(start))
            {
                string output = process.StandardOutput.ReadToEnd();
                string error = process.StandardError.ReadToEnd();
                process.WaitForExit();
                if (process.ExitCode != 0)
                {
                    throw new InvalidOperationException("7-Zip 压缩失败：" + output + error);
                }
            }
        }

        private static bool IsFullyPatched(string source)
        {
            return source.Contains("Ln=!0")
                && source.Contains("e=p=>A.value[p]")
                && source.Contains("m as Gt")
                && source.Contains(NewImport)
                && source.Contains(NewRead)
                && (source.Contains(NewSave) || source.Contains(CurrentNewSave))
                && source.Contains(Endpoint)
                && source.Contains(ResultEndpoint)
                && source.Contains(StrongMarker)
                && source.Contains(PayloadMarker)
                && (source.Contains(NewMount) || source.Contains(CurrentNewMount))
                && source.Contains("r(-2)");
        }

        private static string ReplaceExactlyOnce(string source, string oldValue, string newValue, string label)
        {
            int first = source.IndexOf(oldValue, StringComparison.Ordinal);
            if (first < 0 || source.IndexOf(oldValue, first + oldValue.Length, StringComparison.Ordinal) >= 0)
            {
                throw new InvalidOperationException(label + "的版本特征不是唯一匹配，已停止修改。");
            }
            return source.Substring(0, first) + newValue + source.Substring(first + oldValue.Length);
        }

        private static bool HasGzipHeader(string path)
        {
            using (FileStream stream = File.OpenRead(path))
            {
                return stream.ReadByte() == 0x1f && stream.ReadByte() == 0x8b;
            }
        }

        private static string ReadGzipText(string path)
        {
            using (FileStream file = File.OpenRead(path))
            using (GZipStream gzip = new GZipStream(file, CompressionMode.Decompress))
            using (StreamReader reader = new StreamReader(gzip, Encoding.UTF8, true))
            {
                return reader.ReadToEnd();
            }
        }

        private static void VerifyGzip(string path, bool requirePatched)
        {
            string source = ReadGzipText(path);
            if (source.Length == 0 || (requirePatched && !IsFullyPatched(source)))
            {
                throw new InvalidDataException("候选缓存解压或补丁完整性验证失败。");
            }
        }

        private static string BuildEntropy(int length, string salt)
        {
            StringBuilder builder = new StringBuilder(length + 64);
            int index = 0;
            using (SHA256 algorithm = SHA256.Create())
            {
                while (builder.Length < length)
                {
                    byte[] hash = algorithm.ComputeHash(Encoding.UTF8.GetBytes(salt + "-" + index.ToString(CultureInfo.InvariantCulture)));
                    for (int i = 0; i < hash.Length; i++)
                    {
                        builder.Append(hash[i].ToString("x2", CultureInfo.InvariantCulture));
                    }
                    index++;
                }
            }
            return builder.ToString(0, length);
        }

        private static string JsonEscape(string value)
        {
            StringBuilder result = new StringBuilder();
            foreach (char c in value)
            {
                switch (c)
                {
                    case '\\': result.Append("\\\\"); break;
                    case '"': result.Append("\\\""); break;
                    case '\b': result.Append("\\b"); break;
                    case '\f': result.Append("\\f"); break;
                    case '\n': result.Append("\\n"); break;
                    case '\r': result.Append("\\r"); break;
                    case '\t': result.Append("\\t"); break;
                    default:
                        if (c < 32)
                        {
                            result.Append("\\u");
                            result.Append(((int)c).ToString("x4", CultureInfo.InvariantCulture));
                        }
                        else
                        {
                            result.Append(c);
                        }
                        break;
                }
            }
            return result.ToString();
        }

        private static bool HasAllPrefix(string value)
        {
            if (!value.StartsWith("/all", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }
            return value.Length == 4 || char.IsWhiteSpace(value[4]);
        }

        private static void ReportProgress(Action<int, string> progress, int value, string text)
        {
            if (progress != null)
            {
                progress(Math.Max(0, Math.Min(100, value)), text);
            }
        }

        private sealed class CacheCandidate
        {
            public string Path;
            public string Source;
            public DateTime FileWriteTime;
        }

        private sealed class CompressionCandidate
        {
            public string Path;
            public long Length;
            public int PadLength;
        }
    }
}
