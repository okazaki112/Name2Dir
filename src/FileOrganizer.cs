using System;
using System.Collections.Generic;
using System.IO;

namespace FileOrganizerApp
{
    public enum OrganizeOutcome
    {
        Moved,
        Skipped,
        SkippedFolder,
        Failed,
        Undone
    }

    public sealed class OrganizeResult
    {
        public OrganizeOutcome Outcome { get; set; }
        public string SourcePath { get; set; }
        public string TargetPath { get; set; }
        public string TargetFolder { get; set; }
        public bool FolderCreated { get; set; }
        public string Message { get; set; }
    }

    // 一次成功移动的可撤销记录。
    public sealed class MoveRecord
    {
        public string Source { get; set; }
        public string Target { get; set; }
        public string Folder { get; set; }
        public bool FolderCreated { get; set; }
    }

    // 纯逻辑：与 UI 解耦，可被控制台 harness 独立机检。
    public static class FileOrganizer
    {
        private static readonly string[] ReservedNames = new string[]
        {
            "CON", "PRN", "AUX", "NUL",
            "COM1", "COM2", "COM3", "COM4", "COM5", "COM6", "COM7", "COM8", "COM9",
            "LPT1", "LPT2", "LPT3", "LPT4", "LPT5", "LPT6", "LPT7", "LPT8", "LPT9"
        };

        public static OrganizeResult Organize(string path)
        {
            OrganizeResult r = new OrganizeResult();
            r.SourcePath = path;
            r.TargetPath = null;

            if (string.IsNullOrEmpty(path))
            {
                r.Outcome = OrganizeOutcome.Failed;
                r.Message = "路径为空";
                return r;
            }

            try
            {
                if (Directory.Exists(path))
                {
                    r.Outcome = OrganizeOutcome.SkippedFolder;
                    r.Message = "拖入的是文件夹，已跳过（本工具只处理文件）";
                    return r;
                }

                if (!File.Exists(path))
                {
                    r.Outcome = OrganizeOutcome.Failed;
                    r.Message = "文件不存在或已被移走";
                    return r;
                }

                string dir = Path.GetDirectoryName(path);
                string fileName = Path.GetFileName(path);
                string baseName = SanitizeFolderName(Path.GetFileNameWithoutExtension(path));
                if (string.IsNullOrEmpty(baseName))
                {
                    baseName = SanitizeFolderName(fileName);
                }

                string parentFolderName = Path.GetFileName(dir);
                if (string.Equals(parentFolderName, baseName, StringComparison.OrdinalIgnoreCase))
                {
                    r.Outcome = OrganizeOutcome.Skipped;
                    r.TargetPath = path;
                    r.Message = "文件已在同名文件夹中，无需重复归档";
                    return r;
                }

                string targetDir = Path.Combine(dir, baseName);
                string targetFile = Path.Combine(targetDir, fileName);
                r.TargetPath = targetFile;
                r.TargetFolder = targetDir;

                bool selfCollision = string.Equals(
                    Path.GetFullPath(targetDir), Path.GetFullPath(path),
                    StringComparison.OrdinalIgnoreCase);

                if (selfCollision)
                {
                    // 无扩展名文件（如 README/.gitignore）：目标文件夹与源文件同名占位，
                    // 先移到临时名再建文件夹移入，失败则回滚，避免命名冲突。
                    string temp = Path.Combine(dir, "~organizing_" + Guid.NewGuid().ToString("N") + ".tmp");
                    File.Move(path, temp);
                    try
                    {
                        Directory.CreateDirectory(targetDir);
                        File.Move(temp, targetFile);
                    }
                    catch
                    {
                        try { if (!File.Exists(path)) File.Move(temp, path); }
                        catch { }
                        throw;
                    }
                    r.Outcome = OrganizeOutcome.Moved;
                    r.FolderCreated = true;
                    r.Message = "已移入 " + targetDir;
                    return r;
                }

                bool folderExisted = Directory.Exists(targetDir);
                Directory.CreateDirectory(targetDir);
                r.FolderCreated = !folderExisted;

                if (File.Exists(targetFile))
                {
                    r.Outcome = OrganizeOutcome.Skipped;
                    r.Message = "目标文件夹内已存在同名文件，未覆盖";
                    return r;
                }

                File.Move(path, targetFile);
                r.Outcome = OrganizeOutcome.Moved;
                r.Message = "已移入 " + targetDir;
                return r;
            }
            catch (UnauthorizedAccessException)
            {
                r.Outcome = OrganizeOutcome.Failed;
                r.Message = "没有访问权限（文件可能被占用或只读）";
                return r;
            }
            catch (IOException ex)
            {
                r.Outcome = OrganizeOutcome.Failed;
                r.Message = "移动失败：" + ex.Message;
                return r;
            }
            catch (Exception ex)
            {
                r.Outcome = OrganizeOutcome.Failed;
                r.Message = "出错：" + ex.Message;
                return r;
            }
        }

