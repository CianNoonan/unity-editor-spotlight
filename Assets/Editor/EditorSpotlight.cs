using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEngine;
using UnityEditor;

public class EditorSpotlight : EditorWindow, IHasCustomMenu
{
    const string PlaceholderInput = "Spotlight Search...";
    const string SearchHistoryKey = "SearchHistoryKey";

    List<string> _hits = new List<string>();
    string _input;
    int _selectedIndex;

    SearchHistory _history;

    void OnGUI()
    {
        HandleEvents();

        GUILayout.BeginHorizontal();
        GUILayout.Space(15);
        GUILayout.BeginVertical();
        GUILayout.Space(15);

        GUI.SetNextControlName("SpotlightInput");
        var prevInput = _input;
        _input = GUILayout.TextField(_input, Styles.InputFieldStyle, GUILayout.Height(60));
        EditorGUI.FocusTextInControl("SpotlightInput");

        if (_input != prevInput && !string.IsNullOrEmpty(_input))
            ProcessInput();

        if (_selectedIndex >= _hits.Count)
            _selectedIndex = _hits.Count - 1;
        else if (_selectedIndex <= 0)
            _selectedIndex = 0;

        if (string.IsNullOrEmpty(_input))
        {
            GUI.Label(GUILayoutUtility.GetLastRect(), PlaceholderInput, Styles.PlaceholderStyle);
            var pos = position;
            pos.height = BaseHeight;
            position = pos;
        }

        GUILayout.BeginHorizontal();
        GUILayout.Space(6);

        if (!string.IsNullOrEmpty(_input))
            VisualizeHits();

        GUILayout.Space(6);
        GUILayout.EndHorizontal();
        GUILayout.Space(15);
        GUILayout.EndVertical();
        GUILayout.Space(15);
        GUILayout.EndHorizontal();
    }

    void OnLostFocus()
    {
        Close();
    }

    const float BaseHeight = 90f;
    [MenuItem("Window/Spotlight %Space")]
    static void Init()
    {
        var window = CreateInstance<EditorSpotlight>();
        window.titleContent = new GUIContent("Spotlight");

        var pos = window.position;
        pos.height = BaseHeight;
        pos.width = 750f;
        window.position = pos;
        //pos.x = Screen.currentResolution.width / 2 - 750 / 2;
        //pos.y = Screen.currentResolution.height * 0.3f;
        window.CenterOnMainWin(-200f);

        window.ShowPopup();
        window.Reset();
    }

    void HandleEvents()
    {
        var current = Event.current;

        if (current.type == EventType.KeyDown)
        {
            switch (current.keyCode)
            {
                case KeyCode.UpArrow:
                    current.Use();
                    _selectedIndex--;
                    break;

                case KeyCode.DownArrow:
                    current.Use();
                    _selectedIndex++;
                    break;

                case KeyCode.Return:
                    current.Use();
                    OpenSelectedAssetAndClose();
                    break;

                case KeyCode.Escape:
                    Close();
                    break;

                case KeyCode.Tab:
                    FocusSelection();
                    break;
            }
        }
    }

    void Reset()
    {
        _input = "";
        _hits.Clear();

        var json = EditorPrefs.GetString(SearchHistoryKey, JsonUtility.ToJson(new SearchHistory()));
        _history = JsonUtility.FromJson<SearchHistory>(json);

        Focus();
    }

    void ProcessInput()
    {
        _input = _input.ToLower();
        var assetHits = AssetDatabase.FindAssets(_input) ?? new string[0];
        _hits = assetHits.ToList();

        _hits.Sort((x, y) =>
        {
            _history.Clicks.TryGetValue(x, out var xScore);
            _history.Clicks.TryGetValue(y, out var yScore);

            if (xScore != 0 && yScore != 0)
            {
                var xName = Path.GetFileName(AssetDatabase.GUIDToAssetPath(x)).ToLower();
                var yName = Path.GetFileName(AssetDatabase.GUIDToAssetPath(y)).ToLower();

                if (xName.StartsWith(_input) && !yName.StartsWith(_input))
                    return -1;
                if (!xName.StartsWith(_input) && yName.StartsWith(_input))
                    return 1;
            }

            return yScore - xScore;
        });

        _hits = _hits.Take(10).ToList();
    }

    void VisualizeHits()
    {
        var current = Event.current;

        var windowRect = position;
        windowRect.height = BaseHeight;

        GUILayout.BeginVertical();
        GUILayout.Space(5);

        for (var i = 0; i < _hits.Count; i++)
        {
            var style = i % 2 == 0 ? Styles.EntryOdd : Styles.EntryEven;

            GUILayout.BeginHorizontal(GUILayout.Height(EditorGUIUtility.singleLineHeight * 2),
                                        GUILayout.ExpandWidth(true));
            var elementRect = GUILayoutUtility.GetRect(0, 0, GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));
            GUILayout.EndHorizontal();

            windowRect.height += EditorGUIUtility.singleLineHeight * 2;

            if (current.type == EventType.Repaint)
            {
                style.Draw(elementRect, false, false, i == _selectedIndex, false);
                var assetPath = AssetDatabase.GUIDToAssetPath(_hits[i]);
                var icon = AssetDatabase.GetCachedIcon(assetPath);

                var iconRect = elementRect;
                iconRect.x = 30;
                iconRect.width = 25;
                GUI.DrawTexture(iconRect, icon, ScaleMode.ScaleToFit);

                var assetName = Path.GetFileName(assetPath);
                var coloredAssetName = new StringBuilder();

                var start = assetName.ToLower().IndexOf(_input);
                var end = start + _input.Length;

                // Sometimes the AssetDatabase finds assets without the search input in it.
                if (start == -1)
                {
                    coloredAssetName.Append(string.Format("<color=#{0}>{1}</color>", Styles.NormalColor, assetName));
                }
                else
                {
                    if (0 != start)
                        coloredAssetName.Append(string.Format("<color=#{0}>{1}</color>", Styles.NormalColor, assetName.Substring(0, start)));

                    coloredAssetName.Append(string.Format("<color=#{0}><b>{1}</b></color>", Styles.HighlightColor, assetName.Substring(start, end - start)));

                    if (end != assetName.Length - end)
                        coloredAssetName.Append(string.Format("<color=#{0}>{1}</color>", Styles.NormalColor, assetName.Substring(end, assetName.Length - end)));
                }

                var labelRect = elementRect;
                labelRect.x = 60;
                GUI.Label(labelRect, coloredAssetName.ToString(), Styles.ResultLabelStyle);
            }

            if (current.type == EventType.MouseDown && elementRect.Contains(current.mousePosition))
            {
                _selectedIndex = i;

                if (current.clickCount == 2)
                {
                    OpenSelectedAssetAndClose();
                }
                else
                {
                    FocusSelection();
                }

                Repaint();
            }
        }

