using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace UnityExtensions
{

    public static class AssetBackupPreferences
    {

        private struct BoolPreference
        {

            private readonly string m_key;

            private readonly bool m_defaultValue;

            private bool? m_value;

            public BoolPreference(string key, bool defaultValue)
            {
                m_key = key;
                m_defaultValue = defaultValue;
                m_value = null;
            }

            public bool value { get { return Get(); } set { Set(value); } }

            private bool Get()
            {
                if (m_value == null)
                {
                    m_value = EditorPrefs.GetBool(m_key, m_defaultValue);
                }
                return m_value.Value;
            }

            private void Set(bool value)
            {
                m_value = value;
                EditorPrefs.SetBool(m_key, value);
            }

        }

        //----------------------------------------------------------------------

        private struct IntPreference
        {

            private readonly string m_key;

            private readonly int m_defaultValue;

            private int? m_value;

            public IntPreference(string key, int defaultValue)
            {
                m_key = key;
                m_defaultValue = defaultValue;
                m_value = null;
            }

            public int value { get { return Get(); } set { Set(value); } }

            private int Get()
            {
                if (m_value == null)
                {
                    m_value = EditorPrefs.GetInt(m_key, m_defaultValue);
                }
                return m_value.Value;
            }

            private void Set(int value)
            {
                m_value = value;
                EditorPrefs.SetInt(m_key, value);
            }

        }

        //----------------------------------------------------------------------

        private static BoolPreference s_assetBackupDisabled =
            new BoolPreference(
                typeof(AssetBackupPreferences).FullName + "." +
                nameof(assetBackupDisabled),
                defaultValue: false);

        public static bool assetBackupDisabled 
        {
            get { return s_assetBackupDisabled.value; }
            set { s_assetBackupDisabled.value = value; }
        }

        public static bool assetBackupEnabled
        {
            get { return !assetBackupDisabled; }
            set { assetBackupDisabled = !value; }
        }

        //----------------------------------------------------------------------

        private static IntPreference s_maxBackupsPerAsset =
            new IntPreference(
                typeof(AssetBackupPreferences).FullName + "." +
                nameof(maxBackupsPerAsset),
                defaultValue: 3);

        public static int maxBackupsPerAsset 
        {
            get { return s_maxBackupsPerAsset.value; }
            set { s_maxBackupsPerAsset.value = Mathf.Max(1, value); }
        }

        //----------------------------------------------------------------------

        [UnityEditor.PreferenceItem("Asset Backup")]
        private static void OnPreferenceGUI()
        {
            var projectPath = AssetBackup.ProjectPath;
            var backupsPath = Path.Combine(projectPath, "Backups");
            EditorGUILayout.HelpBox(
                "When Asset Backup is enabled, a backup copy of the asset "+
                "will be made just before it is saved by Unity.\n\n"+
                "Backups are stored in <project-path>/Backups",
                MessageType.None);

            var oldAssetBackupEnabled = assetBackupEnabled;
            var newAssetBackupEnabled =
                EditorGUILayout
                .Toggle("Enabled", oldAssetBackupEnabled);
            if (newAssetBackupEnabled != oldAssetBackupEnabled)
                assetBackupEnabled = newAssetBackupEnabled;

            var oldMaxBackupsPerAsset = maxBackupsPerAsset;
            var newMaxBackupsPerAsset =
                EditorGUILayout
                .IntField("Max Backups Per Asset", oldMaxBackupsPerAsset);
            if (newMaxBackupsPerAsset != oldMaxBackupsPerAsset)
                maxBackupsPerAsset = newMaxBackupsPerAsset;

            EditorGUI.BeginDisabledGroup(!AssetBackup.AnyAssetBackupsExist());
            var showDeleteAllAssetBackupsDialog = default(bool);
            using (GUIColorScope(Color.Lerp(Color.red, GUI.color, 0.5f)))
            {
                showDeleteAllAssetBackupsDialog =
                    GUILayout.Button("Delete All Asset Backups");
            }
            if (showDeleteAllAssetBackupsDialog)
            {
                var deleteAllAssetBackups =
                    EditorUtility.DisplayDialog(
                        "Delete All Asset Backups?",
                        "You cannot undo this action.",
                        "Delete", "Cancel");
                if (deleteAllAssetBackups)
                    AssetBackup.DeleteAllAssetBackups();
            }
            EditorGUI.EndDisabledGroup();
        }

        //----------------------------------------------------------------------

        private struct Deferred : IDisposable
        {
            private readonly Action _onDispose;

            public Deferred(Action onDispose)
            {
                _onDispose = onDispose;
            }

            public void Dispose()
            {
                if (_onDispose != null)
                    _onDispose();
            }
        }

        private static IDisposable GUIColorScope(Color newColor)
        {
            var oldColor = GUI.color;
            GUI.color = newColor;
            return new Deferred(() => GUI.color -= oldColor);
        }

    }

}