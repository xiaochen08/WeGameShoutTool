using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace WeGameShoutTool
{
    internal sealed class ShoutFormV5 : Form
    {
        private const bool TemplateDetailsExpandedByDefault = false;
        private readonly TextBox[] phraseBoxes = new TextBox[10];
        private readonly ComboBox[] audienceBoxes = new ComboBox[10];
        private readonly Button[] rowRandomButtons = new Button[10];
        private readonly Random random = new Random();
        private readonly ToolTip toolTip = new ToolTip();
        private readonly System.Windows.Forms.Timer autoSaveTimer = new System.Windows.Forms.Timer();
        private readonly System.Windows.Forms.Timer statusTimer = new System.Windows.Forms.Timer();
        private readonly ComboBox templateSourceBox = new ComboBox();
        private readonly ComboBox themeBox = new ComboBox();
        private readonly ComboBox packBox = new ComboBox();
        private readonly List<Control> templateDetailControls = new List<Control>();
        private readonly List<Control> templateCompactActionControls = new List<Control>();
        private Control[] contentControlsBelowTemplate = new Control[0];
        private Panel templatePanel;
        private readonly Button templateDetailsButton = new Button();
        private readonly Button fillPackButton = new Button();
        private readonly Button randomPackButton = new Button();
        private readonly Button mixedRandomButton = new Button();
        private readonly Button importTemplateButton = new Button();
        private readonly Button importShareButton = new Button();
        private readonly Button exportShareButton = new Button();
        private readonly Button allAudienceButton = new Button();
        private readonly Button teamAudienceButton = new Button();
        private readonly Button saveButton = new Button();
        private readonly Button applyButton = new Button();
        private readonly Button exportDiagnosisButton = new Button();
        private readonly Button openLogButton = new Button();
        private readonly Label toolVersionLabel = new Label();
        private readonly Label weGameStateLabel = new Label();
        private readonly Label weGameVersionLabel = new Label();
        private readonly Label assistantVersionLabel = new Label();
        private readonly Label compatibilityStatusLabel = new Label();
        private readonly Label weGamePathLabel = new Label();
        private readonly Label administratorStatusLabel = new Label();
        private readonly Button restartAsAdministratorButton = new Button();
        private readonly Label workflowStatus = new Label();
        private readonly Label serverStatus = new Label();
        private readonly Label cacheStatus = new Label();
        private readonly Label safetyStatus = new Label();
        private readonly Label progressStatus = new Label();
        private readonly ProgressBar patchProgress = new ProgressBar();
        private readonly TextBox logBox = new TextBox();
        private readonly PhraseServer server;
        private readonly string toolDirectory;
        private readonly string phraseFile;
        private readonly string customTemplateFile;
        private readonly string legacyPhraseFile;
        private readonly string stateFile;
        private readonly RuntimeLogger logger;
        private TemplateLibrary templateLibrary;
        private string selectedTemplateSource = "builtin";
        private bool templateDetailsExpanded = true;
        private bool suppressPhraseEvents;
        private bool suppressTemplateEvents;
        private bool statusRefreshInProgress;
        private bool patchBusy;
        private bool hasSeenWeGameRunningThisSession;
        private bool hasConfirmedContentThisSession;
        private bool patchAppliedThisSession;
        private bool initialStatusChecked;
        private volatile string currentJson;
        private string lastWeGameSignature;
        private string lastCompatibilityAlertSignature;
        private int lastObservedSyncResultCode = int.MinValue;
        private int lastObservedPhraseRequestCount;
        private int lastLoggedProgressValue = -1;
        private string lastLoggedProgressText;
        private string lastDiagnosisReport;
        private int currentCompatibilityCode;
        private readonly bool runningAsAdministrator;
        private WeGameStatus currentWeGameStatus = new WeGameStatus { IsRunning = false, Version = "未知" };

        public ShoutFormV5()
        {
            runningAsAdministrator = PrivilegeManager.IsAdministrator();
            Text = "WeGame 喊话自定义工具 V" + ToolCompatibility.ToolVersion;
            StartPosition = FormStartPosition.CenterScreen;
            ClientSize = new Size(1000, 970);
            MinimumSize = new Size(920, 880);
            AutoScroll = true;
            Font = new Font("Microsoft YaHei UI", 9F);

            BuildHeader();
            BuildStatusPanel();
            BuildTemplatePanel();
            BuildPhraseRows();
            BuildBottomPanel();
            contentControlsBelowTemplate = GetContentControlsBelowTemplate();
            SetTemplateDetailsExpanded(TemplateDetailsExpandedByDefault);

            string documents = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            toolDirectory = Patcher.GetToolDirectoryForTesting(documents);
            phraseFile = Patcher.GetLocalPhraseFileForTesting(documents);
            customTemplateFile = Path.Combine(toolDirectory, "自定义模板.json");
            stateFile = Path.Combine(toolDirectory, "工具状态.json");
            legacyPhraseFile = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "喊话内容.txt");
            logger = new RuntimeLogger(Path.Combine(toolDirectory, "运行日志.txt"));
            logger.FailureSink = delegate(string message) { AppendUiLog("失败", message); };

            LoadSafetyAndTemplates();
            LoadPhrases();

            autoSaveTimer.Interval = 500;
            autoSaveTimer.Tick += AutoSaveTimerTick;
            statusTimer.Interval = 2000;
            statusTimer.Tick += StatusTimerTick;
            server = new PhraseServer(27777, delegate { return currentJson; });
            Shown += FormShown;
            FormClosed += FormClosedHandler;
        }

        private void BuildHeader()
        {
            Label title = new Label
            {
                Text = "WeGame 喊话自定义工具 V" + ToolCompatibility.ToolVersion,
                Font = new Font(Font.FontFamily, 18F, FontStyle.Bold),
                AutoSize = true,
                Location = new Point(20, 14)
            };
            Controls.Add(title);

            LinkLabel signature = new LinkLabel
            {
                Text = WorkflowGuide.ProductSignature,
                TextAlign = ContentAlignment.MiddleRight,
                LinkColor = Color.FromArgb(55, 95, 155),
                ActiveLinkColor = Color.FromArgb(35, 70, 125),
                LinkBehavior = LinkBehavior.HoverUnderline,
                Location = new Point(720, 18),
                Size = new Size(250, 24),
                Anchor = AnchorStyles.Top | AnchorStyles.Right
            };
            signature.LinkClicked += ProductSignatureLinkClicked;
            toolTip.SetToolTip(signature, "打开B站主页");
            Controls.Add(signature);

            Label description = new Label
            {
                Text = "打开一次 WeGame 辅助设置后，工具会持续强制同步当前喊话；游戏期间请保持工具运行。",
                AutoSize = true,
                ForeColor = Color.FromArgb(70, 70, 70),
                Location = new Point(23, 51)
            };
            Controls.Add(description);

            Label notice = new Label
            {
                Text = "本地安全检查采用保守规则，不代表 LOL 官方最终判定，也不会提供敏感词绕过。",
                AutoSize = true,
                ForeColor = Color.FromArgb(180, 75, 25),
                Location = new Point(23, 75)
            };
            Controls.Add(notice);
        }

        private void BuildStatusPanel()
        {
            Panel panel = new Panel
            {
                Location = new Point(20, 102),
                Size = new Size(950, 108),
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
                BorderStyle = BorderStyle.FixedSingle
            };
            Controls.Add(panel);

            ConfigureStatusLabel(toolVersionLabel, "工具版本：V" + ToolCompatibility.ToolVersion, new Point(12, 8), 130);
            ConfigureStatusLabel(weGameStateLabel, "WeGame：检测中", new Point(150, 8), 130);
            ConfigureStatusLabel(weGameVersionLabel, "WeGame版本：检测中", new Point(290, 8), 210);
            ConfigureStatusLabel(assistantVersionLabel, "助手版本：检测中", new Point(510, 8), 210);
            ConfigureStatusLabel(compatibilityStatusLabel, "版本校验：检测中", new Point(730, 8), 205);
            ConfigureStatusLabel(administratorStatusLabel, runningAsAdministrator ? "权限：管理员" : "权限：不是管理员", new Point(560, 35), 150);
            administratorStatusLabel.ForeColor = runningAsAdministrator ? Color.FromArgb(35, 130, 65) : Color.Firebrick;
            restartAsAdministratorButton.Text = "以管理员身份重新打开";
            restartAsAdministratorButton.Location = new Point(720, 31);
            restartAsAdministratorButton.Size = new Size(215, 28);
            restartAsAdministratorButton.Visible = PrivilegeManager.ShouldShowRelaunchButton(runningAsAdministrator);
            restartAsAdministratorButton.Click += RestartAsAdministratorClick;
            ConfigureStatusLabel(weGamePathLabel, "程序位置：检测中", new Point(12, 35), 535);
            panel.Controls.Add(toolVersionLabel);
            panel.Controls.Add(weGameStateLabel);
            panel.Controls.Add(weGameVersionLabel);
            panel.Controls.Add(assistantVersionLabel);
            panel.Controls.Add(compatibilityStatusLabel);
            panel.Controls.Add(administratorStatusLabel);
            panel.Controls.Add(restartAsAdministratorButton);
            panel.Controls.Add(weGamePathLabel);

            workflowStatus.Text = "正在检查 WeGame 状态。";
            workflowStatus.Location = new Point(8, 61);
            workflowStatus.Size = new Size(932, 38);
            workflowStatus.Padding = new Padding(8, 5, 8, 5);
            workflowStatus.BorderStyle = BorderStyle.FixedSingle;
            workflowStatus.AutoEllipsis = true;
            panel.Controls.Add(workflowStatus);
        }

        private static void ConfigureStatusLabel(Label label, string text, Point location, int width)
        {
            label.Text = text;
            label.Location = location;
            label.Size = new Size(width, 22);
            label.AutoEllipsis = true;
        }

        private void BuildTemplatePanel()
        {
            Panel panel = new Panel
            {
                Location = new Point(20, 220),
                Size = new Size(950, 84),
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
                BorderStyle = BorderStyle.FixedSingle
            };
            templatePanel = panel;
            Controls.Add(panel);

            Label sourceLabel = new Label { Text = "模板库", AutoSize = true, Location = new Point(10, 13) };
            panel.Controls.Add(sourceLabel);
            templateDetailControls.Add(sourceLabel);
            templateSourceBox.DropDownStyle = ComboBoxStyle.DropDownList;
            templateSourceBox.Location = new Point(58, 9);
            templateSourceBox.Size = new Size(105, 28);
            templateSourceBox.SelectedIndexChanged += TemplateSourceChanged;
            panel.Controls.Add(templateSourceBox);
            templateDetailControls.Add(templateSourceBox);

            Label themeLabel = new Label { Text = "主题", AutoSize = true, Location = new Point(172, 13) };
            panel.Controls.Add(themeLabel);
            templateDetailControls.Add(themeLabel);
            themeBox.DropDownStyle = ComboBoxStyle.DropDownList;
            themeBox.Location = new Point(207, 9);
            themeBox.Size = new Size(115, 28);
            themeBox.SelectedIndexChanged += ThemeChanged;
            panel.Controls.Add(themeBox);
            templateDetailControls.Add(themeBox);

            Label packLabel = new Label { Text = "整套", AutoSize = true, Location = new Point(331, 13) };
            panel.Controls.Add(packLabel);
            templateDetailControls.Add(packLabel);
            packBox.DropDownStyle = ComboBoxStyle.DropDownList;
            packBox.Location = new Point(366, 9);
            packBox.Size = new Size(100, 28);
            packBox.SelectedIndexChanged += PackChanged;
            panel.Controls.Add(packBox);
            templateDetailControls.Add(packBox);

            ConfigureButton(fillPackButton, "填入整套", new Point(477, 7), new Size(86, 32), FillPackClick);
            ConfigureButton(randomPackButton, "随机整套", new Point(570, 7), new Size(90, 32), RandomPackClick);
            ConfigureButton(mixedRandomButton, "混合随机10条", new Point(667, 7), new Size(125, 32), MixedRandomClick);
            templateDetailControls.Add(fillPackButton);
            templateDetailControls.Add(randomPackButton);
            templateDetailControls.Add(mixedRandomButton);
            ConfigureButton(importShareButton, "导入分享方案", new Point(10, 45), new Size(118, 30), ImportShareClick);
            ConfigureButton(exportShareButton, "导出当前方案", new Point(136, 45), new Size(118, 30), ExportShareClick);
            ConfigureButton(importTemplateButton, "导入模板库", new Point(262, 45), new Size(132, 30), ImportTemplateClick);
            ConfigureButton(allAudienceButton, "全部设为所有人", new Point(404, 45), new Size(145, 30), AllAudienceClick);
            ConfigureButton(teamAudienceButton, "全部设为队伍", new Point(559, 45), new Size(135, 30), TeamAudienceClick);
            ConfigureButton(templateDetailsButton, "收起", new Point(892, 45), new Size(48, 30), TemplateDetailsClick);
            panel.Controls.Add(fillPackButton);
            panel.Controls.Add(randomPackButton);
            panel.Controls.Add(mixedRandomButton);
            panel.Controls.Add(importShareButton);
            panel.Controls.Add(exportShareButton);
            panel.Controls.Add(importTemplateButton);
            panel.Controls.Add(allAudienceButton);
            panel.Controls.Add(teamAudienceButton);
            panel.Controls.Add(templateDetailsButton);
            templateCompactActionControls.Add(importShareButton);
            templateCompactActionControls.Add(exportShareButton);
            templateCompactActionControls.Add(importTemplateButton);
            templateCompactActionControls.Add(allAudienceButton);
            templateCompactActionControls.Add(teamAudienceButton);
            templateCompactActionControls.Add(templateDetailsButton);

            safetyStatus.Text = "安全检查：准备中";
            safetyStatus.AutoSize = false;
            safetyStatus.AutoEllipsis = true;
            safetyStatus.Size = new Size(174, 22);
            safetyStatus.Location = new Point(710, 51);
            panel.Controls.Add(safetyStatus);
            toolTip.SetToolTip(templateDetailsButton, "展开或收起模板来源、主题与整套选择；分享、频道和单条随机操作不受影响。");
        }

        private Control[] GetContentControlsBelowTemplate()
        {
            List<Control> controlsBelow = new List<Control>();
            foreach (Control control in Controls)
            {
                if (control.Top >= 304)
                {
                    controlsBelow.Add(control);
                }
            }
            return controlsBelow.ToArray();
        }

        private void SetTemplateDetailsExpanded(bool expanded)
        {
            if (templateDetailsExpanded == expanded)
            {
                return;
            }
            templateDetailsExpanded = expanded;
            ApplyTemplatePanelLayout(
                templatePanel,
                templateDetailControls.ToArray(),
                templateCompactActionControls.ToArray(),
                safetyStatus,
                templateDetailsButton,
                contentControlsBelowTemplate,
                expanded);
            toolTip.SetToolTip(templateDetailsButton, expanded
                ? "收起模板来源、主题与整套选择；分享、频道和单条随机操作不受影响。"
                : "展开模板来源、主题与整套选择；分享、频道和单条随机操作不受影响。");
        }

        private static void ApplyTemplatePanelLayout(Panel panel, Control[] detailControls, Control[] compactActionControls, Label safetyLabel, Button detailsButton, Control[] contentControls, bool expanded)
        {
            const int expandedPanelHeight = 84;
            const int collapsedPanelHeight = 46;
            const int expandedActionTop = 45;
            const int collapsedActionTop = 7;
            const int contentOffset = 38;
            int contentDelta = expanded ? contentOffset : -contentOffset;

            panel.Height = expanded ? expandedPanelHeight : collapsedPanelHeight;
            foreach (Control control in detailControls)
            {
                control.Visible = expanded;
            }
            foreach (Control control in compactActionControls)
            {
                control.Top = expanded ? expandedActionTop : collapsedActionTop;
            }
            safetyLabel.Top = expanded ? 51 : 13;
            detailsButton.Text = expanded ? "收起" : "展开";
            detailsButton.AccessibleName = expanded ? "收起模板选项" : "展开模板选项";
            detailsButton.AccessibleDescription = expanded
                ? "隐藏模板来源、主题和整套选择控件。"
                : "显示模板来源、主题和整套选择控件。";
            foreach (Control control in contentControls)
            {
                control.Top += contentDelta;
            }
        }

        private void TemplateDetailsClick(object sender, EventArgs e)
        {
            AppendLog("操作", "用户点击：" + (templateDetailsExpanded ? "收起" : "展开") + "模板选项。");
            SetTemplateDetailsExpanded(!templateDetailsExpanded);
        }

        private static void ConfigureButton(Button button, string text, Point location, Size size, EventHandler handler)
        {
            button.Text = text;
            button.Location = location;
            button.Size = size;
            button.Click += handler;
        }

        private void BuildPhraseRows()
        {
            Label hint = new Label
            {
                Text = "拖动左侧 ☰ 调换位置，右侧随机只替换当前一行",
                AutoSize = true,
                ForeColor = Color.FromArgb(70, 105, 145),
                Location = new Point(23, 316)
            };
            Controls.Add(hint);

            int top = 342;
            for (int i = 0; i < phraseBoxes.Length; i++)
            {
                Panel row = new Panel
                {
                    Location = new Point(20, top + (i * 38)),
                    Size = new Size(950, 31),
                    Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
                    AllowDrop = true,
                    Tag = i
                };
                row.DragEnter += PhraseDragEnter;
                row.DragDrop += PhraseDragDrop;
                row.DragLeave += PhraseDragLeave;
                Controls.Add(row);

                Label handle = new Label
                {
                    Text = "☰",
                    Font = new Font(Font.FontFamily, 13F, FontStyle.Bold),
                    TextAlign = ContentAlignment.MiddleCenter,
                    ForeColor = Color.FromArgb(85, 105, 125),
                    Cursor = Cursors.SizeAll,
                    Location = new Point(0, 0),
                    Size = new Size(28, 28),
                    Tag = i
                };
                handle.MouseDown += PhraseHandleMouseDown;
                toolTip.SetToolTip(handle, "拖动调整顺序，松开后自动保存");
                row.Controls.Add(handle);

                Label number = new Label
                {
                    Text = (i + 1).ToString(),
                    TextAlign = ContentAlignment.MiddleRight,
                    Location = new Point(28, 2),
                    Size = new Size(28, 25),
                    Tag = i,
                    AllowDrop = true
                };
                number.DragEnter += PhraseDragEnter;
                number.DragDrop += PhraseDragDrop;
                number.DragLeave += PhraseDragLeave;
                row.Controls.Add(number);

                ComboBox audience = new ComboBox
                {
                    DropDownStyle = ComboBoxStyle.DropDownList,
                    Location = new Point(62, 1),
                    Size = new Size(88, 28),
                    Tag = i
                };
                audience.Items.Add("队伍");
                audience.Items.Add("所有人");
                audience.SelectedIndex = 0;
                audience.SelectedIndexChanged += AudienceChanged;
                audienceBoxes[i] = audience;
                row.Controls.Add(audience);

                Button randomButton = new Button
                {
                    Text = "随机",
                    Size = new Size(72, 28),
                    Location = new Point(870, 1),
                    Anchor = AnchorStyles.Top | AnchorStyles.Right,
                    Tag = i
                };
                randomButton.Click += RowRandomClick;
                rowRandomButtons[i] = randomButton;
                row.Controls.Add(randomButton);

                TextBox box = new TextBox
                {
                    Location = new Point(156, 1),
                    Size = new Size(706, 28),
                    MaxLength = Patcher.MaxPhraseLength,
                    Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
                    AllowDrop = true,
                    Tag = i
                };
                box.DragEnter += PhraseDragEnter;
                box.DragDrop += PhraseDragDrop;
                box.DragLeave += PhraseDragLeave;
                box.TextChanged += PhraseTextChanged;
                phraseBoxes[i] = box;
                row.Controls.Add(box);
            }
        }

        private void BuildBottomPanel()
        {
            saveButton.Text = "① 保存内容并开启同步";
            saveButton.Size = new Size(190, 36);
            saveButton.Location = new Point(20, 734);
            saveButton.Click += SaveButtonClick;
            Controls.Add(saveButton);

            applyButton.Text = "② 安装强力覆盖并重开 WeGame";
            applyButton.Size = new Size(210, 36);
            applyButton.Location = new Point(220, 734);
            applyButton.Click += ApplyButtonClick;
            Controls.Add(applyButton);

            serverStatus.Text = "强力覆盖：准备中";
            serverStatus.AutoSize = true;
            serverStatus.Location = new Point(450, 744);
            Controls.Add(serverStatus);

            progressStatus.Text = "应用进度：等待操作";
            progressStatus.AutoSize = true;
            progressStatus.Location = new Point(20, 782);
            Controls.Add(progressStatus);

            patchProgress.Minimum = 0;
            patchProgress.Maximum = 100;
            patchProgress.Value = 0;
            patchProgress.Location = new Point(20, 806);
            patchProgress.Size = new Size(950, 18);
            patchProgress.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            Controls.Add(patchProgress);

            cacheStatus.Text = "本地缓存：等待输入";
            cacheStatus.AutoSize = false;
            cacheStatus.Size = new Size(650, 22);
            cacheStatus.AutoEllipsis = true;
            cacheStatus.ForeColor = Color.FromArgb(70, 105, 145);
            cacheStatus.Location = new Point(20, 831);
            Controls.Add(cacheStatus);

            exportDiagnosisButton.Text = "导出诊断报告";
            exportDiagnosisButton.Location = new Point(700, 826);
            exportDiagnosisButton.Size = new Size(130, 27);
            exportDiagnosisButton.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            exportDiagnosisButton.Click += ExportDiagnosisButtonClick;
            toolTip.SetToolTip(exportDiagnosisButton, "导出当前诊断报告，并在文件夹中选中该文件");
            Controls.Add(exportDiagnosisButton);

            openLogButton.Text = "打开日志文件夹";
            openLogButton.Location = new Point(840, 826);
            openLogButton.Size = new Size(130, 27);
            openLogButton.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            openLogButton.Click += OpenLogButtonClick;
            toolTip.SetToolTip(openLogButton, "打开运行日志所在文件夹");
            Controls.Add(openLogButton);

            logBox.Multiline = true;
            logBox.ReadOnly = true;
            logBox.ScrollBars = ScrollBars.Vertical;
            logBox.Location = new Point(20, 856);
            logBox.Size = new Size(950, 94);
            logBox.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
            Controls.Add(logBox);
        }

        private void LoadSafetyAndTemplates()
        {
            try
            {
                string rulesPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "data", "safety-rules.v1.json");
                SafetyTextPolicy.LoadRules(rulesPath);
                ToolState state = ToolStateStore.Load(stateFile);
                string requestedSource = ToolStateStore.ResolveTemplateSource(state, File.Exists(customTemplateFile));
                ActivateTemplateSource(requestedSource, state, false);
                AppendLog("成功", "模板库已载入：" + templateLibrary.ThemeCount + "个主题、" + templateLibrary.PackCount + "套、" + templateLibrary.PhraseCount + "条。");
            }
            catch (Exception ex)
            {
                templateLibrary = null;
                SetTemplateControlsEnabled(false);
                safetyStatus.Text = "安全检查：规则不可用";
                safetyStatus.ForeColor = Color.Firebrick;
                AppendException("安全规则或模板库载入失败", ex);
            }
        }

        private void SetTemplateControlsEnabled(bool enabled)
        {
            templateSourceBox.Enabled = enabled && templateSourceBox.Items.Count > 1;
            themeBox.Enabled = enabled;
            packBox.Enabled = enabled;
            fillPackButton.Enabled = enabled;
            randomPackButton.Enabled = enabled;
            mixedRandomButton.Enabled = enabled;
            for (int i = 0; i < rowRandomButtons.Length; i++)
            {
                if (rowRandomButtons[i] != null)
                {
                    rowRandomButtons[i].Enabled = enabled;
                }
            }
        }

        private void PopulateTemplateControls(ToolState state)
        {
            suppressTemplateEvents = true;
            themeBox.Items.Clear();
            packBox.Items.Clear();
            themeBox.Items.AddRange(templateLibrary.GetThemeNames());
            int themeIndex = state == null ? 0 : state.ThemeIndex;
            int packIndex = state == null ? 0 : state.PackIndex;
            themeBox.SelectedIndex = Math.Max(0, Math.Min(themeIndex, themeBox.Items.Count - 1));
            PopulatePackBox(packIndex);
            suppressTemplateEvents = false;
            SetTemplateControlsEnabled(true);
            toolTip.SetToolTip(templateSourceBox, "内置模板不会被导入文件覆盖；“我的模板”保存在文档目录。");
            toolTip.SetToolTip(importTemplateButton, "支持UTF-8 TXT或JSON；成功后只替换“我的模板”，内置模板仍保留。");
            toolTip.SetToolTip(importShareButton, "导入10条分享喊话，回填当前编辑区并自动保存；不会替换模板库。");
            toolTip.SetToolTip(exportShareButton, "导出编辑区当前10条喊话，包含显示的/all前缀。");
        }

        private void PopulateTemplateSourceBox(string source)
        {
            suppressTemplateEvents = true;
            templateSourceBox.Items.Clear();
            templateSourceBox.Items.Add("内置模板");
            if (File.Exists(customTemplateFile))
            {
                templateSourceBox.Items.Add("我的模板");
            }
            templateSourceBox.SelectedIndex = string.Equals(source, "custom", StringComparison.OrdinalIgnoreCase) && templateSourceBox.Items.Count > 1 ? 1 : 0;
            templateSourceBox.Enabled = templateSourceBox.Items.Count > 1;
            suppressTemplateEvents = false;
        }

        private void ActivateTemplateSource(string requestedSource, ToolState state, bool persistSelection)
        {
            string source = string.Equals(requestedSource, "custom", StringComparison.OrdinalIgnoreCase) && File.Exists(customTemplateFile)
                ? "custom"
                : "builtin";
            string bundledTemplatePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "data", "templates.v5.json");
            string selectedPath = source == "custom" ? customTemplateFile : bundledTemplatePath;
            TemplateLibrary loaded;
            try
            {
                loaded = TemplateLibrary.Load(selectedPath, SafetyTextPolicy.CheckTextForTesting);
            }
            catch (Exception customException)
            {
                if (source != "custom")
                {
                    throw;
                }
                AppendLog("警告", "我的模板无法载入，已改用内置模板；原文件保留。 " + customException.Message);
                logger.WriteException("我的模板载入失败，回退内置模板", customException);
                source = "builtin";
                loaded = TemplateLibrary.Load(bundledTemplatePath, SafetyTextPolicy.CheckTextForTesting);
            }

            selectedTemplateSource = source;
            templateLibrary = loaded;
            PopulateTemplateSourceBox(source);
            PopulateTemplateControls(state);
            if (persistSelection || !string.Equals(requestedSource, source, StringComparison.OrdinalIgnoreCase))
            {
                SaveToolState();
            }
        }

        private void TemplateSourceChanged(object sender, EventArgs e)
        {
            if (suppressTemplateEvents || templateSourceBox.SelectedIndex < 0)
            {
                return;
            }
            string requestedSource = templateSourceBox.SelectedIndex == 1 ? "custom" : "builtin";
            if (string.Equals(requestedSource, selectedTemplateSource, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }
            try
            {
                ActivateTemplateSource(requestedSource, ToolStateStore.Load(stateFile), true);
                AppendLog("操作", string.Equals(selectedTemplateSource, "custom", StringComparison.OrdinalIgnoreCase) ? "切换到我的模板库。" : "切换到内置模板库。");
                if (!string.Equals(requestedSource, selectedTemplateSource, StringComparison.OrdinalIgnoreCase))
                {
                    MessageBox.Show(this, "我的模板无法读取，已切回内置模板。原文件没有删除。", "模板库读取失败", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
            }
            catch (Exception ex)
            {
                PopulateTemplateSourceBox(selectedTemplateSource);
                AppendException("切换模板库失败", ex);
                MessageBox.Show(this, ex.Message, "模板库切换失败", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        private void ImportTemplateClick(object sender, EventArgs e)
        {
            using (OpenFileDialog dialog = new OpenFileDialog())
            {
                dialog.Title = "导入并替换我的模板库";
                dialog.Filter = "模板文件 (*.txt;*.json)|*.txt;*.json|TXT模板 (*.txt)|*.txt|JSON模板 (*.json)|*.json";
                dialog.CheckFileExists = true;
                dialog.Multiselect = false;
                if (dialog.ShowDialog(this) != DialogResult.OK)
                {
                    AppendLog("操作", "用户取消导入模板。");
                    return;
                }

                try
                {
                    AppendLog("操作", "开始导入模板：" + dialog.FileName);
                    TemplateLibrary imported = TemplateLibrary.ImportAndReplace(dialog.FileName, customTemplateFile, SafetyTextPolicy.CheckTextForTesting);
                    templateLibrary = imported;
                    selectedTemplateSource = "custom";
                    PopulateTemplateSourceBox(selectedTemplateSource);
                    PopulateTemplateControls(new ToolState());
                    SaveToolState();
                    AppendLog("成功", "模板已导入并替换：" + imported.ThemeCount + "个主题、" + imported.PackCount + "套、" + imported.PhraseCount + "条。");
                    MessageBox.Show(this, "替换成功。新模板已保存到本地，下次启动会自动载入。", "模板已替换", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                catch (Exception ex)
                {
                    AppendException("模板导入失败，当前模板未改变", ex);
                    MessageBox.Show(this, ex.Message + Environment.NewLine + Environment.NewLine + "当前模板没有被替换。", "导入失败", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
            }
        }

        private void ImportShareClick(object sender, EventArgs e)
        {
            if (!SafetyTextPolicy.RulesReady)
            {
                MessageBox.Show(this, "本地安全规则当前不可用，不能导入分享内容。请检查工具文件后重试。", "无法导入", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            using (OpenFileDialog dialog = new OpenFileDialog())
            {
                dialog.Title = "导入10条喊话分享方案";
                dialog.Filter = "喊话分享文件 (*.wgphrases.json)|*.wgphrases.json|JSON文件 (*.json)|*.json";
                dialog.CheckFileExists = true;
                dialog.Multiselect = false;
                if (dialog.ShowDialog(this) != DialogResult.OK)
                {
                    AppendLog("操作", "用户取消导入喊话分享方案。");
                    return;
                }

                try
                {
                    AppendLog("操作", "开始导入喊话分享方案：" + dialog.FileName);
                    string[] imported = PhraseShareFile.Import(dialog.FileName, SafetyTextPolicy.CheckTextForTesting);
                    SetPhraseTexts(imported);
                    PersistDraft(imported, "已导入分享方案并保存到本地文档缓存。");
                    if (currentJson == null)
                    {
                        AppendLog("失败", "分享方案已填入当前编辑区，但文档缓存写入失败；请检查本地缓存状态。");
                        MessageBox.Show(this, "10条喊话已填入，但没有成功保存到本地文档。请先检查下方本地缓存状态。", "本地保存失败", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                        return;
                    }
                    AppendLog("成功", "喊话分享方案已导入，共10条；当前模板库没有改变。");
                    MessageBox.Show(this, "10条喊话已导入并自动保存到本地。", "导入成功", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                catch (Exception ex)
                {
                    AppendException("喊话分享导入失败，当前内容未改变", ex);
                    MessageBox.Show(this, ex.Message + Environment.NewLine + Environment.NewLine + "当前10条内容没有被替换。", "导入失败", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
            }
        }

        private void ExportShareClick(object sender, EventArgs e)
        {
            if (!SafetyTextPolicy.RulesReady)
            {
                MessageBox.Show(this, "本地安全规则当前不可用，不能导出分享内容。请检查工具文件后重试。", "无法导出", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            using (SaveFileDialog dialog = new SaveFileDialog())
            {
                dialog.Title = "导出当前显示的10条喊话";
                dialog.Filter = "喊话分享文件 (*.wgphrases.json)|*.wgphrases.json";
                dialog.DefaultExt = "wgphrases.json";
                dialog.AddExtension = true;
                dialog.OverwritePrompt = true;
                dialog.FileName = "我的喊话方案.wgphrases.json";
                if (dialog.ShowDialog(this) != DialogResult.OK)
                {
                    AppendLog("操作", "用户取消导出喊话分享方案。");
                    return;
                }

                try
                {
                    string[] current = ReadDisplayedPhrases();
                    PhraseShareFile.Export(dialog.FileName, current, SafetyTextPolicy.CheckTextForTesting);
                    AppendLog("成功", "已导出当前显示的10条喊话分享方案：" + dialog.FileName);
                    MessageBox.Show(this, "当前显示的10条喊话已导出。分享文件中包含每行现有的频道前缀。", "导出成功", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                catch (Exception ex)
                {
                    AppendException("喊话分享导出失败", ex);
                    MessageBox.Show(this, ex.Message + Environment.NewLine + Environment.NewLine + "导出失败，未覆盖目标文件。", "导出失败", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
            }
        }

        private void PopulatePackBox(int selectedIndex)
        {
            packBox.Items.Clear();
            if (templateLibrary == null || themeBox.SelectedIndex < 0)
            {
                return;
            }
            packBox.Items.AddRange(templateLibrary.GetPackNames(themeBox.SelectedIndex));
            packBox.SelectedIndex = Math.Max(0, Math.Min(selectedIndex, packBox.Items.Count - 1));
        }

        private void LoadPhrases()
        {
            string sourceFile = File.Exists(phraseFile) ? phraseFile : (File.Exists(legacyPhraseFile) ? legacyPhraseFile : null);
            if (sourceFile == null)
            {
                cacheStatus.Text = "本地缓存：" + phraseFile;
                toolTip.SetToolTip(cacheStatus, phraseFile);
                UpdateSafetyState(ReadPhrases());
                AppendLog("信息", "尚未保存喊话内容，请填写正好10条。");
                return;
            }

            try
            {
                string[] loaded = File.ReadAllLines(sourceFile, Encoding.UTF8);
                string[] lines = new string[phraseBoxes.Length];
                for (int i = 0; i < lines.Length; i++)
                {
                    lines[i] = i < loaded.Length ? loaded[i] : string.Empty;
                }
                SetPhraseTexts(lines);
                if (!string.Equals(sourceFile, phraseFile, StringComparison.OrdinalIgnoreCase))
                {
                    Patcher.SavePhrasesFileForTesting(phraseFile, lines);
                    AppendLog("信息", "已复制迁移旧内容，旧文件继续保留。");
                }
                UpdateDraftState(lines, "已加载");
                AppendLog("成功", "已加载本地喊话内容。");
            }
            catch (Exception ex)
            {
                AppendException("读取喊话内容失败", ex);
            }
        }

        private string[] ReadPhrases()
        {
            string[] result = new string[phraseBoxes.Length];
            for (int i = 0; i < phraseBoxes.Length; i++)
            {
                result[i] = phraseBoxes[i].Text.Trim();
            }
            return result;
        }

        private string[] ReadDisplayedPhrases()
        {
            string[] result = new string[phraseBoxes.Length];
            for (int i = 0; i < phraseBoxes.Length; i++)
            {
                result[i] = phraseBoxes[i].Text;
            }
            return result;
        }

        private void SetPhraseTexts(string[] phrases)
        {
            suppressPhraseEvents = true;
            for (int i = 0; i < phraseBoxes.Length; i++)
            {
                phraseBoxes[i].Text = phrases[i] ?? string.Empty;
            }
            RefreshAudienceSelections();
            suppressPhraseEvents = false;
        }

        private void FormShown(object sender, EventArgs e)
        {
            AppendLog("信息", "工具启动。");
            AppendLog("诊断", "工具版本：V" + ToolCompatibility.ToolVersion + "，文件版本：" + Application.ProductVersion + "，程序路径：" + Application.ExecutablePath);
            AppendLog("诊断", "运行目录：" + AppDomain.CurrentDomain.BaseDirectory + "，数据目录：" + toolDirectory + "，日志文件：" + logger.LogPath);
            AppendLog("诊断", "系统：" + Environment.OSVersion.VersionString + "，64位系统：" + Environment.Is64BitOperatingSystem + "，64位进程：" + Environment.Is64BitProcess);
            AppendLog("诊断", "支持版本：WeGame " + ToolCompatibility.SupportedWeGameVersionsDisplay + "，助手 " + ToolCompatibility.SupportedAssistantVersion);
            AppendLog(runningAsAdministrator ? "成功" : "提示", runningAsAdministrator
                ? "当前已使用管理员权限运行。"
                : "当前不是管理员权限，请点击“以管理员身份重新打开”。");
            try
            {
                server.Start();
                serverStatus.Text = "强力覆盖：等待 WeGame 打开辅助设置";
                serverStatus.ForeColor = Color.FromArgb(130, 80, 20);
                AppendLog("成功", "强力覆盖服务已启动，只监听本机。");
            }
            catch (Exception ex)
            {
                serverStatus.Text = "强力覆盖：启动失败";
                serverStatus.ForeColor = Color.Firebrick;
                AppendException("同步服务启动失败", ex);
            }
            statusTimer.Start();
            StatusTimerTick(this, EventArgs.Empty);
        }

        private void FormClosedHandler(object sender, FormClosedEventArgs e)
        {
            autoSaveTimer.Stop();
            statusTimer.Stop();
            server.Stop();
            AppendLog("信息", "工具关闭。");
        }

        private async void StatusTimerTick(object sender, EventArgs e)
        {
            UpdateStrongOverrideStatus();
            await RefreshWeGameStatusAsync();
        }

        private void UpdateStrongOverrideStatus()
        {
            if (!server.IsRunning)
            {
                return;
            }
            int resultCode = server.LastSyncResultCode;
            DateTime resultUtc = server.LastSyncResultUtc;
            int requestCount = server.PhraseRequestCount;
            if (requestCount != lastObservedPhraseRequestCount)
            {
                lastObservedPhraseRequestCount = requestCount;
                AppendLog("同步", "WeGame 已读取本地喊话接口，累计请求 " + requestCount + " 次。");
            }
            if (resultCode == int.MinValue)
            {
                serverStatus.Text = server.PhraseRequestCount > 0
                    ? "强力覆盖：WeGame 已读取，等待保存结果"
                    : "强力覆盖：等待 WeGame 打开辅助设置";
                serverStatus.ForeColor = Color.FromArgb(130, 80, 20);
                return;
            }

            if (resultCode == 0)
            {
                serverStatus.Text = "强力覆盖：已生效 " + resultUtc.ToLocalTime().ToString("HH:mm:ss");
                serverStatus.ForeColor = Color.FromArgb(35, 130, 65);
            }
            else if (resultCode == Patcher.NoSaveNeededResultCode)
            {
                serverStatus.Text = "强力覆盖：内容已一致 " + resultUtc.ToLocalTime().ToString("HH:mm:ss");
                serverStatus.ForeColor = Color.FromArgb(35, 130, 65);
            }
            else
            {
                serverStatus.Text = "强力覆盖：保存失败，错误码 " + resultCode;
                serverStatus.ForeColor = Color.Firebrick;
            }

            if (resultCode != lastObservedSyncResultCode)
            {
                lastObservedSyncResultCode = resultCode;
                if (resultCode == 0)
                {
                    AppendLog("成功", "WeGame 已确认保存当前喊话，强力覆盖生效。");
                }
                else if (resultCode == Patcher.NoSaveNeededResultCode)
                {
                    AppendLog("同步", "WeGame 当前喊话与工具内容一致，已跳过重复保存。");
                }
                else
                {
                    AppendLog("失败", "WeGame 保存当前喊话失败，错误码：" + resultCode + "。");
                }
            }
        }

        private async Task RefreshWeGameStatusAsync()
        {
            if (statusRefreshInProgress)
            {
                return;
            }
            statusRefreshInProgress = true;
            try
            {
                WeGameStatus next = await Task.Run(delegate { return WeGameDetector.Detect(); });
                if (IsDisposed)
                {
                    return;
                }
                currentWeGameStatus = next;
                if (next.IsRunning)
                {
                    hasSeenWeGameRunningThisSession = true;
                }
                weGameStateLabel.Text = "WeGame：" + (next.IsRunning ? "运行中" : "未运行");
                weGameStateLabel.ForeColor = next.IsRunning ? Color.FromArgb(35, 130, 65) : Color.FromArgb(130, 80, 20);
                weGameVersionLabel.Text = "WeGame版本：" + (string.IsNullOrWhiteSpace(next.Version) ? "未知" : next.Version.Trim());
                assistantVersionLabel.Text = "助手版本：" + (string.IsNullOrWhiteSpace(next.AssistantVersion) ? "未知" : next.AssistantVersion.Trim());
                weGamePathLabel.Text = "程序位置：" + (string.IsNullOrWhiteSpace(next.ExecutablePath) ? "未找到" : next.ExecutablePath);
                toolTip.SetToolTip(weGamePathLabel, next.ExecutablePath ?? "未找到");
                toolTip.SetToolTip(assistantVersionLabel, next.AssistantExecutablePath ?? "未找到助手程序");
                currentCompatibilityCode = ToolCompatibility.Evaluate(next);
                compatibilityStatusLabel.Text = currentCompatibilityCode == 1
                    ? "版本校验：通过"
                    : (currentCompatibilityCode < 0 ? "版本校验：不适用" : "版本校验：无法确认");
                compatibilityStatusLabel.ForeColor = currentCompatibilityCode == 1
                    ? Color.FromArgb(35, 130, 65)
                    : Color.Firebrick;
                string signature = next.Signature();
                if (!string.Equals(lastWeGameSignature, signature, StringComparison.Ordinal))
                {
                    lastWeGameSignature = signature;
                    AppendLog("检测", "WeGame状态变化：" + (next.IsRunning ? "运行中" : "未运行")
                        + "，WeGame版本：" + next.Version
                        + "，WeGame路径：" + (next.ExecutablePath ?? "未找到")
                        + "，助手版本：" + (next.AssistantVersion ?? "未知")
                        + "，助手路径：" + (next.AssistantExecutablePath ?? "未找到")
                        + "，校验结果：" + (currentCompatibilityCode == 1 ? "通过" : (currentCompatibilityCode < 0 ? "不适用" : "无法确认")));
                }
                if (currentCompatibilityCode < 0 && !string.Equals(lastCompatibilityAlertSignature, signature, StringComparison.Ordinal))
                {
                    lastCompatibilityAlertSignature = signature;
                    AppendLog("失败", ToolCompatibility.UnsupportedMessage + " WeGame=" + next.Version + "，助手=" + next.AssistantVersion);
                    MessageBox.Show(this, ToolCompatibility.BuildAlertMessage(next), "版本不适用", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
                UpdateActionAvailability();
                if (!initialStatusChecked)
                {
                    initialStatusChecked = true;
                    if (!next.IsRunning && currentCompatibilityCode >= 0)
                    {
                        MessageBox.Show(
                            this,
                            "目前未检测到 WeGame 正在运行，请先打开 WeGame。\r\n\r\n工具会自动检测。",
                            "请先打开 WeGame",
                            MessageBoxButtons.OK,
                            MessageBoxIcon.Information);
                    }
                }
            }
            catch (Exception ex)
            {
                AppendException("WeGame状态检测失败", ex);
            }
            finally
            {
                statusRefreshInProgress = false;
            }
        }

        private void PhraseTextChanged(object sender, EventArgs e)
        {
            if (suppressPhraseEvents)
            {
                return;
            }
            autoSaveTimer.Stop();
            autoSaveTimer.Start();
            UpdateSafetyState(ReadPhrases());
        }

        private void AutoSaveTimerTick(object sender, EventArgs e)
        {
            autoSaveTimer.Stop();
            string[] phrases = ReadPhrases();
            PersistDraft(phrases, "输入内容已自动缓存。");
            RefreshAudienceSelections();
        }

        private void AudienceChanged(object sender, EventArgs e)
        {
            if (suppressPhraseEvents)
            {
                return;
            }
            ComboBox audience = (ComboBox)sender;
            int index = (int)audience.Tag;
            try
            {
                string updated = Patcher.ApplyAudienceForTesting(phraseBoxes[index].Text, audience.SelectedIndex == 1);
                suppressPhraseEvents = true;
                phraseBoxes[index].Text = updated;
                phraseBoxes[index].SelectionStart = phraseBoxes[index].TextLength;
                suppressPhraseEvents = false;
                PersistDraft(ReadPhrases(), audience.SelectedIndex == 1 ? "第" + (index + 1) + "条已设为所有人。" : "第" + (index + 1) + "条已设为队伍。");
            }
            catch (Exception ex)
            {
                suppressPhraseEvents = true;
                audience.SelectedIndex = Patcher.IsAllAudienceForTesting(phraseBoxes[index].Text) ? 1 : 0;
                suppressPhraseEvents = false;
                AppendException("切换第 " + (index + 1) + " 条发送范围失败", ex);
                MessageBox.Show(this, ex.Message, "无法切换范围", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        private void AllAudienceClick(object sender, EventArgs e)
        {
            AppendLog("操作", "用户点击：全部设为所有人。");
            ApplyAudienceToAll(true);
        }

        private void TeamAudienceClick(object sender, EventArgs e)
        {
            AppendLog("操作", "用户点击：全部设为队伍。");
            ApplyAudienceToAll(false);
        }

        private void ApplyAudienceToAll(bool allAudience)
        {
            try
            {
                string[] updated = Patcher.ApplyAudienceToAllForTesting(ReadPhrases(), allAudience);
                SetPhraseTexts(updated);
                PersistDraft(updated, allAudience ? "10条已全部设为所有人。" : "10条已全部设为队伍。");
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, ex.Message, "批量切换失败", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                AppendException("批量切换范围失败", ex);
            }
        }

        private void RefreshAudienceSelections()
        {
            bool previous = suppressPhraseEvents;
            suppressPhraseEvents = true;
            for (int i = 0; i < audienceBoxes.Length; i++)
            {
                audienceBoxes[i].SelectedIndex = Patcher.IsAllAudienceForTesting(phraseBoxes[i].Text) ? 1 : 0;
            }
            suppressPhraseEvents = previous;
        }

        private void FillPackClick(object sender, EventArgs e)
        {
            AppendLog("操作", "用户点击：整套填入，主题索引 " + themeBox.SelectedIndex + "，阵容索引 " + packBox.SelectedIndex + "。");
            if (templateLibrary == null || themeBox.SelectedIndex < 0 || packBox.SelectedIndex < 0)
            {
                return;
            }
            ApplyTemplateBodies(templateLibrary.GetPack(themeBox.SelectedIndex, packBox.SelectedIndex), "已填入所选固定阵容。");
        }

        private void RandomPackClick(object sender, EventArgs e)
        {
            AppendLog("操作", "用户点击：随机整套。");
            if (templateLibrary != null)
            {
                ApplyTemplateBodies(templateLibrary.GetRandomPack(random), "已随机填入一套固定阵容。");
            }
        }

        private void MixedRandomClick(object sender, EventArgs e)
        {
            AppendLog("操作", "用户点击：混合随机10条。");
            if (templateLibrary != null)
            {
                ApplyTemplateBodies(templateLibrary.GetMixedRandom(10, random), "已从全库混合随机10条。");
            }
        }

        private void RowRandomClick(object sender, EventArgs e)
        {
            if (templateLibrary == null)
            {
                return;
            }
            int index = (int)((Button)sender).Tag;
            AppendLog("操作", "用户点击：随机替换第 " + (index + 1) + " 条。");
            List<string> excluded = new List<string>();
            for (int i = 0; i < phraseBoxes.Length; i++)
            {
                if (i != index)
                {
                    excluded.Add(GetBody(phraseBoxes[i].Text));
                }
            }
            string line = templateLibrary.GetRandomLine(random, excluded);
            string updated = Patcher.ApplyAudienceForTesting(line, audienceBoxes[index].SelectedIndex == 1);
            suppressPhraseEvents = true;
            phraseBoxes[index].Text = updated;
            suppressPhraseEvents = false;
            PersistDraft(ReadPhrases(), "第" + (index + 1) + "条已随机替换。");
        }

        private void ApplyTemplateBodies(string[] bodies, string logMessage)
        {
            string[] updated = new string[phraseBoxes.Length];
            for (int i = 0; i < updated.Length; i++)
            {
                updated[i] = Patcher.ApplyAudienceForTesting(bodies[i], audienceBoxes[i].SelectedIndex == 1);
            }
            SetPhraseTexts(updated);
            PersistDraft(updated, logMessage);
        }

        private static string GetBody(string phrase)
        {
            string value = (phrase ?? string.Empty).Trim();
            return Patcher.IsAllAudienceForTesting(value) ? value.Substring(4).TrimStart() : value;
        }

        private void ThemeChanged(object sender, EventArgs e)
        {
            if (suppressTemplateEvents)
            {
                return;
            }
            suppressTemplateEvents = true;
            PopulatePackBox(0);
            suppressTemplateEvents = false;
            SaveToolState();
        }

        private void PackChanged(object sender, EventArgs e)
        {
            if (!suppressTemplateEvents)
            {
                SaveToolState();
            }
        }

        private void SaveToolState()
        {
            try
            {
                ToolStateStore.Save(stateFile, new ToolState
                {
                    ThemeIndex = Math.Max(0, themeBox.SelectedIndex),
                    PackIndex = Math.Max(0, packBox.SelectedIndex),
                    TemplateSource = selectedTemplateSource
                });
            }
            catch (Exception ex)
            {
                AppendException("工具状态保存失败", ex);
            }
        }

        private void PersistDraft(string[] phrases, string successMessage)
        {
            hasConfirmedContentThisSession = false;
            patchAppliedThisSession = false;
            if (server != null)
            {
                server.ResetSyncResult();
                lastObservedSyncResultCode = int.MinValue;
            }
            try
            {
                Patcher.SavePhrasesFileForTesting(phraseFile, phrases);
                UpdateDraftState(phrases, "已自动保存");
                AppendLog("成功", successMessage);
            }
            catch (Exception ex)
            {
                currentJson = null;
                cacheStatus.Text = "本地缓存：保存失败";
                AppendException("自动缓存失败", ex);
                UpdateActionAvailability();
            }
        }

        private void UpdateDraftState(string[] phrases, string stateText)
        {
            string validation = Patcher.ValidatePhrasesForTesting(phrases);
            string weGameValidation = Patcher.ValidatePhrasesForWeGameForTesting(phrases);
            SafetyFinding finding = SafetyTextPolicy.CheckAllForTesting(phrases);
            currentJson = validation == null && weGameValidation == null && finding == null ? Patcher.BuildPhrasesJson(phrases) : null;
            cacheStatus.Text = validation == null && weGameValidation == null
                ? "本地缓存：" + stateText + " " + DateTime.Now.ToString("HH:mm:ss")
                : "本地缓存：草稿已保存，内容尚未可同步";
            toolTip.SetToolTip(cacheStatus, phraseFile);
            UpdateSafetyState(phrases);
        }

        private void UpdateSafetyState(string[] phrases)
        {
            SafetyFinding finding = SafetyTextPolicy.CheckAllForTesting(phrases);
            if (!SafetyTextPolicy.RulesReady || (finding != null && finding.Category == "安全规则不可用"))
            {
                safetyStatus.Text = "安全检查：规则不可用";
                safetyStatus.ForeColor = Color.Firebrick;
            }
            else if (finding != null)
            {
                safetyStatus.Text = "安全检查：第" + finding.LineNumber + "条需修改（" + finding.Category + "）";
                safetyStatus.ForeColor = Color.Firebrick;
            }
            else
            {
                safetyStatus.Text = "安全检查：通过";
                safetyStatus.ForeColor = Color.FromArgb(35, 130, 65);
            }
            UpdateActionAvailability();
        }

        private bool SavePhrases()
        {
            string[] phrases = ReadPhrases();
            PersistDraft(phrases, "内容已保存到文档缓存。");
            string validation = Patcher.ValidatePhrasesForTesting(phrases);
            if (validation != null)
            {
                AppendLog("提示", "保存校验未通过：" + validation);
                MessageBox.Show(this, validation, "内容尚未可同步", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return false;
            }
            string weGameValidation = Patcher.ValidatePhrasesForWeGameForTesting(phrases);
            if (weGameValidation != null)
            {
                AppendLog("提示", "保存校验未通过：" + weGameValidation);
                MessageBox.Show(this, weGameValidation, "WeGame 内容限制", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return false;
            }
            SafetyFinding finding = SafetyTextPolicy.CheckAllForTesting(phrases);
            if (finding != null)
            {
                AppendLog("提示", "保存时安全检查未通过：第 " + finding.LineNumber + " 条，类别 " + finding.Category + "。");
                MessageBox.Show(this, "第 " + finding.LineNumber + " 条未通过本地安全检查：" + finding.Category + "。", "内容尚未可同步", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return false;
            }
            hasConfirmedContentThisSession = true;
            UpdateActionAvailability();
            return true;
        }

        private void SaveButtonClick(object sender, EventArgs e)
        {
            AppendLog("操作", "用户点击①保存内容并开启同步。");
            if (SavePhrases())
            {
                string nextStep;
                if (!hasSeenWeGameRunningThisSession)
                {
                    nextStep = "保存成功。\r\n\r\n请先打开 WeGame，检测到以后点击②。";
                }
                else if (currentWeGameStatus.IsRunning)
                {
                    nextStep = "保存成功。\r\n\r\n现在点击②，工具会自动处理。";
                }
                else
                {
                    nextStep = "保存成功。\r\n\r\n现在点击②应用。";
                }
                MessageBox.Show(this, nextStep, "保存成功", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
        }

        private async void ApplyButtonClick(object sender, EventArgs e)
        {
            AppendLog("操作", "用户点击②安装强力覆盖。当前状态：管理员=" + runningAsAdministrator
                + "，WeGame运行=" + currentWeGameStatus.IsRunning
                + "，内容已确认=" + hasConfirmedContentThisSession
                + "，版本校验=" + currentCompatibilityCode + "。");
            if (!runningAsAdministrator)
            {
                AppendLog("提示", "应用被阻止：当前不是管理员权限。");
                MessageBox.Show(this, "当前不是管理员权限。\r\n\r\n请点击上方“以管理员身份重新打开”，允许 Windows 授权后再安装强力覆盖。", "需要管理员权限", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            if (currentCompatibilityCode != 1)
            {
                string compatibilityMessage = currentCompatibilityCode < 0
                    ? ToolCompatibility.BuildAlertMessage(currentWeGameStatus)
                    : "无法确认 WeGame 或 WeGame 助手版本。\r\n\r\n请先打开 WeGame；仍无法检测请联系作者。";
                AppendLog("提示", "应用被阻止：版本校验未通过。WeGame=" + currentWeGameStatus.Version + "，助手=" + currentWeGameStatus.AssistantVersion + "。");
                MessageBox.Show(this, compatibilityMessage, "版本校验未通过", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            string blockMessage = WorkflowGuide.GetApplyBlockMessageForTesting(
                currentWeGameStatus.IsRunning,
                hasSeenWeGameRunningThisSession,
                currentJson != null,
                hasConfirmedContentThisSession,
                patchBusy);
            if (blockMessage != null)
            {
                AppendLog("提示", "点击②时当前顺序尚未满足。已显示下一步说明。");
                MessageBox.Show(this, blockMessage, "请按提示操作", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            string weGameExecutable = currentWeGameStatus.ExecutablePath;
            if (string.IsNullOrWhiteSpace(weGameExecutable))
            {
                MessageBox.Show(this, "没有找到 WeGame 程序位置，无法自动关闭和重新打开。", "未找到 WeGame", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            bool closeBeforeApply = currentWeGameStatus.IsRunning;
            string confirmationText = WorkflowGuide.SecuritySoftwareNotice
                + (closeBeforeApply
                    ? "\r\n\r\n点击确定后，工具会自动关闭 WeGame、安装强力覆盖补丁，然后重新打开。"
                    : "\r\n\r\n点击确定后，工具会安装强力覆盖补丁，然后打开 WeGame。");
            DialogResult confirmation = MessageBox.Show(
                this,
                confirmationText,
                "使用前安全提示",
                MessageBoxButtons.OKCancel,
                MessageBoxIcon.Warning);
            if (confirmation != DialogResult.OK)
            {
                AppendLog("操作", "用户在安全软件提示中取消应用，未关闭 WeGame，未写入缓存。");
                return;
            }
            AppendLog("操作", "用户确认安全软件提示并继续。将沿用已验证路径：" + weGameExecutable);
            string[] phrases = ReadPhrases();
            string weGameValidation = Patcher.ValidatePhrasesForWeGameForTesting(phrases);
            if (weGameValidation != null)
            {
                AppendLog("提示", "应用前检查未通过：" + weGameValidation);
                MessageBox.Show(this, weGameValidation, "WeGame 内容限制", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            server.ResetSyncResult();
            lastObservedSyncResultCode = int.MinValue;
            patchBusy = true;
            UpdateActionAvailability();
            UpdatePatchProgress(0, "准备开始");
            AppendLog("信息", "开始检测兼容缓存。");
            bool restartSucceeded = false;
            string restartError = null;
            try
            {
                string path = await Task.Run(delegate
                {
                    Action<string> backgroundReport = delegate(string line) { BeginInvoke((Action)delegate { AppendLog("信息", line); }); };
                    if (closeBeforeApply)
                    {
                        WeGameProcessController.CloseVerifiedProcesses(weGameExecutable, backgroundReport);
                    }
                    string patchedPath = Patcher.ApplyToCurrentWeGame(
                        phrases,
                        weGameExecutable,
                        backgroundReport,
                        delegate(int value, string text) { BeginInvoke((Action)delegate { UpdatePatchProgress(value, text); }); });
                    try
                    {
                        WeGameProcessController.StartVerified(weGameExecutable, backgroundReport);
                        restartSucceeded = true;
                    }
                    catch (Exception restartException)
                    {
                        restartError = restartException.Message;
                        logger.WriteException("自动重新打开 WeGame 失败", restartException);
                    }
                    return patchedPath;
                });
                AppendLog("成功", "应用成功：" + path);
                patchAppliedThisSession = true;
                UpdateActionAvailability();
                if (restartSucceeded)
                {
                    MessageBox.Show(this, "强力覆盖补丁已安装。\r\n\r\n" + WorkflowGuide.CustomOverrideFallbackNotice + "\r\n\r\n请在 WeGame 打开：英雄联盟 → 辅助设置 → 其他辅助。\r\n\r\n工具显示“强力覆盖：已生效”后，再启动游戏。游戏期间请保持本工具运行。", "等待 WeGame 确认", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                else
                {
                    AppendLog("警告", "内容已应用，但 WeGame 自动重开失败：" + restartError);
                    MessageBox.Show(this, "内容已经应用，但 WeGame 没有自动打开。\r\n\r\n请手动打开 WeGame。", "请手动打开 WeGame", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
            }
            catch (Exception ex)
            {
                if (closeBeforeApply && !restartSucceeded)
                {
                    try
                    {
                        WeGameProcessController.StartVerified(weGameExecutable, delegate(string line)
                        {
                            AppendLog("信息", line);
                        });
                        restartSucceeded = true;
                        AppendLog("信息", "应用未完成，WeGame 已重新打开。");
                    }
                    catch (Exception restartException)
                    {
                        restartError = restartException.Message;
                        logger.WriteException("应用失败后的 WeGame 自动重开失败", restartException);
                    }
                }
                patchAppliedThisSession = false;
                progressStatus.ForeColor = Color.Firebrick;
                AppendException("应用强力覆盖失败", ex);
                lastDiagnosisReport = BuildFailureReport(ex, closeBeforeApply, restartSucceeded, restartError);
                cacheStatus.Text = "本地缓存：未修改，已生成失败诊断";
                cacheStatus.ForeColor = Color.Firebrick;
                toolTip.SetToolTip(cacheStatus, "应用未完成。可点击“导出诊断报告”查看完整原因。");
                AppendLog("诊断", lastDiagnosisReport);
                MessageBox.Show(this, lastDiagnosisReport + "\r\n\r\n可点击下方“导出诊断报告”，工具会自动打开保存位置。", "应用失败", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                patchBusy = false;
                UpdateActionAvailability();
            }
            await RefreshWeGameStatusAsync();
        }

        private void ProductSignatureLinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            AppendLog("操作", "用户点击作者主页链接。");
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = WorkflowGuide.ProductHomeUrl,
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                AppendException("B站主页打开失败", ex);
                MessageBox.Show(this, "无法打开B站主页，请检查默认浏览器设置。", "打开失败", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        private void RestartAsAdministratorClick(object sender, EventArgs e)
        {
            AppendLog("操作", "用户点击以管理员身份重新打开。");
            try
            {
                restartAsAdministratorButton.Enabled = false;
                server.Stop();
                if (!PrivilegeManager.EnsureAdministrator())
                {
                    AppendLog("信息", "已请求以管理员身份重新打开工具。");
                    Close();
                }
            }
            catch (Exception ex)
            {
                restartAsAdministratorButton.Enabled = true;
                try
                {
                    server.Start();
                }
                catch
                {
                }
                AppendException("管理员方式重开失败", ex);
                MessageBox.Show(this, "没有获得管理员权限，软件会继续保持当前窗口。\r\n\r\n请重新点击按钮，并在 Windows 提示中选择“是”。", "未获得管理员权限", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        private void UpdateActionAvailability()
        {
            saveButton.Enabled = !patchBusy;
            applyButton.Enabled = !patchBusy && runningAsAdministrator;
            restartAsAdministratorButton.Enabled = !patchBusy && !runningAsAdministrator;
            string instruction = WorkflowGuide.GetInstructionForTesting(
                currentWeGameStatus.IsRunning,
                hasSeenWeGameRunningThisSession,
                currentJson != null,
                hasConfirmedContentThisSession,
                patchBusy,
                patchAppliedThisSession);
            workflowStatus.Text = runningAsAdministrator
                ? (currentCompatibilityCode < 0
                    ? ToolCompatibility.UnsupportedMessage
                    : (currentCompatibilityCode == 0 && !string.IsNullOrWhiteSpace(currentWeGameStatus.ExecutablePath)
                        ? "无法确认 WeGame 助手版本，请确认安装完整；仍无法检测请联系作者。"
                        : instruction))
                : "需要管理员权限：请点击“以管理员身份重新打开”，授权后再安装强力覆盖。";
            bool readyToApply = !patchBusy
                && currentCompatibilityCode == 1
                && hasSeenWeGameRunningThisSession
                && currentJson != null
                && hasConfirmedContentThisSession
                && !currentWeGameStatus.IsRunning
                && !patchAppliedThisSession;
            workflowStatus.BackColor = currentCompatibilityCode < 0
                ? Color.FromArgb(255, 232, 232)
                : (readyToApply || patchAppliedThisSession
                ? Color.FromArgb(225, 244, 232)
                : Color.FromArgb(255, 247, 220));
            workflowStatus.ForeColor = currentCompatibilityCode < 0
                ? Color.Firebrick
                : (readyToApply || patchAppliedThisSession
                ? Color.FromArgb(25, 105, 55)
                : Color.FromArgb(120, 75, 15));
            string applyBlock = WorkflowGuide.GetApplyBlockMessageForTesting(
                currentWeGameStatus.IsRunning,
                hasSeenWeGameRunningThisSession,
                currentJson != null,
                hasConfirmedContentThisSession,
                patchBusy);
            string applyTip = currentCompatibilityCode < 0
                ? ToolCompatibility.UnsupportedMessage
                : (currentCompatibilityCode == 0
                    ? "尚未确认 WeGame 和助手版本。"
                    : (applyBlock == null ? "当前顺序正确，可以应用。" : applyBlock.Replace("\r\n", " ")));
            toolTip.SetToolTip(applyButton, applyTip);
        }

        private void PhraseHandleMouseDown(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
                Control handle = (Control)sender;
                handle.DoDragDrop((int)handle.Tag, DragDropEffects.Move);
            }
        }

        private void PhraseDragEnter(object sender, DragEventArgs e)
        {
            if (!e.Data.GetDataPresent(typeof(int)))
            {
                e.Effect = DragDropEffects.None;
                return;
            }
            e.Effect = DragDropEffects.Move;
            SetDropHighlight((Control)sender, true);
        }

        private void PhraseDragLeave(object sender, EventArgs e)
        {
            SetDropHighlight((Control)sender, false);
        }

        private void PhraseDragDrop(object sender, DragEventArgs e)
        {
            Control target = (Control)sender;
            SetDropHighlight(target, false);
            if (!e.Data.GetDataPresent(typeof(int)))
            {
                return;
            }
            int sourceIndex = (int)e.Data.GetData(typeof(int));
            int targetIndex = (int)target.Tag;
            if (sourceIndex == targetIndex)
            {
                return;
            }
            string[] reordered = Patcher.ReorderPhrasesForTesting(ReadPhrases(), sourceIndex, targetIndex);
            SetPhraseTexts(reordered);
            PersistDraft(reordered, "顺序已调整并自动保存。");
        }

        private void SetDropHighlight(Control control, bool highlighted)
        {
            Control row = control is Panel ? control : control.Parent;
            if (row != null)
            {
                row.BackColor = highlighted ? Color.FromArgb(225, 239, 252) : BackColor;
            }
        }

        private void UpdatePatchProgress(int value, string text)
        {
            patchProgress.Value = Math.Max(0, Math.Min(100, value));
            progressStatus.Text = "应用进度：" + text + "（" + patchProgress.Value + "%）";
            progressStatus.ForeColor = patchProgress.Value == 100 ? Color.FromArgb(35, 130, 65) : Color.FromArgb(70, 70, 70);
            if (lastLoggedProgressValue != patchProgress.Value || !string.Equals(lastLoggedProgressText, text, StringComparison.Ordinal))
            {
                lastLoggedProgressValue = patchProgress.Value;
                lastLoggedProgressText = text;
                AppendLog("进度", text + "（" + patchProgress.Value + "%）");
            }
        }

        private void OpenLogButtonClick(object sender, EventArgs e)
        {
            try
            {
                AppendLog("操作", "用户点击打开日志文件夹：" + logger.LogPath);
                OpenDirectoryInExplorer(Path.GetDirectoryName(logger.LogPath));
            }
            catch (Exception ex)
            {
                AppendException("打开运行日志失败", ex);
                MessageBox.Show(this, "无法打开日志位置。\r\n\r\n日志文件：" + logger.LogPath, "打开失败", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        private void ExportDiagnosisButtonClick(object sender, EventArgs e)
        {
            try
            {
                string report = string.IsNullOrWhiteSpace(lastDiagnosisReport)
                    ? BuildCurrentStatusReport()
                    : lastDiagnosisReport;
                string path = Path.Combine(toolDirectory, "诊断报告-" + DateTime.Now.ToString("yyyyMMdd-HHmmss") + ".txt");
                File.WriteAllText(path, report, new UTF8Encoding(false));
                AppendLog("操作", "用户导出诊断报告：" + path);
                OpenFileInExplorer(path);
            }
            catch (Exception ex)
            {
                AppendException("导出诊断报告失败", ex);
                MessageBox.Show(this, "无法导出诊断报告。\r\n\r\n请点击“打开日志文件夹”查看运行日志。", "导出失败", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        private string BuildFailureReport(Exception exception, bool weGameWasClosed, bool weGameRestarted, string restartError)
        {
            StringBuilder report = new StringBuilder();
            report.Append("WeGame 喊话工具诊断报告\r\n");
            report.Append("生成时间：");
            report.Append(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
            report.Append("\r\n工具版本：V");
            report.Append(ToolCompatibility.ToolVersion);
            report.Append("\r\nWeGame版本：");
            report.Append(currentWeGameStatus.Version ?? "未知");
            report.Append("\r\n助手版本：");
            report.Append(currentWeGameStatus.AssistantVersion ?? "未知");
            report.Append("\r\n版本校验：");
            report.Append(currentCompatibilityCode == 1 ? "通过" : "未通过或无法确认");
            report.Append("\r\n\r\n");
            if (exception is CompatibilityDiagnosisException)
            {
                report.Append(exception.Message.Trim());
            }
            else
            {
                report.Append("应用没有完成，工具已停止后续操作。\r\n\r\n直接原因：");
                report.Append(exception == null ? "未知错误。" : exception.Message);
                report.Append("\r\n\r\n请不要反复强制应用。打开日志文件夹后可查看完整运行记录。\r\n");
            }
            report.Append("\r\n\r\nWeGame恢复状态：");
            if (!weGameWasClosed)
            {
                report.Append("本次没有由工具关闭 WeGame。 ");
            }
            else if (weGameRestarted)
            {
                report.Append("工具已自动重新打开 WeGame。 ");
            }
            else
            {
                report.Append("工具未能自动重新打开 WeGame，请手动启动。原因：");
                report.Append(string.IsNullOrWhiteSpace(restartError) ? "未取得具体原因。" : restartError);
            }
            report.Append("\r\n日志位置：");
            report.Append(logger.LogPath);
            return report.ToString();
        }

        private string BuildCurrentStatusReport()
        {
            return "WeGame 喊话工具当前状态报告\r\n"
                + "生成时间：" + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + "\r\n"
                + "工具版本：V" + ToolCompatibility.ToolVersion + "\r\n"
                + "WeGame版本：" + (currentWeGameStatus.Version ?? "未知") + "\r\n"
                + "助手版本：" + (currentWeGameStatus.AssistantVersion ?? "未知") + "\r\n"
                + "版本校验：" + (currentCompatibilityCode == 1 ? "通过" : "未通过或无法确认") + "\r\n"
                + "WeGame状态：" + (currentWeGameStatus.IsRunning ? "运行中" : "未运行") + "\r\n"
                + "运行日志：" + logger.LogPath + "\r\n";
        }

        private static void OpenFileInExplorer(string path)
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = "explorer.exe",
                Arguments = "/select,\"" + path + "\"",
                UseShellExecute = true
            });
        }

        private static void OpenDirectoryInExplorer(string path)
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = "explorer.exe",
                Arguments = "\"" + path + "\"",
                UseShellExecute = true
            });
        }

        private void AppendLog(string level, string text)
        {
            AppendUiLog(level, text);
            if (logger != null)
            {
                logger.Write(level, text);
            }
        }

        private void AppendException(string operation, Exception exception)
        {
            AppendUiLog("失败", operation + "：" + (exception == null ? "未知错误" : exception.Message));
            if (logger != null)
            {
                logger.WriteException(operation, exception);
            }
        }

        private void AppendUiLog(string level, string text)
        {
            if (logBox == null || logBox.IsDisposed)
            {
                return;
            }
            logBox.AppendText("[" + DateTime.Now.ToString("HH:mm:ss") + "] [" + level + "] " + text + Environment.NewLine);
        }
    }
}
