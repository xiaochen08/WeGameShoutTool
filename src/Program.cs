using System;
using System.Drawing;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

[assembly: AssemblyTitle("WeGame 喊话自定义工具")]
[assembly: AssemblyProduct("WeGame 喊话自定义工具")]
[assembly: AssemblyVersion(WeGameShoutTool.ToolCompatibility.ToolAssemblyVersion)]
[assembly: AssemblyFileVersion(WeGameShoutTool.ToolCompatibility.ToolAssemblyVersion)]

namespace WeGameShoutTool
{
    internal static class Program
    {
        [STAThread]
        private static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new ShoutFormV5());
        }
    }

    internal sealed class ShoutForm : Form
    {
        private readonly TextBox[] phraseBoxes = new TextBox[10];
        private readonly ComboBox[] audienceBoxes = new ComboBox[10];
        private readonly TextBox logBox;
        private readonly Button saveButton;
        private readonly Button applyButton;
        private readonly Label serverStatus;
        private readonly Label cacheStatus;
        private readonly Label progressStatus;
        private readonly ProgressBar patchProgress;
        private readonly PhraseServer server;
        private readonly string phraseFile;
        private readonly string legacyPhraseFile;
        private readonly ToolTip dragTip;
        private readonly System.Windows.Forms.Timer autoSaveTimer;
        private bool suppressPhraseEvents;
        private volatile string currentJson;

        public ShoutForm()
        {
            Text = "WeGame 喊话自定义工具";
            StartPosition = FormStartPosition.CenterScreen;
            ClientSize = new Size(820, 840);
            MinimumSize = new Size(760, 780);
            Font = new Font("Microsoft YaHei UI", 9F);

            Label title = new Label();
            title.Text = "WeGame 喊话自定义工具";
            title.Font = new Font(Font.FontFamily, 18F, FontStyle.Bold);
            title.AutoSize = true;
            title.Location = new Point(24, 20);
            Controls.Add(title);

            Label description = new Label();
            description.Text = "内容保存在本工具中。工具运行时，在 WeGame 客户端内进入《英雄联盟》详情页 → 辅助设置，即会自动同步；不需要启动游戏。";
            description.AutoSize = true;
            description.ForeColor = Color.FromArgb(70, 70, 70);
            description.Location = new Point(27, 62);
            Controls.Add(description);

            Label warning = new Label();
            warning.Text = "注意：应用补丁时必须彻底退出 WeGame；应用后再启动 WeGame，并让本工具保持运行。";
            warning.AutoSize = true;
            warning.ForeColor = Color.FromArgb(190, 70, 30);
            warning.Location = new Point(27, 87);
            Controls.Add(warning);

            Label dragHint = new Label();
            dragHint.Text = "拖动左侧 ☰ 调整顺序，松开后自动保存";
            dragHint.AutoSize = true;
            dragHint.ForeColor = Color.FromArgb(70, 105, 145);
            dragHint.Location = new Point(27, 112);
            Controls.Add(dragHint);

            dragTip = new ToolTip();
            int top = 138;
            for (int i = 0; i < phraseBoxes.Length; i++)
            {
                Panel row = new Panel();
                row.Location = new Point(20, top + (i * 38));
                row.Size = new Size(770, 30);
                row.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
                row.AllowDrop = true;
                row.Tag = i;
                row.DragEnter += PhraseDragEnter;
                row.DragDrop += PhraseDragDrop;
                row.DragLeave += PhraseDragLeave;
                Controls.Add(row);

                Label handle = new Label();
                handle.Text = "☰";
                handle.Font = new Font(Font.FontFamily, 13F, FontStyle.Bold);
                handle.TextAlign = ContentAlignment.MiddleCenter;
                handle.ForeColor = Color.FromArgb(85, 105, 125);
                handle.Cursor = Cursors.SizeAll;
                handle.Location = new Point(0, 0);
                handle.Size = new Size(28, 28);
                handle.Tag = i;
                handle.MouseDown += PhraseHandleMouseDown;
                dragTip.SetToolTip(handle, "拖动调整顺序，松开后自动保存");
                row.Controls.Add(handle);

                Label number = new Label();
                number.Text = (i + 1).ToString();
                number.TextAlign = ContentAlignment.MiddleRight;
                number.Location = new Point(28, 2);
                number.Size = new Size(28, 25);
                number.Tag = i;
                number.AllowDrop = true;
                number.DragEnter += PhraseDragEnter;
                number.DragDrop += PhraseDragDrop;
                number.DragLeave += PhraseDragLeave;
                row.Controls.Add(number);

                ComboBox audience = new ComboBox();
                audience.DropDownStyle = ComboBoxStyle.DropDownList;
                audience.Items.Add("队伍");
                audience.Items.Add("所有人");
                audience.SelectedIndex = 0;
                audience.Location = new Point(60, 1);
                audience.Size = new Size(88, 28);
                audience.Tag = i;
                audience.SelectedIndexChanged += AudienceChanged;
                audienceBoxes[i] = audience;
                row.Controls.Add(audience);

                TextBox box = new TextBox();
                box.Location = new Point(154, 1);
                box.Size = new Size(616, 28);
                box.MaxLength = Patcher.MaxPhraseLength;
                box.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
                box.AllowDrop = true;
                box.Tag = i;
                box.DragEnter += PhraseDragEnter;
                box.DragDrop += PhraseDragDrop;
                box.DragLeave += PhraseDragLeave;
                box.TextChanged += PhraseTextChanged;
                phraseBoxes[i] = box;
                row.Controls.Add(box);
            }

            saveButton = new Button();
            saveButton.Text = "保存内容并开启同步";
            saveButton.Size = new Size(190, 38);
            saveButton.Location = new Point(60, 528);
            saveButton.Click += SaveButtonClick;
            Controls.Add(saveButton);

            applyButton = new Button();
            applyButton.Text = "检测并应用到 WeGame";
            applyButton.Size = new Size(210, 38);
            applyButton.Location = new Point(265, 528);
            applyButton.Click += ApplyButtonClick;
            Controls.Add(applyButton);

            serverStatus = new Label();
            serverStatus.AutoSize = true;
            serverStatus.Location = new Point(495, 538);
            serverStatus.ForeColor = Color.FromArgb(130, 80, 20);
            serverStatus.Text = "本地同步：准备中";
            Controls.Add(serverStatus);

            progressStatus = new Label();
            progressStatus.AutoSize = true;
            progressStatus.Location = new Point(60, 580);
            progressStatus.Text = "应用进度：等待操作";
            progressStatus.ForeColor = Color.FromArgb(70, 70, 70);
            Controls.Add(progressStatus);

            patchProgress = new ProgressBar();
            patchProgress.Minimum = 0;
            patchProgress.Maximum = 100;
            patchProgress.Value = 0;
            patchProgress.Location = new Point(60, 605);
            patchProgress.Size = new Size(730, 18);
            patchProgress.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            Controls.Add(patchProgress);

            cacheStatus = new Label();
            cacheStatus.AutoSize = true;
            cacheStatus.Location = new Point(60, 632);
            cacheStatus.Text = "本地缓存：等待输入";
            cacheStatus.ForeColor = Color.FromArgb(70, 105, 145);
            Controls.Add(cacheStatus);

            logBox = new TextBox();
            logBox.Multiline = true;
            logBox.ReadOnly = true;
            logBox.ScrollBars = ScrollBars.Vertical;
            logBox.Location = new Point(60, 662);
            logBox.Size = new Size(730, 150);
            logBox.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
            Controls.Add(logBox);

            string documents = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            phraseFile = Patcher.GetLocalPhraseFileForTesting(documents);
            legacyPhraseFile = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "喊话内容.txt");
            autoSaveTimer = new System.Windows.Forms.Timer();
            autoSaveTimer.Interval = 500;
            autoSaveTimer.Tick += AutoSaveTimerTick;
            LoadPhrases();
            server = new PhraseServer(27777, delegate { return currentJson; });
            Shown += FormShown;
            FormClosed += FormClosedHandler;
        }

        private void FormShown(object sender, EventArgs e)
        {
            try
            {
                server.Start();
                serverStatus.Text = "本地同步：运行中（127.0.0.1）";
                serverStatus.ForeColor = Color.FromArgb(35, 130, 65);
                AppendLog("本地同步服务已启动，只监听本机。") ;
            }
            catch (Exception ex)
            {
                serverStatus.Text = "本地同步：启动失败";
                serverStatus.ForeColor = Color.Firebrick;
                AppendLog("同步服务启动失败：" + ex.Message);
            }
        }

        private void FormClosedHandler(object sender, FormClosedEventArgs e)
        {
            autoSaveTimer.Stop();
            server.Stop();
        }

        private void LoadPhrases()
        {
            string sourceFile = File.Exists(phraseFile)
                ? phraseFile
                : (File.Exists(legacyPhraseFile) ? legacyPhraseFile : null);
            if (sourceFile == null)
            {
                cacheStatus.Text = "本地缓存：" + phraseFile;
                dragTip.SetToolTip(cacheStatus, phraseFile);
                AppendLog("尚未保存喊话内容，请填写正好 10 条。自动缓存位置：" + phraseFile);
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
                suppressPhraseEvents = true;
                for (int i = 0; i < phraseBoxes.Length; i++)
                {
                    phraseBoxes[i].Text = lines[i];
                }
                RefreshAudienceSelections();
                suppressPhraseEvents = false;

                if (!string.Equals(sourceFile, phraseFile, StringComparison.OrdinalIgnoreCase))
                {
                    Patcher.SavePhrasesFileForTesting(phraseFile, lines);
                    AppendLog("已把旧内容复制到文档缓存，旧文件继续保留：" + phraseFile);
                }
                string validation = Patcher.ValidatePhrasesForTesting(lines);
                if (validation == null)
                {
                    currentJson = Patcher.BuildPhrasesJson(lines);
                    cacheStatus.Text = "本地缓存：已加载 " + DateTime.Now.ToString("HH:mm:ss");
                    dragTip.SetToolTip(cacheStatus, phraseFile);
                    AppendLog("已加载本地喊话内容：" + phraseFile);
                }
                else
                {
                    currentJson = null;
                    cacheStatus.Text = "本地缓存：已加载草稿，内容尚未完整";
                    AppendLog("本地喊话内容无效：" + validation);
                }
            }
            catch (Exception ex)
            {
                suppressPhraseEvents = false;
                AppendLog("读取喊话内容失败：" + ex.Message);
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

        private bool SavePhrases()
        {
            string[] phrases = ReadPhrases();
            string validation = Patcher.ValidatePhrasesForTesting(phrases);
            if (validation != null)
            {
                MessageBox.Show(this, validation, "内容未保存", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return false;
            }

            Patcher.SavePhrasesFileForTesting(phraseFile, phrases);
            currentJson = Patcher.BuildPhrasesJson(phrases);
            cacheStatus.Text = "本地缓存：已保存 " + DateTime.Now.ToString("HH:mm:ss");
            dragTip.SetToolTip(cacheStatus, phraseFile);
            AppendLog("内容已保存到：" + phraseFile);
            return true;
        }

        private void PhraseTextChanged(object sender, EventArgs e)
        {
            if (suppressPhraseEvents)
            {
                return;
            }
            autoSaveTimer.Stop();
            autoSaveTimer.Start();
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
                PersistDraft(ReadPhrases(), audience.SelectedIndex == 1
                    ? "已切换为所有人并自动缓存。"
                    : "已切换为队伍并自动缓存。");
            }
            catch (Exception ex)
            {
                suppressPhraseEvents = true;
                audience.SelectedIndex = Patcher.IsAllAudienceForTesting(phraseBoxes[index].Text) ? 1 : 0;
                suppressPhraseEvents = false;
                MessageBox.Show(this, ex.Message, "无法切换范围", MessageBoxButtons.OK, MessageBoxIcon.Warning);
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

        private void PersistDraft(string[] phrases, string successMessage)
        {
            try
            {
                Patcher.SavePhrasesFileForTesting(phraseFile, phrases);
                string validation = Patcher.ValidatePhrasesForTesting(phrases);
                currentJson = validation == null ? Patcher.BuildPhrasesJson(phrases) : null;
                cacheStatus.Text = validation == null
                    ? "本地缓存：已自动保存 " + DateTime.Now.ToString("HH:mm:ss")
                    : "本地缓存：草稿已保存，内容尚未完整";
                dragTip.SetToolTip(cacheStatus, phraseFile);
                AppendLog(successMessage);
            }
            catch (Exception ex)
            {
                cacheStatus.Text = "本地缓存：保存失败";
                AppendLog("自动缓存失败：" + ex.Message);
            }
        }

        private void PhraseHandleMouseDown(object sender, MouseEventArgs e)
        {
            if (e.Button != MouseButtons.Left)
            {
                return;
            }
            Control handle = (Control)sender;
            handle.DoDragDrop((int)handle.Tag, DragDropEffects.Move);
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
            Control targetControl = (Control)sender;
            SetDropHighlight(targetControl, false);
            if (!e.Data.GetDataPresent(typeof(int)))
            {
                return;
            }

            int sourceIndex = (int)e.Data.GetData(typeof(int));
            int targetIndex = (int)targetControl.Tag;
            if (sourceIndex == targetIndex)
            {
                return;
            }

            string[] reordered = Patcher.ReorderPhrasesForTesting(ReadPhrases(), sourceIndex, targetIndex);
            suppressPhraseEvents = true;
            for (int i = 0; i < phraseBoxes.Length; i++)
            {
                phraseBoxes[i].Text = reordered[i];
            }
            RefreshAudienceSelections();
            suppressPhraseEvents = false;
            SaveAfterReorder(reordered);
        }

        private void SetDropHighlight(Control control, bool highlighted)
        {
            Control row = control is Panel ? control : control.Parent;
            if (row != null)
            {
                row.BackColor = highlighted ? Color.FromArgb(225, 239, 252) : BackColor;
            }
        }

        private void SaveAfterReorder(string[] phrases)
        {
            PersistDraft(phrases, "顺序已调整并自动缓存，同步内容已立即更新。");
        }

        private void SaveButtonClick(object sender, EventArgs e)
        {
            if (SavePhrases())
            {
                MessageBox.Show(this, "已保存。保持本工具运行，在 WeGame 客户端内进入《英雄联盟》详情页 → 辅助设置即可同步；不需要启动游戏。", "保存成功", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
        }

        private async void ApplyButtonClick(object sender, EventArgs e)
        {
            if (!SavePhrases())
            {
                return;
            }

            saveButton.Enabled = false;
            applyButton.Enabled = false;
            UpdatePatchProgress(0, "准备开始");
            AppendLog("开始检测兼容缓存……");
            try
            {
                string path = await Task.Run(delegate
                {
                    return Patcher.ApplyToCurrentWeGame(
                        delegate(string line)
                        {
                            BeginInvoke((Action)delegate { AppendLog(line); });
                        },
                        delegate(int value, string text)
                        {
                            BeginInvoke((Action)delegate { UpdatePatchProgress(value, text); });
                        });
                });
                AppendLog("已验证目标：" + path);
                MessageBox.Show(this,
                    "应用完成。现在只启动 WeGame 客户端并保持本工具运行，先不要启动游戏；在客户端内进入《英雄联盟》详情页 → 辅助设置，看到“已从喊话工具同步”后再进入游戏。",
                    "应用完成",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                progressStatus.Text = "应用进度：失败 — " + ex.Message;
                progressStatus.ForeColor = Color.Firebrick;
                AppendLog("应用失败：" + ex.Message);
                MessageBox.Show(this, ex.Message, "应用失败", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                saveButton.Enabled = true;
                applyButton.Enabled = true;
            }
        }

        private void UpdatePatchProgress(int value, string text)
        {
            patchProgress.Value = Math.Max(patchProgress.Minimum, Math.Min(patchProgress.Maximum, value));
            progressStatus.Text = "应用进度：" + text + "（" + patchProgress.Value + "%）";
            progressStatus.ForeColor = patchProgress.Value == 100
                ? Color.FromArgb(35, 130, 65)
                : Color.FromArgb(70, 70, 70);
        }

        private void AppendLog(string text)
        {
            if (logBox == null)
            {
                return;
            }
            logBox.AppendText("[" + DateTime.Now.ToString("HH:mm:ss") + "] " + text + Environment.NewLine);
        }
    }

    internal sealed class PhraseServer
    {
        private readonly int port;
        private readonly Func<string> jsonProvider;
        private TcpListener listener;
        private Thread thread;
        private volatile bool running;
        private int phraseRequestCount;
        private int lastSyncResultCode = int.MinValue;
        private long lastSyncResultUtcTicks;

        public PhraseServer(int port, Func<string> jsonProvider)
        {
            this.port = port;
            this.jsonProvider = jsonProvider;
        }

        public bool IsRunning
        {
            get { return running; }
        }

        public int PhraseRequestCount
        {
            get { return Interlocked.CompareExchange(ref phraseRequestCount, 0, 0); }
        }

        public int LastSyncResultCode
        {
            get { return Interlocked.CompareExchange(ref lastSyncResultCode, 0, 0); }
        }

        public DateTime LastSyncResultUtc
        {
            get
            {
                long ticks = Interlocked.Read(ref lastSyncResultUtcTicks);
                return ticks <= 0 ? DateTime.MinValue : new DateTime(ticks, DateTimeKind.Utc);
            }
        }

        public void ResetSyncResult()
        {
            Interlocked.Exchange(ref lastSyncResultCode, int.MinValue);
            Interlocked.Exchange(ref lastSyncResultUtcTicks, 0L);
        }

        public void Start()
        {
            if (running)
            {
                return;
            }
            listener = new TcpListener(IPAddress.Loopback, port);
            listener.Start();
            running = true;
            thread = new Thread(ListenLoop);
            thread.IsBackground = true;
            thread.Name = "WeGameShoutLocalServer";
            thread.Start();
        }

        public void Stop()
        {
            running = false;
            if (listener != null)
            {
                listener.Stop();
            }
        }

        private void ListenLoop()
        {
            while (running)
            {
                try
                {
                    TcpClient client = listener.AcceptTcpClient();
                    ThreadPool.QueueUserWorkItem(delegate { HandleClient(client); });
                }
                catch (SocketException)
                {
                    if (running)
                    {
                        Thread.Sleep(100);
                    }
                }
                catch (ObjectDisposedException)
                {
                    return;
                }
            }
        }

        private void HandleClient(TcpClient client)
        {
            using (client)
            {
                client.ReceiveTimeout = 2500;
                client.SendTimeout = 2500;
                using (NetworkStream stream = client.GetStream())
                using (StreamReader reader = new StreamReader(stream, Encoding.ASCII, false, 1024, true))
                {
                    string requestLine = reader.ReadLine();
                    string line;
                    do
                    {
                        line = reader.ReadLine();
                    }
                    while (!string.IsNullOrEmpty(line));

                    bool options = requestLine != null && requestLine.StartsWith("OPTIONS ", StringComparison.OrdinalIgnoreCase);
                    bool getPhrases = requestLine != null && requestLine.StartsWith("GET /phrases", StringComparison.OrdinalIgnoreCase);
                    int syncResultCode;
                    bool syncResult = TryReadSyncResultCode(requestLine, out syncResultCode);
                    if (options)
                    {
                        WriteResponse(stream, "204 No Content", string.Empty, "text/plain; charset=utf-8");
                        return;
                    }

                    if (syncResult)
                    {
                        Interlocked.Exchange(ref lastSyncResultCode, syncResultCode);
                        Interlocked.Exchange(ref lastSyncResultUtcTicks, DateTime.UtcNow.Ticks);
                        WriteResponse(stream, "204 No Content", string.Empty, "text/plain; charset=utf-8");
                        return;
                    }

                    if (!getPhrases)
                    {
                        WriteResponse(stream, "404 Not Found", "not found", "text/plain; charset=utf-8");
                        return;
                    }

                    Interlocked.Increment(ref phraseRequestCount);

                    string json = jsonProvider();
                    if (string.IsNullOrEmpty(json))
                    {
                        WriteResponse(stream, "503 Service Unavailable", "phrases not configured", "text/plain; charset=utf-8");
                        return;
                    }
                    WriteResponse(stream, "200 OK", json, "application/json; charset=utf-8");
                }
            }
        }

        private static bool TryReadSyncResultCode(string requestLine, out int code)
        {
            code = int.MinValue;
            const string prefix = "GET /sync-result?code=";
            if (string.IsNullOrEmpty(requestLine) || !requestLine.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }
            int end = requestLine.IndexOf(' ', prefix.Length);
            if (end < 0)
            {
                return false;
            }
            return int.TryParse(requestLine.Substring(prefix.Length, end - prefix.Length), out code);
        }

        private static void WriteResponse(NetworkStream stream, string status, string body, string contentType)
        {
            byte[] content = Encoding.UTF8.GetBytes(body);
            string headers = "HTTP/1.1 " + status + "\r\n"
                + "Content-Type: " + contentType + "\r\n"
                + "Content-Length: " + content.Length + "\r\n"
                + "Cache-Control: no-store\r\n"
                + "Access-Control-Allow-Origin: https://wegame.gtimg.com\r\n"
                + "Access-Control-Allow-Methods: GET, OPTIONS\r\n"
                + "Access-Control-Allow-Headers: Content-Type, X-Requested-With, Access-Control-Request-Private-Network\r\n"
                + "Access-Control-Allow-Private-Network: true\r\n"
                + "Vary: Origin\r\n"
                + "Connection: close\r\n\r\n";
            byte[] headerBytes = Encoding.ASCII.GetBytes(headers);
            stream.Write(headerBytes, 0, headerBytes.Length);
            if (content.Length > 0)
            {
                stream.Write(content, 0, content.Length);
            }
            stream.Flush();
        }
    }
}
