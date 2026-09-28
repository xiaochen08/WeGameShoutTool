# WeGame 喊话自定义工具 V5 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 在现有 V4 便携式 WinForms 工具上增加安全模板库、谐音变体检查、WeGame 状态与版本检测、持久化日志、全局范围切换、整套/混合/单行随机，并生成可独立运行的 V5 包。

**Architecture:** 保留 `Patcher` 与本机 27777 同步服务，新增彼此独立的安全策略、模板库、运行状态检测和持久化组件。界面只负责组织交互；所有可测试规则暴露为无 UI 的静态或小型对象接口。模板库由构建期生成器产生 UTF-8 JSON，运行时只读并再次校验。

**Tech Stack:** .NET Framework 4.x、C#、WinForms、`System.Web.Script.Serialization.JavaScriptSerializer`、Windows Registry/Process APIs、现有 7-Zip 等长 gzip 补丁流程。

**Repository note:** `L:\WeGameShoutTool` 当前不是 Git 仓库，因此本计划不创建分支、worktree 或提交；每批用测试结果、文件清单、版本化 ZIP 和 SHA-256 留证。

---

## File map

- Create `L:\WeGameShoutTool\src\SafetyTextPolicy.cs`: 文本归一化、风险类别检测、整批安全校验。
- Create `L:\WeGameShoutTool\src\TemplateLibrary.cs`: 模板 JSON 模型、载入校验、固定整套与随机选择。
- Create `L:\WeGameShoutTool\src\WeGameDetector.cs`: 运行状态、可执行路径和文件版本检测。
- Create `L:\WeGameShoutTool\src\RuntimeLogger.cs`: 文档目录日志和状态 JSON 持久化。
- Create `L:\WeGameShoutTool\tools\GenerateTemplateLibrary.cs`: 确定性生成 50×10×10 原创安全模板。
- Create `L:\WeGameShoutTool\data\templates.v5.json`: 运行时只读的 5000 条模板。
- Create `L:\WeGameShoutTool\data\safety-rules.v1.json`: 风险类别及归一化变体规则，不包含规避建议。
- Modify `L:\WeGameShoutTool\src\Program.cs`: V5 状态区、模板控制区、全局范围、单行随机、日志和安全提示。
- Modify `L:\WeGameShoutTool\src\Patcher.cs`: 写入前安全门禁、状态阶段命名和公共路径辅助方法。
- Modify `L:\WeGameShoutTool\tests\SourcePatchTests.cs`: V4 回归及 V5 单元/集成检查。
- Modify `L:\WeGameShoutTool\dist\使用说明.txt`: V5 使用、安全边界和本地文件说明。
- Create `L:\WeGameShoutTool\WeGame喊话自定义工具-portable-v5.zip`: 新版本便携包，不覆盖 V4。

### Task 1: 安全文本策略

**Files:**
- Create: `L:\WeGameShoutTool\src\SafetyTextPolicy.cs`
- Create: `L:\WeGameShoutTool\data\safety-rules.v1.json`
- Test: `L:\WeGameShoutTool\tests\SourcePatchTests.cs`

- [x] **Step 1: Write failing reflection tests**

Add tests for these exact public methods:

```csharp
Type safetyType = assembly.GetType("WeGameShoutTool.SafetyTextPolicy", false);
MethodInfo normalize = safetyType.GetMethod("NormalizeForTesting", BindingFlags.Public | BindingFlags.Static);
MethodInfo checkText = safetyType.GetMethod("CheckTextForTesting", BindingFlags.Public | BindingFlags.Static);
MethodInfo checkAll = safetyType.GetMethod("CheckAllForTesting", BindingFlags.Public | BindingFlags.Static);
Check((string)normalize.Invoke(null, new object[] { "Ａ b １" }) == "abi", "safety normalization folds width case spaces and substitutions");
Check(checkText.Invoke(null, new object[] { "这波有想法" }) == null, "safe gameplay phrase accepted");
Check(checkText.Invoke(null, new object[] { RiskFixture("direct") }) != null, "direct risky wording rejected");
Check(checkText.Invoke(null, new object[] { RiskFixture("homophone") }) != null, "homophone variant rejected");
Check(checkText.Invoke(null, new object[] { RiskFixture("split") }) != null, "split variant rejected");
```

