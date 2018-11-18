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

        private static readonly string ProjectPath =
            Path.GetDirectoryName(UnityEngine.Application.dataPath);

        //----------------------------------------------------------------------

        private static AssetMoveResult OnWillMoveAsset(
            string sourcePath,
            string targetPath)
        {
            Debug.Log($"TODO: move backups from '{sourcePath}' to '{targetPath}'");
            return AssetMoveResult.DidNotMove;
        }

        private static string[] OnWillSaveAssets(string[] assetPaths)
        {
            foreach (var assetPath in assetPaths)
            {
                BackupAsset(assetPath);
                var assetMetaPath = $"{assetPath}.meta";
                if (assetPaths.Contains(assetMetaPath) == false)
                    BackupAsset(assetMetaPath);
            }
            return assetPaths;
        }

        //----------------------------------------------------------------------

        private const string AssetPathRoot = "Assets/";

        private static bool HasAssetPathRoot(string assetPath)
        {
            return assetPath?.StartsWith(AssetPathRoot) ?? false;
        }

        //----------------------------------------------------------------------

        public static void BackupAsset(string assetPath)
        {
            if (!HasAssetPathRoot(assetPath))
                return;

            var sourcePath = Path.Combine(ProjectPath, assetPath);
            if (!File.Exists(sourcePath))
                return;

            var backupTime = File.GetLastWriteTime(sourcePath);
            var backupPath =
                FormatBackupPathWithoutTimestamp(assetPath) +
                FormatTimestamp(backupTime);
            var backupDir = Path.GetDirectoryName(backupPath);
            if (Directory.Exists(backupDir) == false)
                Directory.CreateDirectory(backupDir);

            if (File.Exists(backupPath) == false)
            {
                File.Copy(sourcePath, backupPath);
                File.SetLastWriteTime(backupPath, backupTime);
            }
            var shortBackupPath = GetShortBackupPath(backupPath);
            Debug.Log($"backed up '{assetPath}' to '{shortBackupPath}'");
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
            return Path.Combine(ProjectPath, $"Backups/{assetSubpath}");
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
                EnumerateBackupPaths(assetPath).Any();
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
            Debug.Log($"restored '{assetPath}' from '{shortBackupPath}'");
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

        public static IEnumerable<string> EnumerateBackupPaths(
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
                .OrderBy(backupPath => backupPath);
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
                EnumerateBackupPaths(assetPath)
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