        windowRect.height += 5;
        position = windowRect;

        GUILayout.EndVertical();
    }

    void OpenSelectedAssetAndClose()
    {
        var asset = GetSelectedAsset();
        var assetPath = AssetDatabase.GetAssetPath(asset);
        var importerName = AssetImporter.GetAtPath(assetPath).ToString();

        switch (importerName)
        {
            case " (UnityEngine.FBXImporter)":
            case " (UnityEngine.TextureImporter)":
                FocusSelection();
                Close();
                return;
        }

        if (AssetDatabase.OpenAsset(asset))
        {
            var guid = _hits[_selectedIndex];
            if (!_history.Clicks.ContainsKey(guid))
                _history.Clicks[guid] = 0;

            _history.Clicks[guid]++;
            EditorPrefs.SetString(SearchHistoryKey, JsonUtility.ToJson(_history));
        }
        Close();
    }

    UnityEngine.Object GetSelectedAsset()
    {
        string assetPath = null;

        if (_selectedIndex >= 0 && _selectedIndex < _hits.Count)
            assetPath = AssetDatabase.GUIDToAssetPath(_hits[_selectedIndex]);

        return AssetDatabase.LoadMainAssetAtPath(assetPath);
    }

    public void AddItemsToMenu(GenericMenu menu)
    {
        menu.AddItem(new GUIContent("Reset history"), false, () =>
        {
            EditorPrefs.SetString(SearchHistoryKey, JsonUtility.ToJson(new SearchHistory()));
            Reset();
        });

        menu.AddItem(new GUIContent("Output history"), false, () =>
        {
            var json = EditorPrefs.GetString(SearchHistoryKey, JsonUtility.ToJson(new SearchHistory()));
            Debug.Log(json);
        });
    }

    void FocusSelection()
    {
        var selectedAsset = GetSelectedAsset();

        if (selectedAsset != null)
        {
            EditorUtility.FocusProjectWindow();
            Selection.activeObject = GetSelectedAsset();
            EditorGUIUtility.PingObject(Selection.activeGameObject);
        }
    }

    static class Styles
    {
        public static readonly GUIStyle InputFieldStyle;
        public static readonly GUIStyle PlaceholderStyle;
        public static readonly GUIStyle ResultLabelStyle;
        public static readonly GUIStyle EntryEven;
        public static readonly GUIStyle EntryOdd;

        public static string HighlightColor;
        static readonly string _proSkinHighlightColor = "eeeeee";
        static readonly string _personalSkinHighlightColor = "eeeeee";

        public static string NormalColor;
        static readonly string _personalSkinNormalColor = "222222";
        static readonly string _proSkinNormalColor = "cccccc";

        static Styles()
        {
            InputFieldStyle = new GUIStyle(EditorStyles.textField)
            {
                contentOffset = new Vector2(10, 10),
                fontSize = 32,
                focused = new GUIStyleState()
            };

            PlaceholderStyle = new GUIStyle(InputFieldStyle)
            {
                normal = { textColor = EditorGUIUtility.isProSkin ? new Color(1, 1, 1, .2f) : new Color(.2f, .2f, .2f, .4f) }
            };

            ResultLabelStyle = new GUIStyle(EditorStyles.largeLabel)
            {
                alignment = TextAnchor.MiddleLeft,
                richText = true
            };

            HighlightColor = EditorGUIUtility.isProSkin ? _proSkinHighlightColor : _personalSkinHighlightColor;
            NormalColor = EditorGUIUtility.isProSkin ? _proSkinNormalColor : _personalSkinNormalColor;

            EntryOdd = new GUIStyle("CN EntryBackOdd");
            EntryEven = new GUIStyle("CN EntryBackEven");
        }
    }

    [Serializable]
    class SearchHistory : ISerializationCallbackReceiver
    {
        public readonly Dictionary<string, int> Clicks = new Dictionary<string, int>();

        [SerializeField]
        List<string> _clickKeys = new List<string>();
        [SerializeField]
        List<int> _clickValues = new List<int>();

        public void OnBeforeSerialize()
        {
            _clickKeys.Clear();
            _clickValues.Clear();

            foreach (var pair in Clicks)
            {
                _clickKeys.Add(pair.Key);
                _clickValues.Add(pair.Value);
            }
        }

        public void OnAfterDeserialize()
        {
            Clicks.Clear();
            for (var i = 0; i < _clickKeys.Count; i++)
                Clicks.Add(_clickKeys[i], _clickValues[i]);
        }
    }
}
