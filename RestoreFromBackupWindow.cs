using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.IMGUI.Controls;
using UnityEngine;
using UnityEngine.Experimental.UIElements;
using Object = UnityEngine.Object;

namespace UnityExtensions
{

    public class RestoreFromBackupWindow : EditorWindow
    {

        private static readonly Type Toolbar =
            typeof(EditorGUI)
            .Assembly
            .GetType("UnityEditor.Toolbar");

        private static readonly FieldInfo Toolbar_get =
            Toolbar
            .GetField("get");

        //----------------------------------------------------------------------

        private static readonly Type View =
            typeof(EditorGUI)
            .Assembly
            .GetType("UnityEditor.View");

        private static readonly FieldInfo View_m_Window =
            View
            .GetField(
                "m_Window",
                BindingFlags.Instance |
                BindingFlags.Public |
                BindingFlags.NonPublic);

        //----------------------------------------------------------------------

        private static readonly Type ContainerWindow =
            typeof(EditorGUI)
            .Assembly
            .GetType("UnityEditor.ContainerWindow");

        private static readonly FieldInfo ContainerWindow_m_PixelRect =
            ContainerWindow
            .GetField(
                "m_PixelRect",
                BindingFlags.Instance |
                BindingFlags.Public |
                BindingFlags.NonPublic);

        //----------------------------------------------------------------------

        private static Rect GetUnityEditorPosition()
        {
            var toolbar = Toolbar_get.GetValue(null);
            if (toolbar == null)
                return default(Rect);

            var containerWindow = View_m_Window.GetValue(toolbar);
            if (containerWindow == null)
                return default(Rect);

            return (Rect)ContainerWindow_m_PixelRect.GetValue(containerWindow);
        }

        //----------------------------------------------------------------------

        [SerializeField] private Object m_asset;

        private TreeViewState m_backupViewState;

        private BackupView m_backupView;

        public static RestoreFromBackupWindow Open(Object asset)
        {
            var lineHeight = EditorGUIUtility.singleLineHeight;
            var lineSpacing = EditorGUIUtility.standardVerticalSpacing;
            var windowWidth = 312;
            var windowHeight = Mathf.Floor(windowWidth / 1.61f);

            var windowPosition = GetUnityEditorPosition();
            windowPosition.position = windowPosition.center;
            windowPosition.x -= windowWidth / 2;
            windowPosition.y -= windowHeight / 2;
            windowPosition.width = windowWidth;
            windowPosition.height = windowHeight;

            var window = CreateInstance<RestoreFromBackupWindow>();
            window.titleContent.text = "Restore From Backup";
            window.m_asset = asset;
            window.ShowUtility();
            window.position = windowPosition;
            return window;
        }

        private void Update()
        {
            // if (EditorWindow.focusedWindow != this)
            // {
            //     Close();
            //     return;
            // }
            // Repaint();
        }

        private void OnGUI()
        {
            if (m_backupViewState == null)
                m_backupViewState = new TreeViewState();

            if (m_backupView == null)
                m_backupView = new BackupView(m_backupViewState, m_asset);

            var lineHeight = EditorGUIUtility.singleLineHeight;
            var lineSpacing = EditorGUIUtility.standardVerticalSpacing;

            var viewport =
                GUILayoutUtility
                .GetRect(
                    0, 0,
                    GUILayout.ExpandWidth(true),
                    GUILayout.ExpandHeight(true));

            const float margin = 4;

            viewport.yMin += margin;

            var topRowRect = viewport;
            topRowRect.xMin += margin;
            topRowRect.xMax -= margin;
            topRowRect.height = lineHeight;

            var bottomRowRect = topRowRect;
            bottomRowRect.y = viewport.yMax - lineHeight - margin;

            var oldAsset = m_asset;
            var newAsset = EditorGUI.ObjectField(
                topRowRect,
                GUIContent.none,
                oldAsset,
                typeof(Object),
                allowSceneObjects: false);
            if (newAsset != oldAsset)
            {
                m_backupView.asset = m_asset = newAsset;
            }

            var backupViewRect = viewport;
            backupViewRect.xMin -= 1;
            backupViewRect.xMax += 1;
            backupViewRect.yMin = topRowRect.yMax + margin;
            backupViewRect.yMax = bottomRowRect.yMin - margin;

            m_backupView.OnGUI(backupViewRect);

            var buttonStyle = EditorStyles.miniButton;
            var content = new GUIContent("Restore");
            var restoreSize = buttonStyle.CalcSize(content);

            var restoreRect = bottomRowRect;

            EditorGUI.BeginDisabledGroup(!m_backupView.HasSelection());
            var restore = GUI.Button(restoreRect, content, buttonStyle);
            if (restore)
            {
                var selectedItem = m_backupView.selectedItem;
                var selectedFile = selectedItem.file;
                var backupPath = selectedFile.path;
                AssetBackup.RestoreFromBackup(m_asset, backupPath);
            }
            EditorGUI.EndDisabledGroup();
        }

        private class BackupView : TreeView
        {

            private Object m_asset;

            private readonly TreeViewItem m_rootItem =
                new TreeViewItem(id: -1, depth: -1)
                {
                    children = new List<TreeViewItem>(),
                };

            public BackupView(TreeViewState state, Object asset)
            : base(state)
            {
                this.showAlternatingRowBackgrounds = true;
                this.showBorder = true;
                this.rowHeight += 2;
                m_asset = asset;
                Reload();
            }

            public Object asset
            {
                get { return m_asset; }
                set
                {
                    if (m_asset != value)
                    {
                        m_asset = value;
                        Reload();
                    }
                }
            }

            public BackupItem selectedItem
            {
                get
                {
                    var selection = GetSelection();
                    if (selection.Count == 0)
                        return null;

                    var selectedId = selection.First();
                    var selectedItem = FindItem(selectedId, m_rootItem);
                    return selectedItem as BackupItem;
                }
            }

            protected override TreeViewItem BuildRoot()
            {
                m_rootItem.children.Clear();
                if (m_asset != null)
                {
                    var assetIcon =
                        EditorGUIUtility
                        .ObjectContent(m_asset, m_asset.GetType())
                        .image as Texture2D;
                    var backupFiles =
                        AssetBackup
                        .EnumerateBackupFiles(m_asset)
                        .Reverse();
                    foreach (var backupFile in backupFiles)
                    {
                        var id = m_rootItem.children.Count;
                        var item = new BackupItem(id, backupFile);
                        item.icon = assetIcon;
                        m_rootItem.AddChild(item);
                    }
                }
                return m_rootItem;
            }

            protected override bool CanMultiSelect(TreeViewItem item)
            {
                return false;
            }

        }

        private class BackupItem : TreeViewItem
        {
            public readonly BackupFile file;

            public BackupItem(int id, BackupFile file)
            : base(id, depth: 0)
            {
                var time = file.time;
                var now = DateTime.Now;
                var age = now - time;
                var today =
                    age.TotalDays < 1 &&
                    now.Day == time.Day;
                var yesterday =
                    age.TotalDays < 2 &&
                    now.Day == time.Day + 1;
                var dateString =
                    today ? "Today" :
                    yesterday ? "Yesterday" :
                    time.ToLongDateString();
                var timeString = time.ToLongTimeString();
                this.displayName = $"{dateString}, {timeString}";
                this.file = file;
            }
        }

    }

}