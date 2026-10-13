using System;
using UnityEngine;

namespace DemocracySim.Engine.Data
{
    /// <summary>
    /// FAZ 5: Global oyun istatistikleri. Oyunlar arası kalıcı.
    /// </summary>
    public static class GlobalStatsTracker
    {
        private const string KEY_GAMES_PLAYED = "Stats_GamesPlayed";
        private const string KEY_TOTAL_TURNS = "Stats_TotalTurns";
        private const string KEY_MAX_GDP = "Stats_MaxGDP";
        private const string KEY_MAX_TURNS_SINGLE = "Stats_MaxTurnsSingle";
        private const string KEY_BEST_LEGITIMACY = "Stats_BestLegitimacy";
        private const string KEY_ELECTIONS_WON = "Stats_ElectionsWon";

        // ============================================================
        // ARTIR / SET
        // ============================================================

        public static void IncrementGamesPlayed()
            => PlayerPrefs.SetInt(KEY_GAMES_PLAYED, GetGamesPlayed() + 1);

        public static void AddTurns(int turns)
            => PlayerPrefs.SetInt(KEY_TOTAL_TURNS, GetTotalTurns() + turns);

        public static void IncrementElectionsWon()
            => PlayerPrefs.SetInt(KEY_ELECTIONS_WON, GetElectionsWon() + 1);

        public static void TryUpdateMaxGDP(float gdp)
        {
            if (gdp > GetMaxGDP())
                PlayerPrefs.SetFloat(KEY_MAX_GDP, gdp);
        }

        public static void TryUpdateMaxTurnsSingle(int turns)
        {
            if (turns > GetMaxTurnsSingle())
                PlayerPrefs.SetInt(KEY_MAX_TURNS_SINGLE, turns);
        }

        public static void TryUpdateBestLegitimacy(float legit)
        {
            if (legit > GetBestLegitimacy())
                PlayerPrefs.SetFloat(KEY_BEST_LEGITIMACY, legit);
        }

        // ============================================================
        // OKU
        // ============================================================

        public static int GetGamesPlayed() => PlayerPrefs.GetInt(KEY_GAMES_PLAYED, 0);
        public static int GetTotalTurns() => PlayerPrefs.GetInt(KEY_TOTAL_TURNS, 0);
        public static float GetMaxGDP() => PlayerPrefs.GetFloat(KEY_MAX_GDP, 0f);
        public static int GetMaxTurnsSingle() => PlayerPrefs.GetInt(KEY_MAX_TURNS_SINGLE, 0);
        public static float GetBestLegitimacy() => PlayerPrefs.GetFloat(KEY_BEST_LEGITIMACY, 0f);
        public static int GetElectionsWon() => PlayerPrefs.GetInt(KEY_ELECTIONS_WON, 0);

        // ============================================================
        // TÜM İSTATİSTİKLER (UI için)
        // ============================================================

        public static string[] GetStatsLines()
{
    return new[]
    {
        LocalizationManager.Get("stats_games_played", GetGamesPlayed()),
        LocalizationManager.Get("stats_total_turns", GetTotalTurns()),
        LocalizationManager.Get("stats_max_turns", GetMaxTurnsSingle()),
        LocalizationManager.Get("stats_elections_won", GetElectionsWon()),
        LocalizationManager.Get("stats_max_gdp", GetMaxGDP()),
        LocalizationManager.Get("stats_best_legit", GetBestLegitimacy())
    };
}

        /// <summary>Tüm istatistikleri sıfırla.</summary>
        public static void ResetAll()
        {
            PlayerPrefs.DeleteKey(KEY_GAMES_PLAYED);
            PlayerPrefs.DeleteKey(KEY_TOTAL_TURNS);
            PlayerPrefs.DeleteKey(KEY_MAX_GDP);
            PlayerPrefs.DeleteKey(KEY_MAX_TURNS_SINGLE);
            PlayerPrefs.DeleteKey(KEY_BEST_LEGITIMACY);
            PlayerPrefs.DeleteKey(KEY_ELECTIONS_WON);
            PlayerPrefs.Save();
        }
    }
}