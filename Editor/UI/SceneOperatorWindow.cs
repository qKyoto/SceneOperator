using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace SceneOperator.Editor
{
    /// <summary>
    /// The main window: the selected collection as one flat list of scenes, groups separated by a divider.
    /// A click opens a scene, Ctrl+click adds it to the open ones, the row buttons cover the rest.
    /// </summary>
    internal sealed class SceneOperatorWindow : EditorWindow
    {
        private const double PollInterval = 0.4;

        private VisualElement _listContainer;
        private Label _collectionLabel;
        private readonly List<SceneRow> _rows = new List<SceneRow>();
        private SceneCollection _builtFor;
        private int _stateHash;
        private double _nextPoll;

        [MenuItem("Tools/Scene Operator", false, 0)]
        public static void ShowWindow()
        {
            var window = GetWindow<SceneOperatorWindow>();
            window.titleContent = new GUIContent("Scene Operator", PackageResources.BuiltIn("SceneAsset Icon"));
            window.minSize = new Vector2(240f, 120f);
            window.Show();
        }

        private void OnEnable()
        {
            SceneOps.Changed += OnSceneStateChanged;
            CollectionRegistry.Changed += Rebuild;
            SceneOperatorSettings.Changed += Rebuild;
            EditorApplication.update += Poll;
        }

        private void OnDisable()
        {
            SceneOps.Changed -= OnSceneStateChanged;
            CollectionRegistry.Changed -= Rebuild;
            SceneOperatorSettings.Changed -= Rebuild;
            EditorApplication.update -= Poll;
        }

        private void CreateGUI()
        {
            if (PackageResources.StyleSheet != null)
                rootVisualElement.styleSheets.Add(PackageResources.StyleSheet);

            rootVisualElement.AddToClassList("so-root");
            BuildChrome();
            Rebuild();
        }

        // Scene dirty flags raise no events, so the rows are refreshed from a cheap fingerprint.
        private void Poll()
        {
            if (EditorApplication.timeSinceStartup < _nextPoll)
                return;
            _nextPoll = EditorApplication.timeSinceStartup + PollInterval;

            int hash = SceneOps.StateHash();
            if (hash == _stateHash)
                return;
            _stateHash = hash;
            UpdateRows();
        }

        private void OnSceneStateChanged()
        {
            _stateHash = SceneOps.StateHash();
            UpdateRows();
        }

        private void UpdateRows()
        {
            foreach (SceneRow row in _rows)
                row.UpdateState();
        }

        // ---------------------------------------------------------------- chrome

        private void BuildChrome()
        {
            rootVisualElement.Clear();
            rootVisualElement.style.backgroundColor = SceneOperatorSettings.Background.Value;

            var toolbar = new VisualElement();
            toolbar.AddToClassList("so-toolbar");

            var collectionButton = new Button(ShowCollectionMenu);
            collectionButton.AddToClassList("so-collection-button");
            collectionButton.tooltip = "Select the collection to show";
            _collectionLabel = new Label();
            _collectionLabel.AddToClassList("so-collection-label");
            var arrow = new VisualElement();
            arrow.AddToClassList("so-collection-arrow");
            SetIcon(arrow, PackageResources.BuiltIn("icon dropdown"));
            collectionButton.Add(_collectionLabel);
            collectionButton.Add(arrow);

            toolbar.Add(collectionButton);
            toolbar.Add(IconButton(PackageResources.BuiltIn("Project"), "Show the collection in the Project window", PingCollection));
            toolbar.Add(IconButton(PackageResources.BuiltIn("Search Icon"), "Open the scene switcher", () => SceneSwitcherPopup.Open()));
            toolbar.Add(IconButton(PackageResources.BuiltIn("_Popup"), "Scene Operator preferences", OpenPreferences));

            rootVisualElement.Add(toolbar);

            var divider = new VisualElement();
            divider.AddToClassList("so-toolbar-divider");
            divider.style.backgroundColor = SceneOperatorSettings.Separator.Value;
            rootVisualElement.Add(divider);

            var scroll = new ScrollView();
            scroll.AddToClassList("so-scroll");
            _listContainer = scroll.contentContainer;
            rootVisualElement.Add(scroll);

            rootVisualElement.Add(BuildSignature());
        }

        private VisualElement BuildSignature()
        {
            var signature = new VisualElement();
            signature.AddToClassList("so-signature");

            var prefix = new Label("by");
            prefix.style.color = SceneOperatorSettings.MutedText.Value;

            var icon = new VisualElement();
            icon.AddToClassList("so-signature-icon");
            SetIcon(icon, PackageResources.Sprite("Sprite_Signature_Variant_1.png"));

            var postfix = new Label("Kyoto");
            postfix.style.color = SceneOperatorSettings.MutedText.Value;

            signature.Add(prefix);
            signature.Add(icon);
            signature.Add(postfix);
            return signature;
        }

        private void ShowCollectionMenu()
        {
            var menu = new GenericMenu();
            IReadOnlyList<SceneCollection> collections = CollectionRegistry.Collections;
            SceneCollection selected = CollectionRegistry.Selected;

            foreach (SceneCollection collection in collections)
            {
                SceneCollection captured = collection;
                string label = collection.name.Replace('/', '∕'); // a slash would create a submenu
                menu.AddItem(new GUIContent(label), collection == selected, () => CollectionRegistry.Select(captured));
            }

            if (collections.Count > 0)
                menu.AddSeparator(string.Empty);
            menu.AddItem(new GUIContent("Create New Collection..."), false, () => CollectionRegistry.CreateCollection());
            menu.ShowAsContext();
        }

        private void PingCollection()
        {
            SceneCollection collection = CollectionRegistry.Selected;
            if (collection == null)
                return;
            Selection.activeObject = collection;
            EditorGUIUtility.PingObject(collection);
        }

        private static void OpenPreferences() => SettingsService.OpenUserPreferences(SceneOperatorPreferences.Path);

        // ---------------------------------------------------------------- list

        private void Rebuild()
        {
            if (_listContainer == null)
                return;

            BuildChrome();
            _rows.Clear();
            _listContainer.Clear();

            SceneCollection collection = CollectionRegistry.Selected;
            _builtFor = collection;
            _collectionLabel.text = collection != null ? collection.name : "No collection";
            _collectionLabel.style.color = collection != null
                ? SceneOperatorSettings.Text.Value
                : SceneOperatorSettings.MutedText.Value;

            if (collection == null)
            {
                _listContainer.Add(EmptyMessage(CollectionRegistry.Collections.Count == 0
                    ? "No scene collections in this project.\nCreate one to get started."
                    : "Select a collection above."));

                if (CollectionRegistry.Collections.Count == 0)
                {
                    var create = new Button(() => CollectionRegistry.CreateCollection()) { text = "Create Collection" };
                    create.AddToClassList("so-empty-button");
                    _listContainer.Add(create);
                }
                return;
            }

            List<SceneItem> items = SceneCatalog.FromCollection(collection);
            if (items.Count == 0)
            {
                _listContainer.Add(EmptyMessage("This collection is empty.\nAdd groups and scenes in the Inspector."));
                var select = new Button(PingCollection) { text = "Show Collection" };
                select.AddToClassList("so-empty-button");
                _listContainer.Add(select);
                return;
            }

            int previousGroup = -1;
            for (int i = 0; i < items.Count; i++)
            {
                SceneItem item = items[i];
                if (i > 0 && item.GroupIndex != previousGroup)
                    _listContainer.Add(GroupDivider(item));
                else if (i == 0 && SceneOperatorSettings.ShowGroupNames && !string.IsNullOrEmpty(item.GroupName))
                    _listContainer.Add(GroupLabel(item.GroupName));

                previousGroup = item.GroupIndex;

                var row = new SceneRow(item, this);
                _rows.Add(row);
                _listContainer.Add(row);
            }

            _stateHash = SceneOps.StateHash();
            UpdateRows();
        }

        private VisualElement GroupDivider(SceneItem nextItem)
        {
            var wrapper = new VisualElement();
            wrapper.AddToClassList("so-divider-wrapper");

            var line = new VisualElement();
            line.AddToClassList("so-divider");
            line.style.backgroundColor = SceneOperatorSettings.Separator.Value;
            wrapper.Add(line);

            if (SceneOperatorSettings.ShowGroupNames && !string.IsNullOrEmpty(nextItem.GroupName))
                wrapper.Add(GroupLabel(nextItem.GroupName));

            return wrapper;
        }

        private static Label GroupLabel(string text)
        {
            var label = new Label(text);
            label.AddToClassList("so-group-label");
            label.style.color = SceneOperatorSettings.MutedText.Value;
            return label;
        }

        private static Label EmptyMessage(string text)
        {
            var label = new Label(text);
            label.AddToClassList("so-empty");
            label.style.color = SceneOperatorSettings.MutedText.Value;
            return label;
        }

        /// <summary>All scene paths of the group the row belongs to, in collection order.</summary>
        internal List<string> GroupPaths(int groupIndex)
        {
            var paths = new List<string>();
            if (_builtFor == null || groupIndex < 0)
                return paths;

            foreach (SceneItem item in SceneCatalog.FromCollection(_builtFor))
            {
                if (item.GroupIndex == groupIndex && !item.Missing && !string.IsNullOrEmpty(item.Path))
                    paths.Add(item.Path);
            }
            return paths;
        }

        // ---------------------------------------------------------------- helpers

        private static Button IconButton(Texture2D icon, string tooltip, System.Action onClick)
        {
            var button = new Button(onClick) { tooltip = tooltip };
            button.AddToClassList("so-icon-button");
            SetIcon(button, icon);
            return button;
        }

        private static void SetIcon(VisualElement element, Texture2D icon)
        {
            if (icon != null)
                element.style.backgroundImage = new StyleBackground(icon);
        }

        // ---------------------------------------------------------------- row

        private sealed class SceneRow : VisualElement
        {
            private readonly SceneItem _item;
            private readonly SceneOperatorWindow _window;
            private readonly VisualElement _accent;
            private readonly Label _name;
            private readonly Label _dirty;
            private readonly Button _additive;
            private readonly Button _play;
            private readonly Button _close;
            private bool _hovered;

            public SceneRow(SceneItem item, SceneOperatorWindow window)
            {
                _item = item;
                _window = window;

                AddToClassList("so-row");
                style.height = SceneOperatorSettings.RowHeight;

                _accent = new VisualElement();
                _accent.AddToClassList("so-row-accent");
                Add(_accent);

                _name = new Label(item.Name);
                _name.AddToClassList("so-row-name");
                Add(_name);

                _dirty = new Label("*");
                _dirty.AddToClassList("so-row-dirty");
                _dirty.style.color = SceneOperatorSettings.DirtyMarker.Value;
                Add(_dirty);

                _additive = IconButton(PackageResources.BuiltIn("Toolbar Plus"), "Open additively (Ctrl+click the row)", () => SceneOps.OpenAdditive(_item.Path));
                _play = IconButton(PackageResources.BuiltIn("PlayButton"), "Open this scene alone and enter play mode", () => SceneOps.OpenAndPlay(_item.Path));
                Button ping = IconButton(PackageResources.BuiltIn("Project"), "Show in the Project window", () => SceneOps.Ping(_item.Path));
                _close = IconButton(PackageResources.BuiltIn("Toolbar Minus"), "Close this scene", () => SceneOps.Close(_item.Path));

                Add(_close);
                Add(_additive);
                Add(_play);
                Add(ping);

                RegisterCallback<MouseEnterEvent>(_ => SetHovered(true));
                RegisterCallback<MouseLeaveEvent>(_ => SetHovered(false));
                RegisterCallback<MouseDownEvent>(OnMouseDown);
                this.AddManipulator(new ContextualMenuManipulator(BuildContextMenu));

                if (item.Missing)
                {
                    SetEnabled(false);
                    tooltip = "The scene asset referenced by the collection is missing.";
                }
                else
                {
                    tooltip = item.Path;
                }
            }

            private void OnMouseDown(MouseDownEvent evt)
            {
                if (evt.button != 0 || _item.Missing)
                    return;

                if (evt.ctrlKey || evt.commandKey)
                    SceneOps.OpenAdditive(_item.Path);
                else
                    SceneOps.OpenSingle(_item.Path);

                evt.StopPropagation();
            }

            private void BuildContextMenu(ContextualMenuPopulateEvent evt)
            {
                if (_item.Missing)
                    return;

                bool canModify = SceneOps.CanModifyScenes;
                DropdownMenuAction.Status enabled = canModify ? DropdownMenuAction.Status.Normal : DropdownMenuAction.Status.Disabled;

                evt.menu.AppendAction("Open", _ => SceneOps.OpenSingle(_item.Path), enabled);
                evt.menu.AppendAction("Open Additively", _ => SceneOps.OpenAdditive(_item.Path), enabled);
                evt.menu.AppendAction("Open and Play", _ => SceneOps.OpenAndPlay(_item.Path), enabled);
                evt.menu.AppendSeparator();

                bool loaded = SceneOps.IsLoaded(_item.Path);
                evt.menu.AppendAction("Set Active", _ => SceneOps.SetActive(_item.Path),
                    loaded && !SceneOps.IsActive(_item.Path) ? DropdownMenuAction.Status.Normal : DropdownMenuAction.Status.Disabled);
                evt.menu.AppendAction("Save", _ => SceneOps.Save(_item.Path),
                    SceneOps.IsDirty(_item.Path) ? DropdownMenuAction.Status.Normal : DropdownMenuAction.Status.Disabled);
                evt.menu.AppendAction("Close", _ => SceneOps.Close(_item.Path),
                    loaded && canModify && SceneOps.OpenSceneCount > 1 ? DropdownMenuAction.Status.Normal : DropdownMenuAction.Status.Disabled);
                evt.menu.AppendSeparator();

                if (_item.InCollection)
                {
                    evt.menu.AppendAction("Open Whole Group", _ => SceneOps.OpenGroup(_window.GroupPaths(_item.GroupIndex), false), enabled);
                    evt.menu.AppendAction("Open Whole Group Additively", _ => SceneOps.OpenGroup(_window.GroupPaths(_item.GroupIndex), true), enabled);
                    evt.menu.AppendSeparator();
                }

                evt.menu.AppendAction("Show in Project", _ => SceneOps.Ping(_item.Path));
            }

            private void SetHovered(bool hovered)
            {
                _hovered = hovered;
                UpdateState();
            }

            public void UpdateState()
            {
                bool loaded = !_item.Missing && SceneOps.IsLoaded(_item.Path);
                bool open = !_item.Missing && SceneOps.IsOpen(_item.Path);
                bool active = !_item.Missing && SceneOps.IsActive(_item.Path);
                bool dirty = !_item.Missing && SceneOps.IsDirty(_item.Path);
                bool canModify = SceneOps.CanModifyScenes;

                Color background = Color.clear;
                if (loaded)
                    background = SceneOperatorSettings.RowLoaded.Value;
                else if (open)
                    background = Fade(SceneOperatorSettings.RowLoaded.Value, 0.45f);
                if (_hovered && !_item.Missing)
                    background = loaded || open ? Lighten(background, SceneOperatorSettings.RowHover.Value) : SceneOperatorSettings.RowHover.Value;
                style.backgroundColor = background;

                _accent.style.backgroundColor = active ? SceneOperatorSettings.ActiveAccent.Value : Color.clear;

                _name.style.color = _item.Missing ? SceneOperatorSettings.MutedText.Value : SceneOperatorSettings.Text.Value;
                _name.style.unityFontStyleAndWeight = active ? FontStyle.Bold : FontStyle.Normal;
                _dirty.style.display = dirty ? DisplayStyle.Flex : DisplayStyle.None;

                _additive.SetEnabled(canModify && !loaded);
                _play.SetEnabled(canModify);
                _close.SetEnabled(canModify && open && SceneOps.OpenSceneCount > 1);
            }

            private static Color Fade(Color color, float alpha) => new Color(color.r, color.g, color.b, color.a * alpha);

            private static Color Lighten(Color background, Color hover) => Color.Lerp(background, hover, 0.35f);
        }
    }
}
