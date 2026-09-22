using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.IMGUI.Controls;
using UnityEngine;

namespace SceneOperator.Editor
{
    /// <summary>
    /// The quick switcher: a search field over the scenes of the selected collection and, optionally,
    /// every scene of the project. Enter opens, Ctrl+Enter opens additively, Shift+Enter opens and plays.
    /// </summary>
    internal sealed class SceneSwitcherPopup : EditorWindow
    {
        private const float RowHeight = 20f;
        private const float HeaderHeight = 18f;
        private const float DividerHeight = 7f;
        private const float SearchBarHeight = 28f;
        private const float FooterHeight = 18f;
        private const float ListPadding = 3f;
        private const float SidePadding = 8f;
        private const float Width = 460f;
        private const double ClickGuardSeconds = 0.12;

        private static SceneSwitcherPopup s_Current;

        private readonly List<Row> _rows = new List<Row>();
        private readonly List<float> _rowTops = new List<float>();
        private List<SceneItem> _collectionScenes;
        private List<SceneItem> _projectScenes;
        private float _contentHeight;
        private string _search = string.Empty;
        private int _selected = -1;
        private Vector2 _scroll;
        private SearchField _searchField;
        private double _openTime;
        private bool _scrollToSelected;
        private bool _resizePending;

        private bool SearchMode => !string.IsNullOrWhiteSpace(_search);

        public static void Open()
        {
            CloseCurrent();

            var window = CreateInstance<SceneSwitcherPopup>();
            window._searchField = new SearchField { autoSetFocusOnFindCommand = false };
            window._openTime = EditorApplication.timeSinceStartup;
            window.LoadScenes();
            window.RefreshRows();
            window.wantsMouseMove = true;

            Rect anchor = AnchorRect();
            window.ShowAsDropDown(anchor, new Vector2(Width, window.DesiredHeight()));
            window._searchField.SetFocus();
            window.Focus();
            s_Current = window;
        }

        public static void CloseCurrent()
        {
            if (s_Current == null)
                return;
            var window = s_Current;
            s_Current = null;
            try { window.Close(); } catch { /* already closed */ }
        }

        private void OnDisable()
        {
            if (s_Current == this)
                s_Current = null;
        }

        /// <summary>A zero-height rect near the top of the editor, so the popup drops down centered.</summary>
        private static Rect AnchorRect()
        {
            Rect main = EditorGUIUtility.GetMainWindowPosition();
            if (main.width < 1f || main.height < 1f)
                main = new Rect(0f, 0f, Screen.currentResolution.width, Screen.currentResolution.height);

            float x = main.x + (main.width - Width) * 0.5f;
            float y = main.y + Mathf.Min(120f, main.height * 0.18f);
            return new Rect(x, y, Width, 0f);
        }

        // ---------------------------------------------------------------- data

        private void LoadScenes()
        {
            _collectionScenes = SceneCatalog.FromCollection(CollectionRegistry.Selected);
            _collectionScenes.RemoveAll(item => item.Missing || string.IsNullOrEmpty(item.Path));

            if (!SceneOperatorSettings.SwitcherIncludesProjectScenes)
            {
                _projectScenes = new List<SceneItem>();
                return;
            }

            var taken = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (SceneItem item in _collectionScenes)
                taken.Add(item.Path);

            _projectScenes = SceneCatalog.ProjectScenes(taken);
        }

