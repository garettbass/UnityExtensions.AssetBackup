using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

namespace UnityExtensions
{

    public class AssetBackup : UnityEditor.AssetModificationProcessor
    {

        public static readonly string ProjectPath =
            Path.GetDirectoryName(UnityEngine.Application.dataPath);

        //----------------------------------------------------------------------

        private const string AssetPathRoot = "Assets/";

        private static bool HasAssetPathRoot(string assetPath)
        {
            return assetPath?.StartsWith(AssetPathRoot) ?? false;
        }

        //----------------------------------------------------------------------

        private const string BackupPathRoot = "Backups/";

        public static bool AnyAssetBackupsExist()
        {
            var fullBackupPath = Path.Combine(ProjectPath, BackupPathRoot);
            return Directory.Exists(fullBackupPath);
        }

        public static void DeleteAllAssetBackups()
        {
            var fullBackupPath = Path.Combine(ProjectPath, BackupPathRoot);
            if (Directory.Exists(fullBackupPath))
                Directory.Delete(fullBackupPath, recursive: true);
        }

        //----------------------------------------------------------------------

        private static AssetMoveResult OnWillMoveAsset(
            string sourcePath,
            string targetPath)
        {
            if (AssetBackupPreferences.assetBackupEnabled)
            {
                MoveAssetBackup(sourcePath, targetPath);
            }
            return AssetMoveResult.DidNotMove;
        }

        private static void MoveAssetBackup(
            string oldAssetPath,
            string newAssetPath)
        {
            if (!HasAssetPathRoot(oldAssetPath))
                return;

            var newBackupPathPrefix =
                FormatBackupPathWithoutTimestamp(newAssetPath);
            var oldBackupPaths = GetBackupPaths(oldAssetPath);
            foreach (var oldBackupPath in oldBackupPaths)
            {
                var backupTime = ParseBackupPathTimestamp(oldBackupPath);
                var newBackupPath =
                    newBackupPathPrefix +
                    FormatTimestamp(backupTime);
                File.Move(oldBackupPath, newBackupPath);
                File.SetLastWriteTime(newBackupPath, backupTime);
                // Debug.Log(
                //     $"moved '{GetShortBackupPath(oldBackupPath)}' "+
                //     $"to '{GetShortBackupPath(newBackupPath)}'");
            }
        }

        //----------------------------------------------------------------------

        private static string[] OnWillSaveAssets(string[] assetPaths)
        {
            if (AssetBackupPreferences.assetBackupEnabled)
            {
                foreach (var assetPath in assetPaths)
                {
                    SaveAssetBackup(assetPath);
                }
            }
            return assetPaths;
        }

        public static void SaveAssetBackup(string assetPath)
        {
            if (!HasAssetPathRoot(assetPath))
                return;

            var fullAssetPath = Path.Combine(ProjectPath, assetPath);
            if (!File.Exists(fullAssetPath))
                return;

            var backupTime = File.GetLastWriteTime(fullAssetPath);
            var backupPath =
                FormatBackupPathWithoutTimestamp(assetPath) +
                FormatTimestamp(backupTime);
            var backupDir = Path.GetDirectoryName(backupPath);
            if (Directory.Exists(backupDir) == false)
                Directory.CreateDirectory(backupDir);

            if (File.Exists(backupPath) == false)
            {
                File.Copy(fullAssetPath, backupPath);
                File.SetLastWriteTime(backupPath, backupTime);
            }
            // Debug.Log(
            //     $"backed up '{assetPath}' "+
            //     $"to '{GetShortBackupPath(backupPath)}'");

            DeleteExcessAssetBackups(assetPath);
        }

        private static void DeleteExcessAssetBackups(string assetPath)
        {
            var backupPaths = GetBackupPaths(assetPath);
            var backupCount = backupPaths.Length;
            var maxBackupCount = AssetBackupPreferences.maxBackupsPerAsset;
            for (int i = maxBackupCount, n = backupCount; i < n; ++i)
            {
                var excessBackupPath = backupPaths[i];
                File.Delete(excessBackupPath);
            }
        }

        //----------------------------------------------------------------------

        private static string GetShortBackupPath(string backupPath)
        {
            return backupPath.Substring(ProjectPath.Length + 1);
        }

        private static string FormatBackupPathWithoutTimestamp(string assetPath)
        {
            Debug.Assert(HasAssetPathRoot(assetPath));
            var assetSubpath = assetPath.Substring(AssetPathRoot.Length);
            return Path.Combine(ProjectPath, $"{BackupPathRoot}{assetSubpath}");
        }