        // 干跑：只预测去向与结果，不做任何磁盘改动，供「移动前确认」预览。
        public static OrganizeResult Plan(string path)
        {
            OrganizeResult r = new OrganizeResult();
            r.SourcePath = path;

            if (string.IsNullOrEmpty(path))
            {
                r.Outcome = OrganizeOutcome.Failed;
                r.Message = "路径为空";
                return r;
            }
            if (Directory.Exists(path))
            {
                r.Outcome = OrganizeOutcome.SkippedFolder;
                r.Message = "是文件夹，将跳过";
                return r;
            }
            if (!File.Exists(path))
            {
                r.Outcome = OrganizeOutcome.Failed;
                r.Message = "文件不存在";
                return r;
            }

            string dir = Path.GetDirectoryName(path);
            string fileName = Path.GetFileName(path);
            string baseName = SanitizeFolderName(Path.GetFileNameWithoutExtension(path));
            if (string.IsNullOrEmpty(baseName))
            {
                baseName = SanitizeFolderName(fileName);
            }

            if (string.Equals(Path.GetFileName(dir), baseName, StringComparison.OrdinalIgnoreCase))
            {
                r.Outcome = OrganizeOutcome.Skipped;
                r.TargetPath = path;
                r.Message = "已在同名文件夹，将跳过";
                return r;
            }

            string targetDir = Path.Combine(dir, baseName);
            string targetFile = Path.Combine(targetDir, fileName);
            r.TargetPath = targetFile;
            r.TargetFolder = targetDir;
            r.FolderCreated = !Directory.Exists(targetDir);

            if (File.Exists(targetFile))
            {
                r.Outcome = OrganizeOutcome.Skipped;
                r.Message = "目标已存在同名文件，将跳过";
                return r;
            }

            r.Outcome = OrganizeOutcome.Moved;
            r.Message = "将移入 " + targetDir;
            return r;
        }

        // 撤销一批移动：文件移回原位置；仅删除本批新建且已空的文件夹。
        public static List<OrganizeResult> UndoBatch(List<MoveRecord> batch)
        {
            List<OrganizeResult> results = new List<OrganizeResult>();
            if (batch == null)
            {
                return results;
            }

            for (int i = batch.Count - 1; i >= 0; i--)
            {
                MoveRecord m = batch[i];
                OrganizeResult r = new OrganizeResult();
                r.SourcePath = m.Target;
                r.TargetPath = m.Source;
                try
                {
                    if (!File.Exists(m.Target))
                    {
                        r.Outcome = OrganizeOutcome.Skipped;
                        r.Message = "撤销跳过：文件已不在归档位置";
                        results.Add(r);
                        continue;
                    }
                    if (File.Exists(m.Source))
                    {
                        r.Outcome = OrganizeOutcome.Skipped;
                        r.Message = "撤销跳过：原位置已有同名文件";
                        results.Add(r);
                        continue;
                    }

                    File.Move(m.Target, m.Source);

                    if (m.FolderCreated && Directory.Exists(m.Folder))
                    {
                        try
                        {
                            if (Directory.GetFileSystemEntries(m.Folder).Length == 0)
                            {
                                Directory.Delete(m.Folder);
                            }
                        }
                        catch
                        {
                        }
                    }

                    r.Outcome = OrganizeOutcome.Undone;
                    r.Message = "已撤销回 " + m.Source;
                }
                catch (Exception ex)
                {
                    r.Outcome = OrganizeOutcome.Failed;
                    r.Message = "撤销失败：" + ex.Message;
                }
                results.Add(r);
            }
            return results;
        }

        private static string SanitizeFolderName(string name)
        {
            if (string.IsNullOrEmpty(name))
            {
                return name;
            }

            char[] invalid = Path.GetInvalidFileNameChars();
            char[] chars = name.ToCharArray();
            for (int i = 0; i < chars.Length; i++)
            {
                for (int j = 0; j < invalid.Length; j++)
                {
                    if (chars[i] == invalid[j])
                    {
                        chars[i] = '_';
                        break;
                    }
                }
            }

            string cleaned = new string(chars).TrimEnd('.', ' ');
            if (cleaned.Length == 0)
            {
                return cleaned;
            }

            string upper = cleaned.ToUpperInvariant();
            for (int k = 0; k < ReservedNames.Length; k++)
            {
                if (upper == ReservedNames[k])
                {
                    cleaned = "_" + cleaned;
                    break;
                }
            }

            return cleaned;
        }
    }
}