        private void RefreshRows()
        {
            SceneItem? previous = _selected >= 0 && _selected < _rows.Count ? _rows[_selected].Item : (SceneItem?)null;

            _rows.Clear();

            if (SearchMode)
            {
                List<SceneItem> matches = Filter(_collectionScenes, _search);
                List<SceneItem> projectMatches = Filter(_projectScenes, _search);

                foreach (SceneItem item in matches)
                    _rows.Add(Row.Scene(item));

                if (projectMatches.Count > 0)
                {
                    if (matches.Count > 0)
                        _rows.Add(Row.Header("Other scenes in the project"));
                    foreach (SceneItem item in projectMatches)
                        _rows.Add(Row.Scene(item));
                }
            }
            else
            {
                int previousGroup = int.MinValue;
                foreach (SceneItem item in _collectionScenes)
                {
                    if (previousGroup != int.MinValue && item.GroupIndex != previousGroup)
                        _rows.Add(Row.Divider());
                    previousGroup = item.GroupIndex;
                    _rows.Add(Row.Scene(item));
                }

                if (_projectScenes.Count > 0)
                {
                    _rows.Add(Row.Header(_collectionScenes.Count > 0 ? "Other scenes in the project" : "Scenes in the project"));
                    foreach (SceneItem item in _projectScenes)
                        _rows.Add(Row.Scene(item));
                }
            }

            _rowTops.Clear();
            float y = ListPadding;
            foreach (Row row in _rows)
            {
                _rowTops.Add(y);
                y += row.Height;
            }
            _contentHeight = y + ListPadding;

            _selected = -1;
            if (previous.HasValue)
            {
                for (int i = 0; i < _rows.Count; i++)
                {
                    if (_rows[i].IsScene && string.Equals(_rows[i].Item.Path, previous.Value.Path, StringComparison.OrdinalIgnoreCase))
                    {
                        _selected = i;
                        break;
                    }
                }
            }
            if (_selected < 0)
                _selected = FirstSelectable();

            _scroll = Vector2.zero;
            _scrollToSelected = _selected >= 0;
            _resizePending = true;
        }

        private static List<SceneItem> Filter(List<SceneItem> source, string query)
        {
            var results = new List<KeyValuePair<int, SceneItem>>();
            string[] words = query.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);

            for (int i = 0; i < source.Count; i++)
            {
                int score = Score(source[i], words);
                if (score >= 0)
                    results.Add(new KeyValuePair<int, SceneItem>(score * 10000 + i, source[i]));
            }

            results.Sort((a, b) => a.Key.CompareTo(b.Key));

            var items = new List<SceneItem>(results.Count);
            foreach (var pair in results)
                items.Add(pair.Value);
            return items;
        }

        // Lower is better, negative means "no match". Every word must appear in the name or in the folder.
        private static int Score(SceneItem item, string[] words)
        {
            string name = item.Name ?? string.Empty;
            string folder = SceneCatalog.FolderOf(item.Path);
            int worst = 0;

            foreach (string word in words)
            {
                int index = name.IndexOf(word, StringComparison.OrdinalIgnoreCase);
                int rank;
                if (index == 0)
                    rank = 0;
                else if (index > 0 && IsWordStart(name, index))
                    rank = 1;
                else if (index > 0)
                    rank = 2;
                else if (folder.IndexOf(word, StringComparison.OrdinalIgnoreCase) >= 0)
                    rank = 3;
                else
                    return -1;

                if (rank > worst)
                    worst = rank;
            }
            return worst;
        }

        private static bool IsWordStart(string text, int index)
        {
            char previous = text[index - 1];
            if (char.IsWhiteSpace(previous) || previous == '_' || previous == '-' || previous == '.' || previous == '/')
                return true;
            return char.IsUpper(text[index]) && char.IsLower(previous);
        }

        // ---------------------------------------------------------------- layout

        private float DesiredHeight()
        {
            float content = Mathf.Max(_contentHeight, RowHeight + ListPadding * 2f);
            float desired = Mathf.Ceil(SearchBarHeight + content + FooterHeight + 1f) + 2f;
            float screenLimit = Screen.currentResolution.height / Mathf.Max(1f, EditorGUIUtility.pixelsPerPoint) - 80f;
            float limit = Mathf.Min(SceneOperatorSettings.SwitcherMaxHeight, screenLimit);
            return Mathf.Max(Mathf.Min(desired, limit), SearchBarHeight + RowHeight + FooterHeight + ListPadding * 2f);
        }

