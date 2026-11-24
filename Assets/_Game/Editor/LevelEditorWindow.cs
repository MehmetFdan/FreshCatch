using System;
using System.Collections.Generic;
using System.IO;
using FreshCatch.Core;
using FreshCatch.Data;
using FreshCatch.Model;
using FreshCatch.View;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace FreshCatch.EditorTools
{
    /// <summary>
    /// FreshCatch → Level Editor. Manages the LevelDatabase, edits boats/customers with colored chips,
    /// shows supply vs demand, runs the solver for a difficulty estimate, generates levels and
    /// plays a level in the prototype scene.
    ///
    /// Chips: left-click cycles the fish type, right-click removes it.
    /// </summary>
    public class LevelEditorWindow : EditorWindow
    {
        private const string LevelsFolder = "Assets/_Game/Levels";
        private const string DatabasePath = LevelsFolder + "/LevelDatabase.asset";

        private LevelDatabase _db;
        private int _selected = -1;
        private Vector2 _listScroll, _editScroll;
        private LevelAnalysis _analysis;
        private LevelData _analysisFor;
        private string _analysisHash;
        private GeneratorSettings _gen = new GeneratorSettings();
        private bool _genFoldout = true;
        private FishType _brush = FishType.Hamsi;

        private static readonly FishType[] AllTypes = (FishType[])Enum.GetValues(typeof(FishType));

        [MenuItem("FreshCatch/Level Editor")]
        public static void Open() => GetWindow<LevelEditorWindow>("Level Editor").minSize = new Vector2(760, 480);

        private void OnEnable()
        {
            _db = AssetDatabase.LoadAssetAtPath<LevelDatabase>(DatabasePath);
            Undo.undoRedoPerformed += Repaint;
        }

        private void OnDisable() => Undo.undoRedoPerformed -= Repaint;

        private LevelData Selected =>
            _db != null && _selected >= 0 && _selected < _db.levels.Count ? _db.levels[_selected] : null;

        // ================================================================== layout

        private void OnGUI()
        {
            if (_db == null)
            {
                DrawNoDatabase();
                return;
            }

            EditorGUILayout.BeginHorizontal();
            DrawLevelList();
            DrawEditor();
            EditorGUILayout.EndHorizontal();
        }

        private void DrawNoDatabase()
        {
            EditorGUILayout.Space(20);
            EditorGUILayout.HelpBox($"Level veritabanı yok. {DatabasePath} oluşturulacak.", MessageType.Info);
            if (GUILayout.Button("Veritabanı oluştur", GUILayout.Height(32))) CreateDatabase(false);
            if (GUILayout.Button("Oluştur ve hazır 6 level'ı içe aktar", GUILayout.Height(32))) CreateDatabase(true);
        }

        // ================================================================== list

        private void DrawLevelList()
        {
            EditorGUILayout.BeginVertical(GUILayout.Width(210));
            EditorGUILayout.LabelField($"Level'lar ({_db.levels.Count})", EditorStyles.boldLabel);

            _listScroll = EditorGUILayout.BeginScrollView(_listScroll, GUILayout.ExpandHeight(true));
            for (var i = 0; i < _db.levels.Count; i++)
            {
                var l = _db.levels[i];
                var label = l == null ? $"{i + 1}. (eksik)" : $"{i + 1}. {l.name}";
                var style = i == _selected ? EditorStyles.toolbarButton : EditorStyles.miniButton;
                var prev = GUI.backgroundColor;
                if (i == _selected) GUI.backgroundColor = new Color(0.55f, 0.8f, 1f);
                if (GUILayout.Button(label, style)) Select(i);
                GUI.backgroundColor = prev;
            }
            EditorGUILayout.EndScrollView();

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("+ Yeni")) AddLevel(null);
            GUI.enabled = Selected != null;
            if (GUILayout.Button("Kopyala")) AddLevel(Selected.ToDefinition());
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("▲")) Move(-1);
            if (GUILayout.Button("▼")) Move(1);
            if (GUILayout.Button("Sil")) DeleteSelected();
            GUI.enabled = true;
            EditorGUILayout.EndHorizontal();

            if (GUILayout.Button("Hazır level'ları içe aktar")) ImportBuiltIns();
            EditorGUILayout.EndVertical();
        }

        private void Select(int i)
        {
            _selected = i;
            _analysis = null;
            GUI.FocusControl(null);
            if (Selected != null) EditorGUIUtility.PingObject(Selected);
        }

        // ================================================================== editor

        private void DrawEditor()
        {
            EditorGUILayout.BeginVertical();
            var level = Selected;
            if (level == null)
            {
                EditorGUILayout.HelpBox("Soldan bir level seç ya da yeni bir level ekle.", MessageType.None);
                EditorGUILayout.EndVertical();
                return;
            }

            _editScroll = EditorGUILayout.BeginScrollView(_editScroll);

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField($"Level {_selected + 1}: {level.name}", EditorStyles.largeLabel);
            if (GUILayout.Button("▶ Oyna", GUILayout.Width(90), GUILayout.Height(22))) PlayLevel(_selected);
            EditorGUILayout.EndHorizontal();

            DrawSettings(level);
            EditorGUILayout.Space(6);
            DrawBrush();
            DrawGroups(level, level.boats, "Tekneler (sırayla yanaşır)", "Tekne");
            DrawGroups(level, level.customers, "Müşteriler (sırayla gelir)", "Müşteri");
            EditorGUILayout.Space(6);
            DrawBalance(level);
            DrawAnalysis(level);
            DrawGenerator(level);

            EditorGUILayout.EndScrollView();
            EditorGUILayout.EndVertical();
        }

        private void DrawSettings(LevelData level)
        {
            EditorGUI.BeginChangeCheck();
            EditorGUILayout.LabelField("Ayarlar", EditorStyles.boldLabel);
            EditorGUILayout.BeginHorizontal();
            var slots = EditorGUILayout.IntSlider("Tezgâh slotu", level.stallSlots, 1, 10);
            var visible = EditorGUILayout.IntSlider("Görünen müşteri", level.visibleCustomers, 1, 4);
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.BeginHorizontal();
            var rotten = EditorGUILayout.IntSlider("İzin verilen çürük", level.maxRottenFish, 0, 5);
            var auto = EditorGUILayout.Toggle("AutoServe", level.autoServe);
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.BeginHorizontal();
            var mode = (FreshnessMode)EditorGUILayout.EnumPopup("Tazelik modu", level.freshnessMode);
            float seconds = level.freshnessSeconds;
            int moves = level.freshnessMoves;
            if (mode == FreshnessMode.TimeBased) seconds = EditorGUILayout.Slider("Saniye / balık", seconds, 3f, 60f);
            else moves = EditorGUILayout.IntSlider("Hamle / balık", moves, 2, 30);
            EditorGUILayout.EndHorizontal();

            if (!EditorGUI.EndChangeCheck()) return;
            Undo.RecordObject(level, "Edit level settings");
            level.stallSlots = slots;
            level.visibleCustomers = visible;
            level.maxRottenFish = rotten;
            level.autoServe = auto;
            level.freshnessMode = mode;
            level.freshnessSeconds = seconds;
            level.freshnessMoves = moves;
            MarkDirty(level);
        }

        private void DrawBrush()
        {
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Fırça (+ ile eklenen tür):", GUILayout.Width(170));
            foreach (var t in AllTypes)
            {
                var prev = GUI.backgroundColor;
                GUI.backgroundColor = Palette.Of(t);
                var label = t == _brush ? $"[{t}]" : t.ToString();
                if (GUILayout.Button(label, GUILayout.Width(78))) _brush = t;
                GUI.backgroundColor = prev;
            }
            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.LabelField("Balığa sol tık: türü değiştir · sağ tık veya Shift+tık: sil", EditorStyles.miniLabel);
        }

        private void DrawGroups(LevelData level, List<LevelData.FishGroup> groups, string title, string itemName)
        {
            EditorGUILayout.Space(4);
            EditorGUILayout.LabelField($"{title} ({groups.Count})", EditorStyles.boldLabel);

            for (var g = 0; g < groups.Count; g++)
            {
                var fish = groups[g].fish;
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField($"{itemName} {g + 1}", GUILayout.Width(80));

                for (var i = 0; i < fish.Count; i++)
                {
                    var prev = GUI.backgroundColor;
                    GUI.backgroundColor = Palette.Of(fish[i]);
                    if (GUILayout.Button(new GUIContent(Short(fish[i]), fish[i].ToString()), GUILayout.Width(34), GUILayout.Height(22)))
                    {
                        Undo.RecordObject(level, "Edit fish");
                        if (Event.current.button == 1 || Event.current.shift) fish.RemoveAt(i);
                        else fish[i] = AllTypes[((int)fish[i] + 1) % AllTypes.Length];
                        MarkDirty(level);
                        GUI.backgroundColor = prev;
                        GUIUtility.ExitGUI();
                    }
                    GUI.backgroundColor = prev;
                }

                if (GUILayout.Button("+", GUILayout.Width(24), GUILayout.Height(22)))
                {
                    Undo.RecordObject(level, "Add fish");
                    fish.Add(_brush);
                    MarkDirty(level);
                }

                GUILayout.FlexibleSpace();
                if (GUILayout.Button("▲", EditorStyles.miniButtonLeft, GUILayout.Width(24)) && g > 0)
                {
                    Undo.RecordObject(level, "Move group");
                    (groups[g - 1], groups[g]) = (groups[g], groups[g - 1]);
                    MarkDirty(level);
                }
                if (GUILayout.Button("▼", EditorStyles.miniButtonMid, GUILayout.Width(24)) && g < groups.Count - 1)
                {
                    Undo.RecordObject(level, "Move group");
                    (groups[g + 1], groups[g]) = (groups[g], groups[g + 1]);
                    MarkDirty(level);
                }
                if (GUILayout.Button("×", EditorStyles.miniButtonRight, GUILayout.Width(24)))
                {
                    Undo.RecordObject(level, "Remove group");
                    groups.RemoveAt(g);
                    MarkDirty(level);
                    GUIUtility.ExitGUI();
                }
                EditorGUILayout.EndHorizontal();
            }

            if (GUILayout.Button($"+ {itemName} ekle", GUILayout.Width(140)))
            {
                Undo.RecordObject(level, "Add group");
                groups.Add(new LevelData.FishGroup { fish = new List<FishType> { _brush } });
                MarkDirty(level);
            }
        }

        private static void DrawBalance(LevelData level)
        {
            EditorGUILayout.LabelField("Arz / Talep", EditorStyles.boldLabel);
            var supply = new Dictionary<FishType, int>();
            var demand = new Dictionary<FishType, int>();
            foreach (var b in level.boats) foreach (var t in b.fish) supply[t] = supply.TryGetValue(t, out var n) ? n + 1 : 1;
            foreach (var c in level.customers) foreach (var t in c.fish) demand[t] = demand.TryGetValue(t, out var n) ? n + 1 : 1;

            EditorGUILayout.BeginHorizontal();
            foreach (var t in AllTypes)
            {
                supply.TryGetValue(t, out var s);
                demand.TryGetValue(t, out var d);
                if (s == 0 && d == 0) continue;
                var prev = GUI.color;
                GUI.color = s < d ? new Color(1f, 0.45f, 0.45f) : s > d ? new Color(1f, 0.85f, 0.4f) : Color.white;
                EditorGUILayout.LabelField($"{t}: {s}/{d}", EditorStyles.helpBox, GUILayout.Width(110));
                GUI.color = prev;
            }
            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.LabelField("tekne/sipariş · kırmızı: eksik balık · sarı: fazla balık (çürüyecek)", EditorStyles.miniLabel);

            foreach (var e in level.ToDefinition().Validate())
                EditorGUILayout.HelpBox(e, MessageType.Error);
        }

        // ================================================================== solver

        private void DrawAnalysis(LevelData level)
        {
            EditorGUILayout.Space(6);
            EditorGUILayout.LabelField("Çözücü", EditorStyles.boldLabel);
            if (GUILayout.Button("Analiz et (1 dikkatli + 200 hatalı oyun)", GUILayout.Height(26)))
            {
                _analysis = LevelSolver.Analyze(level.ToDefinition());
                _analysisFor = level;
                _analysisHash = JsonUtility.ToJson(level);
            }

            if (_analysis == null || _analysisFor != level) return;
            if (_analysisHash != JsonUtility.ToJson(level))
                EditorGUILayout.HelpBox("Level analizden sonra değişti, sonuç eski.", MessageType.Warning);

            var g = _analysis.Greedy;
            var type = !g.Won ? MessageType.Error : _analysis.WinRate < 0.25f ? MessageType.Warning : MessageType.Info;
            var losses = new List<string>();
            foreach (var kv in _analysis.Losses) losses.Add($"{Reason(kv.Key)}: {kv.Value}");
            EditorGUILayout.HelpBox(
                $"Zorluk: {_analysis.Difficulty}\n" +
                $"Dikkatli oyuncu: {(g.Won ? "kazandı" : "kaybetti (" + Reason(g.Reason) + ")")}, {g.Moves} hamle, {g.Rotten} çürük, {g.CustomersServed} müşteri\n" +
                $"Hatalı oyuncu kazanma oranı: %{_analysis.WinRate * 100f:0} ({_analysis.Wins}/{_analysis.Playouts}), kazanınca ort. {_analysis.AvgMovesOnWin:0} hamle\n" +
                (losses.Count > 0 ? "Kayıp nedenleri: " + string.Join(", ", losses) : ""),
                type);
        }

        private static string Reason(LoseReason r) =>
            r == LoseReason.TooManyRotten ? "çürüme" : r == LoseReason.NoMovesLeft ? "tezgâh doldu" : "-";

        // ================================================================== generator

        private void DrawGenerator(LevelData level)
        {
            EditorGUILayout.Space(6);
            _genFoldout = EditorGUILayout.Foldout(_genFoldout, "Üretici (tekne ve müşterileri yeniden yazar)", true);
            if (!_genFoldout) return;

            EditorGUI.indentLevel++;
            _gen.FishTypes = EditorGUILayout.IntSlider("Balık türü", _gen.FishTypes, 1, LevelGenerator.TypeCount);
            _gen.Customers = EditorGUILayout.IntSlider("Müşteri sayısı", _gen.Customers, 1, 20);
            EditorGUILayout.BeginHorizontal();
            _gen.MinOrder = EditorGUILayout.IntSlider("Sipariş min", _gen.MinOrder, 1, 5);
            _gen.MaxOrder = EditorGUILayout.IntSlider("max", Mathf.Max(_gen.MinOrder, _gen.MaxOrder), _gen.MinOrder, 6);
            EditorGUILayout.EndHorizontal();
            _gen.BoatSize = EditorGUILayout.IntSlider("Tekne kapasitesi", _gen.BoatSize, 1, 8);
            _gen.ExtraFish = EditorGUILayout.IntSlider("Fazla balık", _gen.ExtraFish, 0, 6);
            _gen.Scramble = EditorGUILayout.IntSlider("Karışıklık", _gen.Scramble, 0, 12);
            _gen.Seed = EditorGUILayout.IntField("Seed", _gen.Seed);
            EditorGUI.indentLevel--;

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Üret"))
            {
                var def = level.ToDefinition();
                LevelGenerator.Generate(_gen, def);
                ApplyGenerated(level, def);
            }
            if (GUILayout.Button("Çözülebilir üret (50 deneme)"))
            {
                var def = level.ToDefinition();
                var seed = LevelGenerator.GenerateSolvable(_gen, def);
                if (seed < 0) ShowNotification(new GUIContent("50 seed'de çözülebilir level bulunamadı"));
                else ApplyGenerated(level, def);
            }
            if (GUILayout.Button("Seed +1", GUILayout.Width(70))) _gen.Seed++;
            EditorGUILayout.EndHorizontal();
        }

        private void ApplyGenerated(LevelData level, LevelDefinition def)
        {
            Undo.RecordObject(level, "Generate level");
            level.CopyFrom(def);
            MarkDirty(level);
            _analysis = LevelSolver.Analyze(level.ToDefinition());
            _analysisFor = level;
            _analysisHash = JsonUtility.ToJson(level);
        }

        // ================================================================== asset ops

        private void CreateDatabase(bool importBuiltIns)
        {
            Directory.CreateDirectory(LevelsFolder);
            _db = CreateInstance<LevelDatabase>();
            AssetDatabase.CreateAsset(_db, DatabasePath);
            AssetDatabase.SaveAssets();
            if (importBuiltIns) ImportBuiltIns();
        }

        private void ImportBuiltIns()
        {
            foreach (var def in BuiltInLevels.All()) AddLevel(def, select: false);
            Select(_db.levels.Count - 1);
        }

        private void AddLevel(LevelDefinition from, bool select = true)
        {
            var level = CreateInstance<LevelData>();
            if (from != null) level.CopyFrom(from);
            else
            {
                level.boats.Add(new LevelData.FishGroup { fish = new List<FishType> { FishType.Hamsi, FishType.Hamsi } });
                level.customers.Add(new LevelData.FishGroup { fish = new List<FishType> { FishType.Hamsi, FishType.Hamsi } });
            }

            var path = AssetDatabase.GenerateUniqueAssetPath($"{LevelsFolder}/Level_{_db.levels.Count + 1:000}.asset");
            AssetDatabase.CreateAsset(level, path);

            Undo.RecordObject(_db, "Add level");
            var insertAt = select && _selected >= 0 && from != null ? _selected + 1 : _db.levels.Count;
            _db.levels.Insert(insertAt, level);
            MarkDirty(_db);
            AssetDatabase.SaveAssets();
            if (select) Select(insertAt);
        }

        private void Move(int dir)
        {
            var j = _selected + dir;
            if (Selected == null || j < 0 || j >= _db.levels.Count) return;
            Undo.RecordObject(_db, "Reorder levels");
            (_db.levels[j], _db.levels[_selected]) = (_db.levels[_selected], _db.levels[j]);
            _selected = j;
            MarkDirty(_db);
        }

        private void DeleteSelected()
        {
            var level = Selected;
            if (level == null) return;
            if (!EditorUtility.DisplayDialog("Level sil",
                    $"{level.name} silinsin mi? Asset dosyası çöp kutusuna taşınır.", "Sil", "Vazgeç")) return;

            Undo.RecordObject(_db, "Delete level");
            _db.levels.RemoveAt(_selected);
            MarkDirty(_db);
            AssetDatabase.MoveAssetToTrash(AssetDatabase.GetAssetPath(level));
            AssetDatabase.SaveAssets();
            Select(Mathf.Min(_selected, _db.levels.Count - 1));
        }

        private static void MarkDirty(UnityEngine.Object o) => EditorUtility.SetDirty(o);

        private static string Short(FishType t) => t.ToString().Substring(0, 2);

        // ================================================================== play

        private void PlayLevel(int index)
        {
            if (EditorApplication.isPlaying) EditorApplication.isPlaying = false;
            AssetDatabase.SaveAssets();
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

            if (EditorSceneManager.GetActiveScene().path != PrototypeSceneBuilder.ScenePath)
            {
                if (!File.Exists(PrototypeSceneBuilder.ScenePath)) PrototypeSceneBuilder.Build();
                else EditorSceneManager.OpenScene(PrototypeSceneBuilder.ScenePath);
            }

            var gm = FindFirstObjectByType<GameManager>();
            if (gm == null)
            {
                ShowNotification(new GUIContent("Sahnede GameManager yok"));
                return;
            }

            var so = new SerializedObject(gm);
            so.FindProperty("levelDatabase").objectReferenceValue = _db;
            so.FindProperty("startLevel").intValue = index;
            so.ApplyModifiedProperties();
            EditorSceneManager.SaveScene(gm.gameObject.scene);
            EditorApplication.EnterPlaymode();
        }
    }
}
