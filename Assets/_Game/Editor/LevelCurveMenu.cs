using System.IO;
using System.Text;
using FreshCatch.Data;
using FreshCatch.Model;
using UnityEditor;
using UnityEngine;

namespace FreshCatch.EditorTools
{
    /// <summary>FreshCatch → Generate Level Curve: replaces the level database with a tuned sequence.</summary>
    public static class LevelCurveMenu
    {
        private const string LevelsFolder = "Assets/_Game/Levels";
        private const string DatabasePath = LevelsFolder + "/LevelDatabase.asset";
        private const string ReportPath = LevelsFolder + "/LevelCurve.md";

        [MenuItem("FreshCatch/Generate Level Curve (20)")]
        private static void Menu()
        {
            if (!EditorUtility.DisplayDialog("Level eğrisi üret",
                    "Veritabanındaki tüm level'lar 20 yeni level ile değiştirilecek (eski asset'ler çöp kutusuna gider). Devam?",
                    "Üret", "Vazgeç")) return;
            Debug.Log(Generate(20));
        }

        /// <summary>Non-interactive entry point; returns the report text.</summary>
        public static string Generate(int total)
        {
            System.Collections.Generic.List<CurveLevel> curve;
            try
            {
                curve = LevelCurve.Build(total, progress: (n, all) =>
                    EditorUtility.DisplayProgressBar("Level eğrisi", $"Level {n}/{all} aranıyor…", (n - 1f) / all));
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }

            Directory.CreateDirectory(LevelsFolder);
            var db = AssetDatabase.LoadAssetAtPath<LevelDatabase>(DatabasePath);
            if (db == null)
            {
                db = ScriptableObject.CreateInstance<LevelDatabase>();
                AssetDatabase.CreateAsset(db, DatabasePath);
            }

            foreach (var old in db.levels)
                if (old != null) AssetDatabase.MoveAssetToTrash(AssetDatabase.GetAssetPath(old));
            db.levels.Clear();

            foreach (var c in curve)
            {
                var asset = ScriptableObject.CreateInstance<LevelData>();
                asset.CopyFrom(c.Level);
                AssetDatabase.CreateAsset(asset, $"{LevelsFolder}/Level_{c.Number:000}.asset");
                db.levels.Add(asset);
            }
            EditorUtility.SetDirty(db);
            AssetDatabase.SaveAssets();

            var report = Report(curve);
            File.WriteAllText(ReportPath, report);
            AssetDatabase.ImportAsset(ReportPath);
            return report;
        }

        private static string Report(System.Collections.Generic.List<CurveLevel> curve)
        {
            var sb = new StringBuilder();
            sb.AppendLine("# FreshCatch level eğrisi");
            sb.AppendLine();
            sb.AppendLine("Otomatik üretildi (FreshCatch → Generate Level Curve). Kazanma oranı: %30 hatalı hamle yapan oyuncunun 200 denemesi.");
            sb.AppendLine();
            sb.AppendLine("| # | Kaynak | Hedef | Kazanma | Zorluk | Tür | Müşteri | Balık | Slot | Görünen | Tazelik | Çürük | Seed |");
            sb.AppendLine("|---|---|---|---|---|---|---|---|---|---|---|---|---|");
            foreach (var c in curve)
            {
                var l = c.Level;
                var types = new System.Collections.Generic.HashSet<FishType>();
                var fish = 0;
                foreach (var b in l.Boats) { fish += b.Count; foreach (var t in b) types.Add(t); }
                sb.AppendLine($"| {c.Number} | {(c.Handmade ? "el yapımı" : "üretildi")} | %{c.TargetWinRate * 100:0} | %{c.Analysis.WinRate * 100:0} | {c.Analysis.Difficulty} | " +
                              $"{types.Count} | {l.Customers.Count} | {fish} | {l.StallSlots} | {l.VisibleCustomers} | {l.FreshnessMoves} | {l.MaxRottenFish} | {(c.Handmade ? "-" : c.Seed.ToString())} |");
            }
            return sb.ToString();
        }
    }
}