        private void ApplyPendingResize()
        {
            _resizePending = false;
            float height = DesiredHeight();
            if (Mathf.Abs(height - position.height) < 1f)
                return;

            var size = new Vector2(Width, height);
            var rect = new Rect(position.x, position.y, size.x, size.y);
            minSize = size;
            maxSize = size;
            position = rect;
        }

        private int FirstSelectable()
        {
            for (int i = 0; i < _rows.Count; i++)
                if (_rows[i].IsScene)
                    return i;
            return -1;
        }

        private int LastSelectable()
        {
            for (int i = _rows.Count - 1; i >= 0; i--)
                if (_rows[i].IsScene)
                    return i;
            return -1;
        }

        // ---------------------------------------------------------------- GUI

        private void OnGUI()
        {
            Event e = Event.current;
            Styles styles = Styles.Instance;

            if (e.type == EventType.Layout && _resizePending)
                ApplyPendingResize();

            var windowRect = new Rect(0f, 0f, position.width, position.height);
            if (e.type == EventType.Repaint)
            {
                EditorGUI.DrawRect(windowRect, SceneOperatorSettings.Background.Value);
                DrawBorder(windowRect, SceneOperatorSettings.Separator.Value);
            }

            HandleKeyboard(e);
            DrawSearchBar(styles);

            var listRect = new Rect(1f, SearchBarHeight, windowRect.width - 2f, windowRect.height - SearchBarHeight - FooterHeight - 1f);
            DrawList(listRect, styles, e);
            DrawFooter(new Rect(1f, windowRect.yMax - FooterHeight - 1f, windowRect.width - 2f, FooterHeight), styles);

            if (e.type == EventType.MouseDown)
                _searchField.SetFocus();
        }

        private static void DrawBorder(Rect r, Color color)
        {
            EditorGUI.DrawRect(new Rect(r.x, r.y, r.width, 1f), color);
            EditorGUI.DrawRect(new Rect(r.x, r.yMax - 1f, r.width, 1f), color);
            EditorGUI.DrawRect(new Rect(r.x, r.y, 1f, r.height), color);
            EditorGUI.DrawRect(new Rect(r.xMax - 1f, r.y, 1f, r.height), color);
        }

        private void DrawSearchBar(Styles styles)
        {
            var fieldRect = new Rect(6f, 5f, position.width - 12f, 18f);
            string text = _searchField.OnGUI(fieldRect, _search) ?? string.Empty;
            if (text != _search)
            {
                _search = text;
                RefreshRows();
            }

            if (_search.Length == 0 && Event.current.type == EventType.Repaint)
                styles.Placeholder.Draw(fieldRect, Temp("Search scenes..."), false, false, false, false);
        }

        private void DrawFooter(Rect rect, Styles styles)
        {
            if (Event.current.type != EventType.Repaint)
                return;

            EditorGUI.DrawRect(new Rect(rect.x, rect.y, rect.width, 1f), SceneOperatorSettings.Separator.Value);
            styles.Footer.Draw(new Rect(rect.x + SidePadding, rect.y, rect.width - SidePadding * 2f, rect.height),
                Temp("Enter  open      Ctrl+Enter  additive      Shift+Enter  play"), false, false, false, false);
        }

