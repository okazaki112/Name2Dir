using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Windows.Forms;

namespace FileOrganizerApp
{
    public sealed class MainForm : Form
    {
        private static readonly Color Accent = Color.FromArgb(47, 111, 237);
        private static readonly Color TextPrimary = Color.FromArgb(31, 41, 55);
        private static readonly Color TextSecondary = Color.FromArgb(107, 114, 128);
        private static readonly Color SuccessColor = Color.FromArgb(22, 163, 74);
        private static readonly Color SkipColor = Color.FromArgb(180, 130, 20);
        private static readonly Color FailColor = Color.FromArgb(220, 38, 38);
        private static readonly Color UndoColor = Color.FromArgb(13, 148, 136);

        private DropPanel dropPanel;
        private ListView resultList;
        private Label statusLabel;
        private CheckBox confirmCheck;
        private Button executeButton;
        private Button undoButton;

        private readonly List<string> pendingPaths = new List<string>();
        private List<MoveRecord> lastBatch = new List<MoveRecord>();
        private int movedCount;
        private int skippedCount;
        private int failedCount;

        public MainForm()
        {
            BuildUi();
        }

        internal bool ConfirmMode
        {
            get { return confirmCheck.Checked; }
            set { confirmCheck.Checked = value; }
        }

        private void BuildUi()
        {
            LoadAppIcon();
            Text = "文件归档助手";
            Font = new Font("Segoe UI", 9F);
            AutoScaleMode = AutoScaleMode.Font;
            ClientSize = new Size(880, 580);
            MinimumSize = new Size(720, 460);
            StartPosition = FormStartPosition.CenterScreen;
            BackColor = Color.FromArgb(250, 250, 252);
            AllowDrop = true;

            TableLayoutPanel root = new TableLayoutPanel();
            root.Dock = DockStyle.Fill;
            root.AutoSize = false;
            root.GrowStyle = TableLayoutPanelGrowStyle.FixedSize;
            root.ColumnCount = 1;
            root.RowCount = 4;
            root.Padding = new Padding(16, 12, 16, 12);
            root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 64F));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 168F));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 52F));
            root.BackColor = BackColor;

            root.Controls.Add(BuildHeader(), 0, 0);

            dropPanel = new DropPanel();
            dropPanel.Dock = DockStyle.Fill;
            dropPanel.Margin = new Padding(0, 4, 0, 8);
            dropPanel.AllowDrop = true;
            root.Controls.Add(dropPanel, 0, 1);

            resultList = BuildResultList();
            resultList.Dock = DockStyle.Fill;
            resultList.Margin = new Padding(0, 0, 0, 8);
            resultList.AllowDrop = true;
            root.Controls.Add(resultList, 0, 2);

            root.Controls.Add(BuildFooter(), 0, 3);

            Controls.Add(root);

            WireDrop(this);
            WireDrop(dropPanel);
            WireDrop(resultList);

            UpdateStatus();
        }

        private Control BuildHeader()
        {
            Panel header = new Panel();
            header.Dock = DockStyle.Fill;
            header.Margin = new Padding(0);
            header.BackColor = BackColor;

            Label title = new Label();
            title.AutoSize = true;
            title.Text = "文件归档助手";
            title.Font = new Font("Segoe UI Semibold", 15F);
            title.ForeColor = TextPrimary;
            title.Location = new Point(2, 6);
            header.Controls.Add(title);

            Label subtitle = new Label();
            subtitle.AutoSize = true;
            subtitle.Text = "把文件拖进来，自动按文件名建文件夹并移入（位置不变，不覆盖已有文件）";
            subtitle.Font = new Font("Segoe UI", 9F);
            subtitle.ForeColor = TextSecondary;
            subtitle.Location = new Point(3, 40);
            header.Controls.Add(subtitle);

            confirmCheck = new CheckBox();
            confirmCheck.AutoSize = true;
            confirmCheck.Text = "移动前确认";
            confirmCheck.Font = new Font("Segoe UI", 9F);
            confirmCheck.ForeColor = TextSecondary;
            confirmCheck.BackColor = BackColor;
            confirmCheck.Checked = true;
            confirmCheck.CheckedChanged += delegate
            {
                if (!confirmCheck.Checked)
                {
                    pendingPaths.Clear();
                    RemovePreviewRows();
                    executeButton.Enabled = false;
                    UpdateStatus();
                }
            };
            header.Controls.Add(confirmCheck);
            header.Resize += delegate
            {
                confirmCheck.Location = new Point(header.ClientSize.Width - confirmCheck.Width - 4, 12);
            };

            return header;
        }

        private ListView BuildResultList()
        {
            ListView lv = new ListView();
            lv.View = View.Details;
            lv.FullRowSelect = true;
            lv.GridLines = false;
            lv.MultiSelect = false;
            lv.HideSelection = false;
            lv.HeaderStyle = ColumnHeaderStyle.Nonclickable;
            lv.BorderStyle = BorderStyle.FixedSingle;
            lv.BackColor = Color.White;
            lv.ForeColor = TextPrimary;
            lv.Font = new Font("Segoe UI", 9F);
            lv.Columns.Add("结果", 72);
            lv.Columns.Add("文件", 280);
            lv.Columns.Add("说明", 440);
            lv.Resize += delegate { StretchLastColumn(lv); };
            lv.DoubleClick += delegate { OpenSelectedFolder(); };
            return lv;
        }

        private static void StretchLastColumn(ListView lv)
        {
            if (lv.Columns.Count < 3)
            {
                return;
            }
            int used = lv.Columns[0].Width + lv.Columns[1].Width;
            int remain = lv.ClientSize.Width - used - 4;
            lv.Columns[2].Width = Math.Max(140, remain);
        }

        private Control BuildFooter()
        {
            TableLayoutPanel footer = new TableLayoutPanel();
            footer.Dock = DockStyle.Fill;
            footer.AutoSize = false;
            footer.GrowStyle = TableLayoutPanelGrowStyle.FixedSize;
            footer.Margin = new Padding(0);
            footer.ColumnCount = 2;
            footer.RowCount = 1;
            footer.BackColor = BackColor;
            footer.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            footer.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 336F));
            footer.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));

            statusLabel = new Label();
            statusLabel.Dock = DockStyle.Fill;
            statusLabel.TextAlign = ContentAlignment.MiddleLeft;
            statusLabel.AutoEllipsis = true;
            statusLabel.ForeColor = TextSecondary;
            statusLabel.Font = new Font("Segoe UI", 9F);
            statusLabel.Margin = new Padding(2, 0, 0, 0);
            footer.Controls.Add(statusLabel, 0, 0);

            FlowLayoutPanel buttons = new FlowLayoutPanel();
            buttons.Dock = DockStyle.Fill;
            buttons.FlowDirection = FlowDirection.RightToLeft;
            buttons.WrapContents = false;
            buttons.Margin = new Padding(0);
            buttons.BackColor = BackColor;

            Button open = MakeButton("打开文件夹", 100);
            open.Click += delegate { OpenSelectedFolder(); };
            buttons.Controls.Add(open);

            Button clear = MakeButton("清空", 64);
            clear.Click += delegate { ClearResults(); };
            buttons.Controls.Add(clear);

            executeButton = MakeButton("执行", 64);
            executeButton.Enabled = false;
            executeButton.Click += delegate { ExecutePending(); };
            buttons.Controls.Add(executeButton);

            undoButton = MakeButton("撤销", 64);
            undoButton.Enabled = false;
            undoButton.Click += delegate { UndoLast(); };
            buttons.Controls.Add(undoButton);

            footer.Controls.Add(buttons, 1, 0);
            return footer;
        }

        private void LoadAppIcon()
        {
            try
            {
                System.Reflection.Assembly asm = System.Reflection.Assembly.GetExecutingAssembly();
                Stream s = asm.GetManifestResourceStream("FileOrganizerApp.app.ico");
                if (s != null)
                {
                    using (s)
                    {
                        this.Icon = new Icon(s);
                    }
                }
            }
            catch
            {
            }
        }

        private static Button MakeButton(string text, int width)
        {
            Button b = new Button();
            b.Text = text;
            b.AutoSize = false;
            b.Size = new Size(width, 34);
            b.Margin = new Padding(8, 6, 0, 6);
            b.FlatStyle = FlatStyle.Flat;
            b.FlatAppearance.BorderSize = 1;
            b.FlatAppearance.BorderColor = Color.FromArgb(209, 213, 219);
            b.FlatAppearance.MouseOverBackColor = Color.FromArgb(243, 246, 251);
            b.FlatAppearance.MouseDownBackColor = Color.FromArgb(228, 235, 247);
            b.BackColor = Color.White;
            b.ForeColor = TextPrimary;
            b.Font = new Font("Segoe UI", 9F);
            b.Cursor = Cursors.Hand;
            b.UseVisualStyleBackColor = false;
            return b;
        }

        private void WireDrop(Control c)
        {
            c.DragEnter += OnDragEnter;
            c.DragDrop += OnDragDrop;
            c.DragLeave += OnDragLeave;
        }

        private void OnDragEnter(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                e.Effect = DragDropEffects.Move;
                dropPanel.IsDragging = true;
                dropPanel.Invalidate();
            }
            else
            {
                e.Effect = DragDropEffects.None;
            }
        }

        private void OnDragLeave(object sender, EventArgs e)
        {
            dropPanel.IsDragging = false;
            dropPanel.Invalidate();
        }

        private void OnDragDrop(object sender, DragEventArgs e)
        {
            dropPanel.IsDragging = false;
            dropPanel.Invalidate();
            HandleDrop(e);
        }

        internal void HandleDrop(DragEventArgs e)
        {
            if (!e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                return;
            }
            string[] paths = e.Data.GetData(DataFormats.FileDrop) as string[];
            if (paths == null || paths.Length == 0)
            {
                return;
            }
            if (confirmCheck.Checked)
            {
                EnqueuePreview(paths);
            }
            else
            {
                ProcessPaths(paths);
            }
        }

        private void EnqueuePreview(string[] paths)
        {
            resultList.BeginUpdate();
            for (int i = 0; i < paths.Length; i++)
            {
                OrganizeResult plan = FileOrganizer.Plan(paths[i]);
                ListViewItem item = new ListViewItem("待执行");
                item.SubItems.Add(Path.GetFileName(paths[i]));
                item.SubItems.Add(plan.Message ?? "");
                item.ForeColor = Accent;
                item.UseItemStyleForSubItems = true;
                item.Tag = null;
                resultList.Items.Add(item);
                pendingPaths.Add(paths[i]);
            }
            resultList.EndUpdate();
            executeButton.Enabled = pendingPaths.Count > 0;
            UpdateStatus();
            if (resultList.Items.Count > 0)
            {
                resultList.EnsureVisible(resultList.Items.Count - 1);
            }
        }

        private void RemovePreviewRows()
        {
            for (int i = resultList.Items.Count - 1; i >= 0; i--)
            {
                if (resultList.Items[i].Text == "待执行")
                {
                    resultList.Items.RemoveAt(i);
                }
            }
        }

        internal void ExecutePending()
        {
            if (pendingPaths.Count == 0)
            {
                return;
            }
            string[] arr = pendingPaths.ToArray();
            pendingPaths.Clear();
            RemovePreviewRows();
            executeButton.Enabled = false;
            ProcessPaths(arr);
        }

        internal void ProcessPaths(string[] paths)
        {
            Cursor previous = Cursor.Current;
            Cursor.Current = Cursors.WaitCursor;
            List<MoveRecord> batch = new List<MoveRecord>();
            resultList.BeginUpdate();
            try
            {
                for (int i = 0; i < paths.Length; i++)
                {
                    OrganizeResult r = FileOrganizer.Organize(paths[i]);
                    AddResult(r);
                    switch (r.Outcome)
                    {
                        case OrganizeOutcome.Moved:
                            movedCount++;
                            batch.Add(new MoveRecord
                            {
                                Source = r.SourcePath,
                                Target = r.TargetPath,
                                Folder = r.TargetFolder,
                                FolderCreated = r.FolderCreated
                            });
                            break;
                        case OrganizeOutcome.Skipped:
                        case OrganizeOutcome.SkippedFolder:
                            skippedCount++;
                            break;
                        default:
                            failedCount++;
                            break;
                    }
                }
            }
            finally
            {
                resultList.EndUpdate();
                Cursor.Current = previous;
            }
            lastBatch = batch;
            undoButton.Enabled = batch.Count > 0;
            UpdateStatus();
            if (resultList.Items.Count > 0)
            {
                resultList.EnsureVisible(resultList.Items.Count - 1);
            }
        }

        internal void UndoLast()
        {
            if (lastBatch.Count == 0)
            {
                return;
            }
            List<OrganizeResult> results = FileOrganizer.UndoBatch(lastBatch);
            lastBatch = new List<MoveRecord>();
            undoButton.Enabled = false;
            resultList.BeginUpdate();
            for (int i = 0; i < results.Count; i++)
            {
                AddResult(results[i]);
            }
            resultList.EndUpdate();
            statusLabel.Text = "已撤销上一批操作";
            if (resultList.Items.Count > 0)
            {
                resultList.EnsureVisible(resultList.Items.Count - 1);
            }
        }

        private void AddResult(OrganizeResult r)
        {
            string status;
            Color color;
            switch (r.Outcome)
            {
                case OrganizeOutcome.Moved:
                    status = "成功";
                    color = SuccessColor;
                    break;
                case OrganizeOutcome.Skipped:
                case OrganizeOutcome.SkippedFolder:
                    status = "跳过";
                    color = SkipColor;
                    break;
                case OrganizeOutcome.Undone:
                    status = "撤销";
                    color = UndoColor;
                    break;
                default:
                    status = "失败";
                    color = FailColor;
                    break;
            }

            string fileName = string.IsNullOrEmpty(r.SourcePath) ? "" : Path.GetFileName(r.SourcePath);
            ListViewItem item = new ListViewItem(status);
            item.SubItems.Add(fileName);
            item.SubItems.Add(r.Message ?? "");
            item.ForeColor = color;
            item.UseItemStyleForSubItems = true;
            item.Tag = r;
            resultList.Items.Add(item);
        }

        private void UpdateStatus()
        {
            if (pendingPaths.Count > 0)
            {
                statusLabel.Text = string.Format("待执行 {0} 个 · 点「执行」开始移动", pendingPaths.Count);
                return;
            }
            int total = movedCount + skippedCount + failedCount;
            if (total == 0)
            {
                if (confirmCheck.Checked)
                {
                    statusLabel.Text = "就绪 · 拖入先预览，点「执行」移动（可撤销）";
                }
                else
                {
                    statusLabel.Text = "就绪 · 拖入即自动归档";
                }
            }
            else
            {
                statusLabel.Text = string.Format("共 {0} 个 · 成功 {1} · 跳过 {2} · 失败 {3}",
                    total, movedCount, skippedCount, failedCount);
            }
        }

        private void ClearResults()
        {
            resultList.Items.Clear();
            pendingPaths.Clear();
            executeButton.Enabled = false;
            movedCount = 0;
            skippedCount = 0;
            failedCount = 0;
            UpdateStatus();
        }

        private void OpenSelectedFolder()
        {
            string dir = null;
            if (resultList.SelectedItems.Count > 0)
            {
                OrganizeResult r = resultList.SelectedItems[0].Tag as OrganizeResult;
                if (r != null)
                {
                    string path = string.IsNullOrEmpty(r.TargetPath) ? r.SourcePath : r.TargetPath;
                    if (!string.IsNullOrEmpty(path))
                    {
                        dir = Path.GetDirectoryName(path);
                    }
                }
            }
            if (string.IsNullOrEmpty(dir) || !Directory.Exists(dir))
            {
                return;
            }
            try
            {
                Process.Start("explorer.exe", "\"" + dir + "\"");
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, "无法打开文件夹：" + ex.Message, "文件归档助手",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }
    }
}