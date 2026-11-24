using FreshCatch.Data;
using FreshCatch.Diagnostics;
using FreshCatch.Input;
using FreshCatch.Model;
using FreshCatch.View;
using UnityEngine;

namespace FreshCatch.Core
{
    /// <summary>
    /// Prototype game loop: loads a level, wires model to view and input, draws a quick IMGUI HUD.
    /// </summary>
    public class GameManager : MonoBehaviour
    {
        [SerializeField] private BoardView boardView;
        [SerializeField] private TapController tapController;
        [SerializeField] private Camera cam;
        [Tooltip("Optional. Empty = use the built-in levels.")]
        [SerializeField] private LevelDatabase levelDatabase;
        [SerializeField] private int startLevel;

        [Header("Playtest overrides (A/B)")]
        [SerializeField] private bool overrideFreshnessMode;
        [SerializeField] private FreshnessMode freshnessMode = FreshnessMode.MoveBased;
        [SerializeField] private bool overrideAutoServe;
        [SerializeField] private bool autoServe;

        [Header("Boosters per level")]
        [SerializeField] private int iceBuckets = 1;
        [SerializeField] private int cleaners = 1;

        [Header("Camera")]
        [SerializeField] private float boardWidth = 9.6f;
        [SerializeField] private Vector3 lookAt = new Vector3(0f, 0f, 1.6f);

        private BoardState _board;
        private int _levelIndex;
        private int _attempt;
        private float _levelTime;
        private int _iceLeft, _cleanLeft;
        private float _lastAspect;

        private int LevelCount => levelDatabase != null && levelDatabase.levels.Count > 0
            ? levelDatabase.levels.Count
            : BuiltInLevels.Count;

        private LevelDefinition GetLevel(int i)
        {
            if (levelDatabase != null && levelDatabase.levels.Count > 0)
                return levelDatabase.levels[i % levelDatabase.levels.Count].ToDefinition();
            return BuiltInLevels.Get(i);
        }

        private void Awake()
        {
            if (cam == null) cam = Camera.main;
            Application.targetFrameRate = 60;
        }

        private void OnEnable() { if (tapController != null) tapController.FishTapped += OnFishTapped; }
        private void OnDisable() { if (tapController != null) tapController.FishTapped -= OnFishTapped; }

        private void Start() => LoadLevel(startLevel);

        public void LoadLevel(int index)
        {
            _attempt = index == _levelIndex && _board != null ? _attempt + 1 : 1;
            _levelIndex = ((index % LevelCount) + LevelCount) % LevelCount;

            var def = GetLevel(_levelIndex);
            if (overrideFreshnessMode) def.FreshnessMode = freshnessMode;
            if (overrideAutoServe) def.AutoServe = autoServe;
            foreach (var e in def.Validate()) Debug.LogWarning($"Level {_levelIndex + 1}: {e}");

            if (_board != null) _board.GameEnded -= OnGameEnded;
            _board = new BoardState(def);
            _board.GameEnded += OnGameEnded;
            boardView.Bind(_board);
            _board.Start();
            boardView.SyncBoat();

            _levelTime = 0f;
            _iceLeft = iceBuckets;
            _cleanLeft = cleaners;
        }

        private void Update()
        {
            FitCamera();
            if (_board == null || _board.Result != GameResult.Playing) return;
            _levelTime += Time.deltaTime;
            _board.Tick(Time.deltaTime);
        }

        private void FitCamera()
        {
            if (cam == null || Mathf.Approximately(cam.aspect, _lastAspect)) return;
            _lastAspect = cam.aspect;
            cam.orthographic = true;
            cam.orthographicSize = Mathf.Max(7.5f, boardWidth / (2f * cam.aspect));
            cam.transform.rotation = Quaternion.Euler(52f, 0f, 0f);
            cam.transform.position = lookAt - cam.transform.forward * 30f;
        }

        // ------------------------------------------------------------------ input

        private void OnFishTapped(FishView view)
        {
            if (_board == null || _board.Result != GameResult.Playing) return;

            bool ok;
            var boatIndex = -1;
            if (_board.CurrentBoat != null)
                for (var i = 0; i < _board.CurrentBoat.Fish.Count; i++)
                    if (_board.CurrentBoat.Fish[i] == view.Fish) { boatIndex = i; break; }

            if (boatIndex >= 0) ok = _board.TapBoatFish(boatIndex);
            else
            {
                var slot = _board.Stall.IndexOf(view.Fish);
                ok = slot >= 0 && _board.TapStallFish(slot);
            }

            if (ok) boardView.SyncBoat();
            else view.Shake();
        }

        private void OnGameEnded(GameResult result, LoseReason reason)
        {
            PlaytestLogger.Log(_levelIndex, _board, _levelTime, iceBuckets - _iceLeft, cleaners - _cleanLeft, _attempt);
        }

        // ------------------------------------------------------------------ boosters