        private void DrawList(Rect listRect, Styles styles, Event e)
        {
            bool needScroll = _contentHeight > listRect.height + ListPadding;
            GUIStyle scrollbarStyle = needScroll ? GUI.skin.verticalScrollbar : GUIStyle.none;
            float scrollbarWidth = needScroll ? Mathf.Max(scrollbarStyle.fixedWidth, 12f) + 2f : 0f;
            float viewWidth = listRect.width - scrollbarWidth;
            var viewRect = new Rect(0f, 0f, viewWidth, needScroll ? _contentHeight : listRect.height);

            if (_scrollToSelected && _selected >= 0 && _selected < _rows.Count)
            {
                _scrollToSelected = false;
                float top = _rowTops[_selected];
                float bottom = top + _rows[_selected].Height;
                if (top < _scroll.y + ListPadding)
                    _scroll.y = top - ListPadding;
                else if (bottom > _scroll.y + listRect.height - ListPadding)
                    _scroll.y = bottom - listRect.height + ListPadding;
                _scroll.y = Mathf.Clamp(_scroll.y, 0f, Mathf.Max(0f, _contentHeight - listRect.height));
            }

            _scroll = GUI.BeginScrollView(listRect, _scroll, viewRect, false, needScroll, GUIStyle.none, scrollbarStyle);

            if (_rows.Count == 0)
            {
                GUI.Label(new Rect(SidePadding, ListPadding, viewWidth - SidePadding, RowHeight), Temp("No scenes found"), styles.Muted);
            }

            int hovered = -1;
            bool trackHover = e.type == EventType.MouseMove || e.type == EventType.MouseDrag || e.type == EventType.MouseDown;

            for (int i = 0; i < _rows.Count; i++)
            {
                Row row = _rows[i];
                var rowRect = new Rect(0f, _rowTops[i], viewWidth, row.Height);

                if (trackHover && row.IsScene && rowRect.Contains(e.mousePosition))
                    hovered = i;

                if (e.type == EventType.Repaint)
                    DrawRow(rowRect, row, i == _selected, styles);

                if (e.type == EventType.MouseDown && e.button == 0 && row.IsScene && rowRect.Contains(e.mousePosition)
                    && EditorApplication.timeSinceStartup - _openTime >= ClickGuardSeconds)
                {
                    _selected = i;
                    Execute(row.Item, e.control || e.command ? OpenMode.Additive : e.shift ? OpenMode.Play : OpenMode.Single);
                    e.Use();
                }
            }

            GUI.EndScrollView();

            if ((e.type == EventType.MouseMove || e.type == EventType.MouseDrag) && hovered >= 0 && hovered != _selected)
            {
                _selected = hovered;
                Repaint();
            }
        }

        private void DrawRow(Rect rowRect, Row row, bool selected, Styles styles)
        {
            if (row.IsDivider)
            {
                EditorGUI.DrawRect(new Rect(rowRect.x + SidePadding, rowRect.y + Mathf.Floor(rowRect.height * 0.5f),
                    rowRect.width - SidePadding * 2f, 1f), SceneOperatorSettings.Separator.Value);
                return;
            }

            if (row.IsHeader)
            {
                styles.Header.Draw(new Rect(rowRect.x + SidePadding, rowRect.y, rowRect.width - SidePadding * 2f, rowRect.height),
                    Temp(row.HeaderText), false, false, false, false);
                return;
            }

            SceneItem item = row.Item;
            bool loaded = SceneOps.IsLoaded(item.Path);
            bool active = SceneOps.IsActive(item.Path);

            if (selected)
                EditorGUI.DrawRect(new Rect(rowRect.x + 2f, rowRect.y, rowRect.width - 4f, rowRect.height), SceneOperatorSettings.Selection.Value);
            else if (loaded)
                EditorGUI.DrawRect(new Rect(rowRect.x + 2f, rowRect.y, rowRect.width - 4f, rowRect.height), SceneOperatorSettings.RowLoaded.Value);

            if (active)
                EditorGUI.DrawRect(new Rect(rowRect.x + 2f, rowRect.y, 3f, rowRect.height), SceneOperatorSettings.ActiveAccent.Value);

            var nameRect = new Rect(rowRect.x + SidePadding + 4f, rowRect.y, rowRect.width - SidePadding * 2f - 8f, rowRect.height);

            string name = item.Name;
            if (SceneOps.IsDirty(item.Path))
                name += " *";

            // The active scene is drawn bold, which is wider than the regular font: measure with the style
            // that actually draws, otherwise the name gets clipped.
            GUIStyle drawStyle = active
                ? (selected ? styles.NameSelectedBold : styles.NameBold)
                : (selected ? styles.NameSelected : styles.Name);
            float nameWidth = Mathf.Min(drawStyle.CalcSize(Temp(name)).x + 1f, nameRect.width);
            var textRect = new Rect(nameRect.x, nameRect.y, nameWidth, nameRect.height);
            drawStyle.Draw(textRect, Temp(name), false, false, false, false);

            if (SceneOperatorSettings.SwitcherShowPaths)
            {
                string folder = SceneCatalog.FolderOf(item.Path);
                float pathX = textRect.xMax + 10f;
                if (folder.Length > 0 && nameRect.xMax - pathX > 40f)
                {
                    GUIStyle pathStyle = selected ? styles.PathSelected : styles.Path;
                    pathStyle.Draw(new Rect(pathX, nameRect.y, nameRect.xMax - pathX, nameRect.height), Temp(folder), false, false, false, false);
                }
            }
        }

