using System;
using System.Collections.Generic;
using System.IO;
using FileOrganizerApp;

// 独立机检：对临时文件断言目录树与负样例，不依赖 UI。
internal static class TestHarness
{
    private static int passed;
    private static int failed;

    private static int Main()
    {
        string root = Path.Combine(Path.GetTempPath(), "FileOrganizerTest_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            string f1 = MakeFile(root, "report.pdf");
            OrganizeResult r1 = FileOrganizer.Organize(f1);
            Check("report.pdf 移动成功", r1.Outcome == OrganizeOutcome.Moved);
            Check("去扩展名建 report 文件夹且文件在内", File.Exists(Path.Combine(root, "report", "report.pdf")));
            Check("原位置文件已移走", !File.Exists(f1));

            Directory.CreateDirectory(Path.Combine(root, "photo"));
            MakeFile(Path.Combine(root, "photo"), "existing.txt");
            string f2 = MakeFile(root, "photo.jpg");
            OrganizeResult r2 = FileOrganizer.Organize(f2);
            Check("photo.jpg 移入已有同名文件夹", r2.Outcome == OrganizeOutcome.Moved && File.Exists(Path.Combine(root, "photo", "photo.jpg")));
            Check("已有文件夹原内容保留", File.Exists(Path.Combine(root, "photo", "existing.txt")));

            string f3 = MakeFile(root, "README");
            OrganizeResult r3 = FileOrganizer.Organize(f3);
            Check("无扩展名 README 归档", r3.Outcome == OrganizeOutcome.Moved && File.Exists(Path.Combine(root, "README", "README")));

            string f4 = MakeFile(root, "my.report.pdf");
            OrganizeResult r4 = FileOrganizer.Organize(f4);
            Check("多点文件名建 my.report 文件夹", r4.Outcome == OrganizeOutcome.Moved && File.Exists(Path.Combine(root, "my.report", "my.report.pdf")));

            Directory.CreateDirectory(Path.Combine(root, "dup"));
            MakeFile(Path.Combine(root, "dup"), "dup.txt", "OLD");
            string f5 = MakeFile(root, "dup.txt", "NEW");
            OrganizeResult r5 = FileOrganizer.Organize(f5);
            Check("目标已存在同名文件则跳过", r5.Outcome == OrganizeOutcome.Skipped);
            Check("绝不覆盖已有文件", File.ReadAllText(Path.Combine(root, "dup", "dup.txt")) == "OLD");
            Check("跳过时源文件保留", File.Exists(f5));

            string dir6 = Path.Combine(root, "aFolder");
            Directory.CreateDirectory(dir6);
            OrganizeResult r6 = FileOrganizer.Organize(dir6);
            Check("拖入文件夹本身被跳过", r6.Outcome == OrganizeOutcome.SkippedFolder);

            OrganizeResult r7 = FileOrganizer.Organize(Path.Combine(root, "report", "report.pdf"));
            Check("已在同名文件夹不重复嵌套", r7.Outcome == OrganizeOutcome.Skipped);
            Check("未生成 report\\report 嵌套", !Directory.Exists(Path.Combine(root, "report", "report")));

            OrganizeResult r9 = FileOrganizer.Organize(Path.Combine(root, "ghost.txt"));
            Check("不存在的文件报失败", r9.Outcome == OrganizeOutcome.Failed);

            string f10 = MakeFile(root, ".gitignore");
            OrganizeResult r10 = FileOrganizer.Organize(f10);
            Check("点文件 .gitignore 可归档", r10.Outcome == OrganizeOutcome.Moved && !File.Exists(f10));
            Console.WriteLine("  (.gitignore 目标: " + r10.TargetPath + ")");

            string fp = MakeFile(root, "plan.pdf");
            OrganizeResult rp = FileOrganizer.Plan(fp);
            Check("Plan 预测将移入", rp.Outcome == OrganizeOutcome.Moved);
            Check("Plan 干跑不改动磁盘", File.Exists(fp) && !Directory.Exists(Path.Combine(root, "plan")));

            string fu = MakeFile(root, "undo.pdf");
            OrganizeResult ru = FileOrganizer.Organize(fu);
            List<MoveRecord> batch = new List<MoveRecord>();
            batch.Add(new MoveRecord { Source = ru.SourcePath, Target = ru.TargetPath, Folder = ru.TargetFolder, FolderCreated = ru.FolderCreated });
            List<OrganizeResult> un = FileOrganizer.UndoBatch(batch);
            Check("撤销回原位置", File.Exists(fu) && !File.Exists(ru.TargetPath));
            Check("撤销删除新建空文件夹", !Directory.Exists(ru.TargetFolder));
            Check("撤销结果标记 Undone", un.Count == 1 && un[0].Outcome == OrganizeOutcome.Undone);

            Directory.CreateDirectory(Path.Combine(root, "keep"));
            MakeFile(Path.Combine(root, "keep"), "old.txt");
            string fk = MakeFile(root, "keep.pdf");
            OrganizeResult rk = FileOrganizer.Organize(fk);
            Check("keep.pdf 移入已有文件夹且标记非新建", rk.Outcome == OrganizeOutcome.Moved && !rk.FolderCreated);
            List<MoveRecord> batch2 = new List<MoveRecord>();
            batch2.Add(new MoveRecord { Source = rk.SourcePath, Target = rk.TargetPath, Folder = rk.TargetFolder, FolderCreated = rk.FolderCreated });
            FileOrganizer.UndoBatch(batch2);
            Check("撤销保留已有文件夹及原内容", Directory.Exists(Path.Combine(root, "keep")) && File.Exists(Path.Combine(root, "keep", "old.txt")) && File.Exists(fk));
        }
        finally
        {
            try { Directory.Delete(root, true); }
            catch { }
        }

        Console.WriteLine("==== 逻辑机检结果 ====");
        Console.WriteLine(string.Format("通过 {0} · 失败 {1}", passed, failed));
        return failed == 0 ? 0 : 1;
    }

    private static string MakeFile(string dir, string name)
    {
        return MakeFile(dir, name, "content");
    }

    private static string MakeFile(string dir, string name, string content)
    {
        string p = Path.Combine(dir, name);
        File.WriteAllText(p, content);
        return p;
    }

    private static void Check(string label, bool ok)
    {
        if (ok)
        {
            passed++;
            Console.WriteLine("[PASS] " + label);
        }
        else
        {
            failed++;
            Console.WriteLine("[FAIL] " + label);
        }
    }
}