        private void UseIce()
        {
            if (_iceLeft <= 0 || _board.Result != GameResult.Playing) return;
            _iceLeft--;
            _board.RefreshAll(_board.Level.MaxFreshness);
        }

        private void UseCleaner()
        {
            if (_cleanLeft <= 0 || _board.Result != GameResult.Playing) return;
            for (var i = 0; i < _board.Stall.Capacity; i++)
            {
                var f = _board.Stall[i];
                if (f == null || !f.IsRotten) continue;
                boardView.RemoveFish(f);
                _board.ClearRotten(i);
                _cleanLeft--;
                return;
            }
        }

        // ------------------------------------------------------------------ HUD (greybox IMGUI)

        private GUIStyle _label, _big, _button, _center, _strip;

        private void OnGUI()
        {
            if (_board == null) return;

            // Design for a 1080x1920 portrait canvas and scale to the actual screen.
            var scale = Mathf.Min(Screen.width / 1080f, Screen.height / 1920f);
            var w = Screen.width / scale;
            var h = Screen.height / scale;
            GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1f));
            EnsureStyles();

            var lvl = _board.Level;
            var mode = lvl.FreshnessMode == FreshnessMode.MoveBased ? "Hamle" : "Süre";
            GUI.Box(new Rect(0, 0, w, 215), GUIContent.none, _strip);
            GUI.Label(new Rect(30, 30, w - 60, 60), $"Level {_levelIndex + 1}/{LevelCount}", _big);
            GUI.Label(new Rect(30, 100, w - 60, 50),
                $"Müşteri {_board.CustomersServed}/{_board.TotalCustomers}   Çürük {_board.RottenCount}/{lvl.MaxRottenFish}   Hamle {_board.MoveCount}",
                _label);
            GUI.Label(new Rect(30, 150, w - 60, 50),
                $"Tazelik: {mode}   AutoServe: {(lvl.AutoServe ? "Açık" : "Kapalı")}   Kalan tekne: {_board.BoatsRemaining}",
                _label);

            // Booster bar
            var by = h - 170;
            if (GUI.Button(new Rect(30, by, 300, 130), $"Buz Kovası\n({_iceLeft})", _button)) UseIce();
            if (GUI.Button(new Rect(350, by, 300, 130), $"Temizle\n({_cleanLeft})", _button)) UseCleaner();
            if (GUI.Button(new Rect(w - 330, by, 300, 130), "Yeniden", _button)) LoadLevel(_levelIndex);

            // Debug toggles for the A/B questions; restart the level to apply.
            var ty = by - 90;
            if (GUI.Button(new Rect(30, ty, 480, 70), $"Mod: {mode} (değiştir)", _button))
            {
                overrideFreshnessMode = true;
                freshnessMode = lvl.FreshnessMode == FreshnessMode.MoveBased ? FreshnessMode.TimeBased : FreshnessMode.MoveBased;
                LoadLevel(_levelIndex);
            }
            if (GUI.Button(new Rect(530, ty, 520, 70), $"AutoServe: {(lvl.AutoServe ? "Açık" : "Kapalı")} (değiştir)", _button))
            {
                overrideAutoServe = true;
                autoServe = !lvl.AutoServe;
                LoadLevel(_levelIndex);
            }

            if (_board.Result == GameResult.Playing) return;

            // End panel
            var panel = new Rect(90, h * 0.35f, w - 180, 420);
            GUI.Box(panel, GUIContent.none);
            var won = _board.Result == GameResult.Won;
            var title = won ? "Tezgâh boşaldı!" : _board.LoseReason == LoseReason.TooManyRotten ? "Balıklar çürüdü!" : "Tezgâhta yer kalmadı!";
            GUI.Label(new Rect(panel.x, panel.y + 40, panel.width, 80), title, _big);
            GUI.Label(new Rect(panel.x, panel.y + 130, panel.width, 50), $"{_levelTime:0} sn, {_board.MoveCount} hamle", _center);
            if (GUI.Button(new Rect(panel.x + 60, panel.y + 240, panel.width - 120, 130), won ? "Sonraki Level" : "Tekrar Dene", _button))
                LoadLevel(won ? _levelIndex + 1 : _levelIndex);
        }

        private void EnsureStyles()
        {
            if (_label != null) return;
            _label = new GUIStyle(GUI.skin.label) { fontSize = 36, alignment = TextAnchor.MiddleLeft };
            _label.normal.textColor = Color.white;
            _big = new GUIStyle(_label) { fontSize = 60, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
            _button = new GUIStyle(GUI.skin.button) { fontSize = 38 };
            _center = new GUIStyle(_label) { alignment = TextAnchor.MiddleCenter };

            var tex = new Texture2D(1, 1);
            tex.SetPixel(0, 0, new Color(0f, 0f, 0f, 0.45f));
            tex.Apply();
            _strip = new GUIStyle { normal = { background = tex } };
        }
    }
}