`RiskFixture` returns development-only inputs from encoded test fixtures; test output must print only the risk category, never the raw fixture.

- [x] **Step 2: Compile and verify the tests fail because the type is absent**

Run the Framework64 compiler against all `src\*.cs`, then compile the test executable and run it.

Expected: existing V4 checks pass; V5 check reports `SafetyTextPolicy` missing.

- [x] **Step 3: Implement the safety policy**

Implement this stable API and immutable result shape:

```csharp
public sealed class SafetyFinding
{
    public int LineNumber;
    public string Category;
}

public static string NormalizeForTesting(string value);
public static string CheckTextForTesting(string value);
public static SafetyFinding CheckAllForTesting(string[] phrases);
public static void LoadRules(string path);
public static bool RulesReady { get; }
```

Normalization must use Unicode FormKC, lowercase invariant, full/half-width folding, removal of whitespace and separator punctuation, and explicit rule-file mappings for pinyin, initials, number/letter substitutions, common same/near-sound forms, split forms, and traditional variants. `CheckTextForTesting` returns only a category such as `人身攻击` or `暴力威胁`; it never returns the matched text or a replacement suggestion.

- [x] **Step 4: Run safety tests**

Expected: safe gameplay fixtures pass; direct, homophone, split, alphanumeric, and traditional fixtures are rejected; malformed/missing rule files leave `RulesReady == false`.

### Task 2: 固定模板库与生成器

**Files:**
- Create: `L:\WeGameShoutTool\src\TemplateLibrary.cs`
- Create: `L:\WeGameShoutTool\tools\GenerateTemplateLibrary.cs`
- Create: `L:\WeGameShoutTool\data\templates.v5.json`
- Test: `L:\WeGameShoutTool\tests\SourcePatchTests.cs`

- [x] **Step 1: Write failing library tests**

Test this API:

```csharp
TemplateLibrary library = TemplateLibrary.Load(templatePath, SafetyTextPolicy.CheckTextForTesting);
Check(library.ThemeCount == 50, "template library has 50 themes");
Check(library.PackCount == 500, "template library has 500 packs");
Check(library.PhraseCount == 5000, "template library has 5000 phrases");
Check(library.AllPhrases.Distinct().Count() == 5000, "template phrases globally unique");
Check(library.GetPack(0, 0).SequenceEqual(library.GetPack(0, 0)), "fixed pack order deterministic");
Check(library.GetMixedRandom(10, new Random(7)).Distinct().Count() == 10, "mixed random unique");
```

Also assert every body is 1–10 characters, contains no punctuation except `？`, and passes `SafetyTextPolicy`.

- [x] **Step 2: Verify tests fail for missing library**

Expected: failure names identify only the absent `TemplateLibrary` API.

- [x] **Step 3: Implement deterministic generator and loader**

Use these models:

```csharp
public sealed class TemplateTheme { public string Name; public List<TemplatePack> Packs; }
public sealed class TemplatePack { public string Name; public List<string> Lines; }
public sealed class TemplateLibrary
{
    public static TemplateLibrary Load(string path, Func<string,string> safetyCheck);
    public string[] GetPack(int themeIndex, int packIndex);
    public string[] GetRandomPack(Random random);
    public string[] GetMixedRandom(int count, Random random);
    public string GetRandomLine(Random random, IEnumerable<string> excluded);
}
```

The generator may combine only curated game-state atoms. It must stop without writing output unless the final candidate set is exactly 50 themes, 10 packs per theme, 10 lines per pack, globally unique, 1–10 characters, punctuation-safe, and safety-policy clean.

- [x] **Step 4: Generate and validate JSON**

Compile and run `GenerateTemplateLibrary.exe` with explicit input/output paths. Expected validator summary: `themes=50 packs=500 phrases=5000 unique=5000 unsafe=0`.

### Task 3: WeGame 状态与版本检测

**Files:**
- Create: `L:\WeGameShoutTool\src\WeGameDetector.cs`
- Test: `L:\WeGameShoutTool\tests\SourcePatchTests.cs`

- [x] **Step 1: Write failing deterministic detector tests**