        // ---------------------------------------------------------------- input

        private void HandleKeyboard(Event e)
        {
            if (e.type != EventType.KeyDown)
                return;

            switch (e.keyCode)
            {
                case KeyCode.DownArrow:
                    Move(1);
                    e.Use();
                    return;
                case KeyCode.UpArrow:
                    Move(-1);
                    e.Use();
                    return;
                case KeyCode.PageDown:
                    Move(8);
                    e.Use();
                    return;
                case KeyCode.PageUp:
                    Move(-8);
                    e.Use();
                    return;
                case KeyCode.Return:
                case KeyCode.KeypadEnter:
                    if (_selected >= 0 && _selected < _rows.Count && _rows[_selected].IsScene)
                    {
                        OpenMode mode = e.control || e.command ? OpenMode.Additive : e.shift ? OpenMode.Play : OpenMode.Single;
                        Execute(_rows[_selected].Item, mode);
                    }
                    e.Use();
                    return;
                case KeyCode.Escape:
                    e.Use();
                    Close();
                    GUIUtility.ExitGUI();
                    return;
                case KeyCode.Tab:
                    e.Use();
                    return;
            }

            if (e.character == '\n' || e.character == '\r' || e.character == '\t')
                e.Use();
        }

        private void Move(int delta)
        {
            if (_rows.Count == 0)
                return;

            int step = delta > 0 ? 1 : -1;
            int remaining = Mathf.Abs(delta);
            int index = _selected;
            int last = index;

            while (remaining > 0)
            {
                index += step;
                if (index < 0 || index >= _rows.Count)
                    break;
                if (_rows[index].IsScene)
                {
                    last = index;
                    remaining--;
                }
            }

            if (last < 0 || last >= _rows.Count || !_rows[last].IsScene)
                last = delta > 0 ? LastSelectable() : FirstSelectable();

            if (last >= 0 && last != _selected)
            {
                _selected = last;
                _scrollToSelected = true;
                Repaint();
            }
        }

        private enum OpenMode { Single, Additive, Play }

        private void Execute(SceneItem item, OpenMode mode)
        {
            string path = item.Path;
            s_Current = null;
            Close();

            EditorApplication.delayCall += () =>
            {
                switch (mode)
                {
                    case OpenMode.Additive:
                        SceneOps.OpenAdditive(path);
                        break;
                    case OpenMode.Play:
                        SceneOps.OpenAndPlay(path);
                        break;
                    default:
                        SceneOps.OpenSingle(path);
                        break;
                }
            };

            GUIUtility.ExitGUI();
        }

        private static readonly GUIContent s_Temp = new GUIContent();

        private static GUIContent Temp(string text)
        {
            s_Temp.text = text;
            s_Temp.image = null;
            s_Temp.tooltip = string.Empty;
            return s_Temp;
        }

        // ---------------------------------------------------------------- rows and styles

