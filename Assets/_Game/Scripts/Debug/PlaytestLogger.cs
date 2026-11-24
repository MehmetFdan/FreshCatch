using System;
using System.Globalization;
using System.IO;
using FreshCatch.Model;
using UnityEngine;

namespace FreshCatch.Diagnostics
{
    /// <summary>Appends one CSV row per finished level to persistentDataPath/playtest.csv.</summary>
    public static class PlaytestLogger
    {
        public static string FilePath => Path.Combine(Application.persistentDataPath, "playtest.csv");

        private const string Header =
            "timestamp,level,result,loseReason,seconds,moves,rotten,customersServed,totalCustomers,freshnessMode,autoServe,iceUsed,cleanUsed,attempt";

        public static void Log(int levelIndex, BoardState board, float seconds, int iceUsed, int cleanUsed, int attempt)
        {
            try
            {
                var exists = File.Exists(FilePath);
                using (var w = new StreamWriter(FilePath, append: true))
                {
                    if (!exists) w.WriteLine(Header);
                    w.WriteLine(string.Join(",",
                        DateTime.UtcNow.ToString("o"),
                        levelIndex + 1,
                        board.Result,
                        board.LoseReason,
                        seconds.ToString("0.0", CultureInfo.InvariantCulture),
                        board.MoveCount,
                        board.RottenCount,
                        board.CustomersServed,
                        board.TotalCustomers,
                        board.Level.FreshnessMode,
                        board.Level.AutoServe,
                        iceUsed,
                        cleanUsed,
                        attempt));
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning($"Playtest log failed: {e.Message}");
            }
        }
    }
}
