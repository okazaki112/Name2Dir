using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Windows.Forms;
using FileOrganizerApp;

// UI 端到端机检：确认模式→预览(不动磁盘)→执行(移动)→撤销(回退)，全程构造真实 FileDrop 数据。
internal static class UiHarness
{
    [STAThread]
    private static int Main()
    {
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);

        string root = Path.Combine(Path.GetTempPath(), "FileOrganizerUi_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        string baseDir = AppDomain.CurrentDomain.BaseDirectory;
        string previewPng = Path.Combine(baseDir, "界面预览.png");
        string afterPng = Path.Combine(baseDir, "执行撤销后.png");

        try
        {
            string f1 = Path.Combine(root, "年度报告.pdf");
            File.WriteAllText(f1, "x");
            string f2 = Path.Combine(root, "风景照.jpg");
            File.WriteAllText(f2, "x");
            string f3 = Path.Combine(root, "笔记.txt");
            File.WriteAllText(f3, "x");
            string folder4 = Path.Combine(root, "一个文件夹");
            Directory.CreateDirectory(folder4);

            using (MainForm form = new MainForm())
            {
                form.StartPosition = FormStartPosition.Manual;
                form.Location = new Point(-2000, -2000);
                bool defaultChecked = form.ConfirmMode;
                form.Show();
                Application.DoEvents();
                bool iconSet = form.Icon != null;

                string[] paths = new string[] { f1, f2, f3, folder4 };
                DataObject data = new DataObject(DataFormats.FileDrop, paths);
                DragEventArgs args = new DragEventArgs(data, 0, 0, 0, DragDropEffects.Move, DragDropEffects.Move);
                form.HandleDrop(args);
                Application.DoEvents();
                bool previewNoMove = File.Exists(f1) && File.Exists(f2) && File.Exists(f3);
                Capture(form, previewPng);

                form.ExecutePending();
                Application.DoEvents();
                bool movedNow = !File.Exists(f1) && File.Exists(Path.Combine(root, "年度报告", "年度报告.pdf"));

                form.UndoLast();
                Application.DoEvents();
                bool undone = File.Exists(f1) && File.Exists(f2) && File.Exists(f3)
                    && !Directory.Exists(Path.Combine(root, "年度报告"));
                Capture(form, afterPng);

                form.Close();
                Console.WriteLine("默认勾选确认=" + defaultChecked + " 预览不动磁盘=" + previewNoMove + " 执行后移动=" + movedNow + " 撤销回退=" + undone + " 窗口图标已加载=" + iconSet);
                Console.WriteLine("截图: " + previewPng + " / " + afterPng);
                return (defaultChecked && previewNoMove && movedNow && undone && iconSet) ? 0 : 1;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine("UI harness 异常: " + ex);
            return 1;
        }
        finally
        {
            try { Directory.Delete(root, true); }
            catch { }
        }
    }

    private static void Capture(MainForm form, string path)
    {
        using (Bitmap bmp = new Bitmap(form.ClientSize.Width, form.ClientSize.Height))
        {
            form.DrawToBitmap(bmp, new Rectangle(0, 0, bmp.Width, bmp.Height));
            bmp.Save(path, ImageFormat.Png);
        }
    }
}