```csharp
WeGameStatus status = WeGameDetector.DetectForTesting(
    new string[] { @"X:\WeGame\wegame.exe" },
    new string[] { @"L:\Program Files (x86)\WeGame\wegame.exe" });
Check(status.ExecutablePath.EndsWith("wegame.exe"), "detector selects verified executable");
Check(!string.IsNullOrEmpty(status.Version) || status.Version == "未知", "detector never invents version");
```

- [x] **Step 2: Implement ordered detection**

```csharp
public sealed class WeGameStatus
{
    public bool IsRunning;
    public string ExecutablePath;
    public string Version;
}

public static WeGameStatus Detect();
public static WeGameStatus DetectForTesting(string[] processPaths, string[] candidatePaths);
```

Detection order is process path, uninstall registry, standard paths, then confirmed `L:\Program Files (x86)\WeGame\wegame.exe`. Catch protected-process and registry access failures and return `未知` instead of guessing.

- [x] **Step 3: Run detector tests and a read-only live snapshot**

Expected: tests pass; live result reports running/not running and a real file version or `未知`.

### Task 4: 持久化日志与工具状态

**Files:**
- Create: `L:\WeGameShoutTool\src\RuntimeLogger.cs`
- Test: `L:\WeGameShoutTool\tests\SourcePatchTests.cs`

- [x] **Step 1: Write failing persistence tests**

```csharp
RuntimeLogger logger = new RuntimeLogger(tempLogPath);
logger.Write("信息", "工具启动");
Check(File.ReadAllText(tempLogPath, Encoding.UTF8).Contains("[信息] 工具启动"), "runtime log persisted");
ToolStateStore.Save(tempStatePath, new ToolState { ThemeIndex = 2, PackIndex = 3 });
Check(ToolStateStore.Load(tempStatePath).PackIndex == 3, "tool state round trips");
```

- [x] **Step 2: Implement append-only logger and state store**

```csharp
public sealed class RuntimeLogger { public RuntimeLogger(string path); public void Write(string level, string message); }
public sealed class ToolState { public int ThemeIndex; public int PackIndex; }
public static class ToolStateStore { public static ToolState Load(string path); public static void Save(string path, ToolState state); }
```

Use UTF-8 without BOM. Corrupt state returns defaults and never overwrites `喊话内容.txt`. Log failures surface through an event/callback but do not block phrase persistence.

- [x] **Step 3: Run persistence tests**

Expected: append, UTF-8 Chinese, state round trip, and corrupt-state fallback pass.

### Task 5: 批量范围与安全写入门禁

**Files:**
- Modify: `L:\WeGameShoutTool\src\Patcher.cs`
- Test: `L:\WeGameShoutTool\tests\SourcePatchTests.cs`

- [x] **Step 1: Write failing atomic-operation tests**

```csharp
MethodInfo applyAll = patcher.GetMethod("ApplyAudienceToAllForTesting", BindingFlags.Public | BindingFlags.Static);
string[] switched = (string[])applyAll.Invoke(null, new object[] { tenSafeLines, true });
Check(switched.All(x => x.StartsWith("/all ")), "global all applies every row");
Check(Throws(applyAll, oneOverLimit, true), "global all rejects atomically");
Check(tenSafeLines.SequenceEqual(originalCopy), "global operation does not mutate source");
```

- [x] **Step 2: Implement atomic helpers and patch preflight**

Add:

```csharp
public static string[] ApplyAudienceToAllForTesting(string[] phrases, bool allAudience);
public static string GetToolDirectoryForTesting(string documentsDirectory);
```

`ApplyToCurrentWeGame` must invoke the safety batch checker before cache scanning. A failed safety check reports the row and category, keeps progress below 100, and performs no backup or write.

- [x] **Step 3: Run all patcher regression tests**

Expected: existing exact-length patch, server CORS, reorder, cache, and audience tests stay green; new atomic and safety-gate tests pass.

### Task 6: V5 WinForms integration

**Files:**
- Modify: `L:\WeGameShoutTool\src\Program.cs`

- [x] **Step 1: Split UI responsibilities without changing patch/server behavior**