        private readonly struct Row
        {
            private Row(SceneItem item, string headerText, RowKind kind)
            {
                Item = item;
                HeaderText = headerText;
                Kind = kind;
            }

            private enum RowKind { Scene, Header, Divider }

            public readonly SceneItem Item;
            public readonly string HeaderText;
            private readonly RowKind Kind;

            public bool IsScene => Kind == RowKind.Scene;
            public bool IsHeader => Kind == RowKind.Header;
            public bool IsDivider => Kind == RowKind.Divider;

            public float Height => Kind == RowKind.Scene ? RowHeight : Kind == RowKind.Header ? HeaderHeight : DividerHeight;

            public static Row Scene(SceneItem item) => new Row(item, null, RowKind.Scene);
            public static Row Header(string text) => new Row(default, text, RowKind.Header);
            public static Row Divider() => new Row(default, null, RowKind.Divider);
        }

        private sealed class Styles
        {
            private static Styles s_Instance;

            public static Styles Instance
            {
                get
                {
                    if (s_Instance == null || s_Instance._version != SceneOperatorSettings.AppearanceVersion || s_Instance._dark != SceneOperatorSettings.IsDarkTheme)
                        s_Instance = new Styles();
                    return s_Instance;
                }
            }

            private readonly int _version;
            private readonly bool _dark;

            public readonly GUIStyle Name;
            public readonly GUIStyle NameBold;
            public readonly GUIStyle NameSelected;
            public readonly GUIStyle NameSelectedBold;
            public readonly GUIStyle Path;
            public readonly GUIStyle PathSelected;
            public readonly GUIStyle Header;
            public readonly GUIStyle Footer;
            public readonly GUIStyle Muted;
            public readonly GUIStyle Placeholder;

            private Styles()
            {
                _version = SceneOperatorSettings.AppearanceVersion;
                _dark = SceneOperatorSettings.IsDarkTheme;

                GUIStyle baseLabel = null;
                try { baseLabel = UnityEditor.EditorStyles.label; } catch { baseLabel = null; }

                Name = baseLabel != null ? new GUIStyle(baseLabel) : new GUIStyle();
                Name.alignment = TextAnchor.MiddleLeft;
                Name.clipping = TextClipping.Clip;
                Name.wordWrap = false;
                Name.padding = new RectOffset(0, 0, 0, 0);
                Name.margin = new RectOffset(0, 0, 0, 0);
                SetColor(Name, SceneOperatorSettings.Text.Value);

                NameBold = new GUIStyle(Name) { fontStyle = FontStyle.Bold };
                SetColor(NameBold, SceneOperatorSettings.Text.Value);

                NameSelected = new GUIStyle(Name);
                SetColor(NameSelected, SceneOperatorSettings.SelectionText.Value);

                NameSelectedBold = new GUIStyle(NameBold);
                SetColor(NameSelectedBold, SceneOperatorSettings.SelectionText.Value);

                Path = new GUIStyle(Name) { fontSize = Name.fontSize > 0 ? Name.fontSize - 1 : 0, alignment = TextAnchor.MiddleLeft };
                SetColor(Path, SceneOperatorSettings.MutedText.Value);

                PathSelected = new GUIStyle(Path);
                Color selectionText = SceneOperatorSettings.SelectionText.Value;
                SetColor(PathSelected, new Color(selectionText.r, selectionText.g, selectionText.b, selectionText.a * 0.7f));

                Header = new GUIStyle(Path) { fontStyle = FontStyle.Bold };
                SetColor(Header, SceneOperatorSettings.MutedText.Value);

                Footer = new GUIStyle(Path);
                SetColor(Footer, SceneOperatorSettings.MutedText.Value);

                Muted = new GUIStyle(Name);
                SetColor(Muted, SceneOperatorSettings.MutedText.Value);

                Placeholder = new GUIStyle(Name) { padding = new RectOffset(18, 0, 0, 0) };
                SetColor(Placeholder, SceneOperatorSettings.MutedText.Value);
            }

            private static void SetColor(GUIStyle style, Color color)
            {
                style.normal.textColor = color;
                style.hover.textColor = color;
                style.active.textColor = color;
                style.focused.textColor = color;
            }
        }
    }
}