        private static DateTime ParseBackupPathTimestamp(string backupPath)
        {
            var lparenIndex = backupPath.LastIndexOf('(');
            var timestamp = backupPath.Substring(lparenIndex);
            return ParseTimestamp(timestamp);
        }

        //----------------------------------------------------------------------

        private static string FormatTimestamp(DateTime time)
        {
            var Y = time.Year;
            var M = time.Month;
            var D = time.Day;
            var h = time.Hour;
            var m = time.Minute;
            var s = time.Second;
            return $"({Y:00}-{M:00}-{D:00}-{h:00}-{m:00}-{s:00})";
        }

        private static DateTime ParseTimestamp(string timestamp)
        {
            var parts = timestamp.Trim('(', ')').Split('-');
            var Y = int.Parse(parts[0]);
            var M = int.Parse(parts[1]);
            var D = int.Parse(parts[2]);
            var h = int.Parse(parts[3]);
            var m = int.Parse(parts[4]);
            var s = int.Parse(parts[5]);
            return new DateTime(Y, M, D, h, m, s);
        }

        //----------------------------------------------------------------------

        public static bool CanRestoreFromBackup(string assetPath)
        {
            return 
                HasAssetPathRoot(assetPath) &&
                GetBackupPaths(assetPath).Any();
        }

        [MenuItem(
            "Assets/Restore From Backup",
            priority = 9999,
            validate = true)]
        private static bool CanRestoreFromBackup()
        {
            var asset = Selection.activeObject;
            if (asset == null)
                return false;

            var assetPath = AssetDatabase.GetAssetPath(asset);
            return CanRestoreFromBackup(assetPath);
        }

        //----------------------------------------------------------------------

        public static void RestoreFromBackup(
            Object asset,
            string backupPath)
        {
            var assetPath = AssetDatabase.GetAssetPath(asset);
            RestoreFromBackup(assetPath, backupPath);
        }

        public static void RestoreFromBackup(
            string assetPath,
            string backupPath)
        {
            var replacedPath = Path.Combine(ProjectPath, assetPath);
            var replacedTime = File.GetLastWriteTime(replacedPath);
            var replacedBackupPath =
                FormatBackupPathWithoutTimestamp(assetPath) +
                FormatTimestamp(replacedTime);
            File.Replace(backupPath, replacedPath, replacedBackupPath);
            AssetDatabase.ImportAsset(assetPath);
            AssetDatabase.Refresh();
            var shortBackupPath = GetShortBackupPath(backupPath);
            // Debug.Log($"restored '{assetPath}' from '{shortBackupPath}'");
            if (EditorSceneManager.GetActiveScene().path == assetPath)
            {
                EditorSceneManager.OpenScene(assetPath, OpenSceneMode.Single);
            }
        }

        [MenuItem(
            "Assets/Restore From Backup",
            priority = 9999)]
        private static void RestoreFromBackup()
        {
            var asset = Selection.activeObject;
            RestoreFromBackupWindow.Open(asset);
        }

        //----------------------------------------------------------------------

        private static readonly string[] NoBackupPaths = new string[0];

        public static string[] GetBackupPaths(
            string assetPath)
        {
            if (string.IsNullOrEmpty(assetPath))
                return NoBackupPaths;

            var backupPathPrefix =
                $"{FormatBackupPathWithoutTimestamp(assetPath)}(";

            var backupDir = Path.GetDirectoryName(backupPathPrefix);
            if (Directory.Exists(backupDir) == false)
                return NoBackupPaths;

            return
                Directory
                .EnumerateFiles(backupDir)
                .Where(backupPath => backupPath.StartsWith(backupPathPrefix))
                .OrderBy(backupPath => backupPath)
                .Reverse()
                .ToArray();
        }

        public static IEnumerable<BackupFile> EnumerateBackupFiles(
            Object asset)
        {
            var assetPath = AssetDatabase.GetAssetPath(asset);
            return EnumerateBackupFiles(assetPath);
        }

        public static IEnumerable<BackupFile> EnumerateBackupFiles(
            string assetPath)
        {
            return
                GetBackupPaths(assetPath)
                .Select(backupPath =>
                    new BackupFile(
                        backupPath,
                        ParseBackupPathTimestamp(backupPath)));
        }
    }

    public struct BackupFile
    {
        public readonly string path;

        public readonly DateTime time;

        public BackupFile(string path, DateTime time)
        {
            this.path = path;
            this.time = time;
        }
    }

}