Add fields for `TemplateLibrary`, `SafetyTextPolicy`, `RuntimeLogger`, `ToolStateStore`, a single UI-thread `Random`, the 2-second status timer, theme/pack controls, global audience buttons, and ten row-random buttons. Keep `PhraseServer` behavior and port unchanged.

- [x] **Step 2: Build the top status and template controls**

Render exactly:

```text
WeGame：运行中 / 未运行
当前版本：真实版本 / 未知
程序位置：真实路径 / 未找到
主题 [下拉]  阵容 [下拉]  [整套填入] [随机整套] [混合随机10条]
[全部设为所有人] [全部设为队伍]  安全检查：通过 / 需修改 / 规则不可用
```

Timer refreshes every two seconds and writes a log entry only when running state, version, or path changes.

- [x] **Step 3: Add row random and batch fill interactions**

Every row becomes `拖动 | 序号 | 范围 | 文本框 | 随机`. Template operations change only message bodies, preserve each row audience, update all ten controls under `suppressPhraseEvents`, then invoke exactly one `PersistDraft`.

- [x] **Step 4: Add global audience and safety feedback**

Global audience buttons call the atomic helper and perform one UI replacement plus one save. Autosave retains drafts even when unsafe, but `currentJson` becomes null and the apply button is disabled until all ten lines pass local safety checks.

- [x] **Step 5: Wire persistent logs and state**

Every existing `AppendLog` call writes both the UI and `运行日志.txt` with `信息/成功/警告/失败`; template selection saves `工具状态.json`. Form close logs shutdown, stops timers, and then stops the local server.

- [x] **Step 6: Compile and launch a smoke-test copy**

Expected: no compiler warnings promoted to errors; form opens, controls fit at 100% and 125% Windows scaling, drag and random buttons remain clickable, and no existing V4 cache is overwritten during the smoke test.

### Task 7: End-to-end automated verification

**Files:**
- Modify: `L:\WeGameShoutTool\tests\SourcePatchTests.cs`

- [x] **Step 1: Run the full executable-backed suite**

Compile production EXE, compile tests, then execute tests against that exact EXE.

Expected final line: `ALL TESTS PASSED`.

- [x] **Step 2: Validate shipped data independently**

Run the template validator against the distribution copy. Expected: exact counts, global uniqueness, length and punctuation constraints, and zero local safety findings.

- [x] **Step 3: Run read-only package preflight**

Verify the target package file list, that `WeGame` is not forcibly terminated, that no account/token/cookie paths are read, and that the application does not inject into League or anti-cheat processes.

### Task 8: V5 documentation and portable package

**Files:**
- Modify: `L:\WeGameShoutTool\dist\使用说明.txt`
- Create: `L:\WeGameShoutTool\WeGame喊话自定义工具-portable-v5.zip`

- [x] **Step 1: Update the user guide**

Document the five template actions, global audience buttons, drag-save behavior, three Documents files, status/version meaning, safety check limitations, requirement to exit WeGame only while applying the patch, and the fact that the tool never promises platform acceptance.

- [x] **Step 2: Build the distribution without overwriting V4 archives**

Package `dist\WeGame喊话自定义工具.exe`, `dist\data\templates.v5.json`, `dist\data\safety-rules.v1.json`, `dist\runtime\7z.exe`, `dist\runtime\7z.dll`, `dist\runtime\License.txt`, and `dist\使用说明.txt` into the new V5 ZIP.

- [x] **Step 3: Verify the exact artifact**

List ZIP entries, test extraction to a new temporary folder, rerun tests against the extracted EXE, and calculate SHA-256 for both EXE and ZIP.

- [x] **Step 4: Record real-user acceptance gates**

Keep these explicitly pending until the user tests them: current WeGame page fetches the local service, WeGame account save succeeds, League receives the selected team/all message, and no current server-side text filter rejects the chosen safe template.

### Post-acceptance correction: 对手向模板

- [x] Replace teammate-command suffixes with opponent-facing, game-only teasing phrases.
- [x] Keep the shipped program behavior unchanged and replace template text data only.
- [x] Preserve the existing manual “队伍/所有人” and global audience buttons.
- [x] Add regression checks for the first opponent-facing fixed pack.
