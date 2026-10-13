using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using DemocracySim.Engine.Core;

// =====================================================================
//  CHRONICLE SYSTEM  —  "The Official History" / "Truth Debt"
//
//  Idea: most political sims simulate the PRESENT (budget, polls, votes).
//  This one also simulates MEMORY. The game records what really happened
//  (measured from the simulation itself) and the player decides how
//  history will REMEMBER it: make it legend, teach it plainly, bury it,
//  or blame someone else. Every lie buys legitimacy today and adds
//  "Truth Debt" — a hidden gap between story and reality that no stat
//  screen shows. Buried chapters can be exhumed; at high debt a
//  "Reckoning" collapses the official story. In opposition the player
//  can unearth the government's own buried history.
//
//  Deterministic (no UnityEngine.Random) so lockstep multiplayer stays in sync.
//  All text goes through LocalizationManager (keys chr_*).
// =====================================================================

[Serializable]
public class ChronicleEntry
{
    public int turn;
    public string kind;     // legit_up, legit_down, infl_up, infl_down, debt_up, debt_down
    public string arg;
    public float truth;     // -100 (harmful to citizens) .. +100 (beneficial)
    public int frame;       // 0 pending, 1 legend, 2 plain record, 3 buried, 4 scapegoat
    public bool exhumed;
}

[Serializable]
public class ChronicleState
{
    public List<ChronicleEntry> entries = new List<ChronicleEntry>();
    public float pride = 20f;        // strength of the national myth (0-100)
    public float debt = 0f;          // Truth Debt: gap between story and reality (0-100)
    public float credibility = 50f;  // how much historians/citizens trust you (0-100)
    public float rivalDebt = 10f;    // the rival government's Truth Debt (used when in opposition)
    public int reckonings = 0;
    public int level = 0;            // which debt threshold already fired (0-2)
    public int lastNotifyTurn = -99;
    public int turnNow = 0;
    public bool init = false;
    public float prevLeg, prevInfl, prevDebtVal;
}

public static class ChronicleSystem
{
    // ---------------- Tuning (all balance numbers live here) ----------------
    const int   MaxEntries        = 40;
    const int   PendingExpiryTurns = 16;
    const int   NotifyEvery       = 6;
    const float PrideDecay        = 0.5f;
    const float PrideLegitBonus   = 0.012f;   // per pride point above 30, per turn
    const float DebtDecayHonest   = 0.40f;    // credibility >= 60
    const float DebtDecayNormal   = 0.15f;
    const float UnearthCost       = 20f;
    const float UnearthMinDebt    = 15f;
    const string PrefKey          = "Chronicle_v1";

    static ChronicleState S = new ChronicleState();

    // ---------------- Lifecycle ----------------
    public static void Reset()
    {
        S = new ChronicleState();
        PlayerPrefs.DeleteKey(PrefKey);
    }
    public static string ExportJson() {
    try { return JsonUtility.ToJson(S); } catch { return ""; }
}
public static void ImportJson(string json) {
    if (string.IsNullOrEmpty(json)) return;
    try { var loaded = JsonUtility.FromJson<ChronicleState>(json); if (loaded != null) S = loaded; } catch { }
}

    public static void Save()
    {
     }
     
    

    public static void Load()
    {
     
    }

    // ---------------- Deterministic pseudo-random (lockstep safe) ----------------
    static float Hash01(int a, int b)
    {
        unchecked
        {
            uint h = (uint)(a * 73856093) ^ (uint)(b * 19349663);
            h ^= h >> 13; h *= 0x5bd1e995u; h ^= h >> 15;
            return (h & 0xFFFFFFu) / (float)0x1000000;
        }
    }

    static float Clamp100(float v) { return Mathf.Clamp(v, 0f, 100f); }
    static int Pending() { return S.entries.Count(x => x.frame == 0); }

    // ---------------- Turn hook ----------------
    public static void OnTurnEnd(SimulationEngine e, int turn, UIManager ui)
    {
        if (e == null) return;
        S.turnNow = turn;

        float leg  = (float)e.Legitimacy.CurrentLegitimacy;
        float infl = (float)e.Economy.Inflation;
        float debt = (float)e.Economy.NationalDebt;

        if (!S.init)
        {
            S.init = true; S.prevLeg = leg; S.prevInfl = infl; S.prevDebtVal = debt;
            return;
        }

        // 1) Measure what REALLY happened this turn (objective truth from the simulation)
        var found = new List<ChronicleEntry>();
        float dL = leg - S.prevLeg;
        if (Mathf.Abs(dL) >= 4f)
            found.Add(new ChronicleEntry { turn = turn, kind = dL > 0 ? "legit_up" : "legit_down", arg = "", truth = Mathf.Clamp(dL * 6f, -100f, 100f) });
        float dI = infl - S.prevInfl;
        if (Mathf.Abs(dI) >= 1.5f)
            found.Add(new ChronicleEntry { turn = turn, kind = dI > 0 ? "infl_up" : "infl_down", arg = "", truth = Mathf.Clamp(-dI * 20f, -100f, 100f) });
        float dD = (debt - S.prevDebtVal) / Mathf.Max(1f, Mathf.Abs(S.prevDebtVal));
        if (Mathf.Abs(dD) >= 0.05f)
            found.Add(new ChronicleEntry { turn = turn, kind = dD > 0 ? "debt_up" : "debt_down", arg = "", truth = Mathf.Clamp(-dD * 400f, -100f, 100f) });
        foreach (var f in found.OrderByDescending(x => Mathf.Abs(x.truth)).Take(2)) S.entries.Add(f);
        S.prevLeg = leg; S.prevInfl = infl; S.prevDebtVal = debt;

        // 2) Expire neglected entries: ignoring history = burying it (half debt)
        foreach (var en in S.entries)
            if (en.frame == 0 && turn - en.turn >= PendingExpiryTurns)
            {
                en.frame = 3;
                S.debt = Clamp100(S.debt + 4f * Bad(en));
            }

        // 3) Passive dynamics
        float bonus = Mathf.Max(0f, S.pride - 30f) * PrideLegitBonus;
        if (bonus > 0.01f) e.Legitimacy.AdjustLegitimacy(bonus);
        S.pride = Clamp100(S.pride - PrideDecay);
        S.debt  = Clamp100(S.debt - (S.credibility >= 60f ? DebtDecayHonest : DebtDecayNormal));
        if (e.CurrentRole == SimulationEngine.PlayerRole.Opposition) S.rivalDebt = Clamp100(S.rivalDebt + 0.9f);

        // 4) Buried / embellished chapters can be exhumed
        for (int i = 0; i < S.entries.Count; i++)
        {
            var en = S.entries[i];
            if (en.exhumed || en.truth > -20f || (en.frame != 1 && en.frame != 3 && en.frame != 4)) continue;
            float p = (0.01f + S.debt / 100f * 0.06f) * (en.frame == 4 ? 2f : 1f);
            if (Hash01(turn, i + en.turn * 31) < p)
            {
                en.exhumed = true;
                float hit = Mathf.Min(12f, Mathf.Abs(en.truth) * 0.12f);
                e.Legitimacy.AdjustLegitimacy(-hit);
                S.debt = Clamp100(S.debt + 5f);
                S.pride = Clamp100(S.pride - Mathf.Abs(en.truth) * 0.2f);
                S.credibility = Clamp100(S.credibility - 6f);
                ui?.WriteLog(LocalizationManager.Get("chr_exhumed", Describe(en), -hit));
                ui?.Notify(LocalizationManager.Get("chr_exhumed", Describe(en), -hit), true);
            }
        }

        // 5) Reckonings
        if (S.debt < 30f) S.level = 0;
        if (S.debt >= 40f && S.level < 1)
        {
            S.level = 1; e.Legitimacy.AdjustLegitimacy(-5f);
            ui?.WriteLog(LocalizationManager.Get("chr_reck1")); ui?.Notify(LocalizationManager.Get("chr_reck1"), true);
        }
        if (S.debt >= 70f && S.level < 2)
        {
            S.level = 2; e.Legitimacy.AdjustLegitimacy(-10f);
            e.PoliticalCapital -= 15f; if (e.PoliticalCapital < 0) e.PoliticalCapital = 0;
            ui?.WriteLog(LocalizationManager.Get("chr_reck2")); ui?.Notify(LocalizationManager.Get("chr_reck2"), true);
        }
        if (S.debt >= 100f)
        {
            e.Legitimacy.AdjustLegitimacy(-20f);
            S.reckonings++; S.pride = 0f; S.debt = 35f; S.level = 0; S.credibility = Clamp100(S.credibility - 15f);
            foreach (var en in S.entries) if (en.truth < 0f) en.exhumed = true;
            ui?.WriteLog(LocalizationManager.Get("chr_reck3")); ui?.Notify(LocalizationManager.Get("chr_reck3"), true);
        }
        // Opposition reward: the rival's story collapses on its own
        if (S.rivalDebt >= 80f && e.CurrentRole == SimulationEngine.PlayerRole.Opposition)
        {
            e.Legitimacy.AdjustLegitimacy(8f); S.rivalDebt = 40f;
            ui?.WriteLog(LocalizationManager.Get("chr_commission")); ui?.Notify(LocalizationManager.Get("chr_commission"), false);
        }

        // 6) Remind the player
        int pend = Pending();
        if (pend >= 2 && turn - S.lastNotifyTurn >= NotifyEvery)
        {
            S.lastNotifyTurn = turn;
            ui?.Notify(LocalizationManager.Get("chr_board_ready", pend), false);
        }

        // 7) Housekeeping
        while (S.entries.Count > MaxEntries)
        {
            int idx = S.entries.FindIndex(x => x.frame != 0);
            S.entries.RemoveAt(idx >= 0 ? idx : 0);
        }
    }

    // ---------------- Framing math ----------------
    static float Bad(ChronicleEntry en)  { return Mathf.Max(0f, -en.truth) / 100f; }
    static float Good(ChronicleEntry en) { return Mathf.Max(0f,  en.truth) / 100f; }

    struct Delta { public float legit, debt, cred, pride; }

    static Delta Preview(ChronicleEntry en, int frame)
    {
        float b = Bad(en), g = Good(en);
        var d = new Delta();
        switch (frame)
        {
            case 1: d.legit = 2f + 5f * b;       d.debt = 12f * b + 2f * g; d.cred = 0f;  d.pride = 6f + 6f * g; break; // legend
            case 2: d.legit = -3f * b + 2f * g;  d.debt = -4f;              d.cred = 6f;  d.pride = 3f * g;      break; // plain record
            case 3: d.legit = 0f;                d.debt = 8f * b;           d.cred = 0f;  d.pride = -1f * g;     break; // buried
            case 4: d.legit = 1f + 3f * b;       d.debt = 6f * b;           d.cred = -5f; d.pride = 2f;          break; // scapegoat
        }
        return d;
    }

    static void Apply(SimulationEngine e, ChronicleEntry en, int frame, UIManager ui)
    {
        var d = Preview(en, frame);
        en.frame = frame;
        if (Mathf.Abs(d.legit) > 0.001f) e.Legitimacy.AdjustLegitimacy(d.legit);
        S.debt = Clamp100(S.debt + d.debt);
        S.credibility = Clamp100(S.credibility + d.cred);
        S.pride = Clamp100(S.pride + d.pride);
        ui?.WriteLog(LocalizationManager.Get("chr_logged", Describe(en), LocalizationManager.Get("chr_fn_" + frame)));
    }

    static string Describe(ChronicleEntry en)
    {
        return LocalizationManager.Get("chr_k_" + en.kind, en.arg ?? "", en.turn);
    }

    // ---------------- UI (uses the existing choice popup only) ----------------
    public static string MenuLabel()
    {
        int p = Pending();
        return p > 0 ? LocalizationManager.Get("chr_menu", p) : LocalizationManager.Get("chr_menu_none");
    }

    static string StatusText()
    {
        string hint = S.debt >= 70f ? LocalizationManager.Get("chr_hint_high")
                    : S.debt >= 35f ? LocalizationManager.Get("chr_hint_mid")
                    : LocalizationManager.Get("chr_hint_low");
        return LocalizationManager.Get("chr_status", S.pride, S.debt, S.credibility, S.reckonings, Pending(), hint);
    }

    public static void OpenMenu(UIManager ui, SimulationEngine e, Action refresh)
    {
        if (ui == null || e == null) return;
        var opts = new List<(string label, Action onClick)>();
        if (Pending() > 0)
            opts.Add((LocalizationManager.Get("chr_open_board"), () => OpenBoard(ui, e, refresh)));
        if (e.CurrentRole == SimulationEngine.PlayerRole.Opposition)
            opts.Add((LocalizationManager.Get("chr_unearth", UnearthCost), () => Unearth(ui, e, refresh)));
        opts.Add((LocalizationManager.Get("close"), () => { }));
        ui.ShowChoicePopup(StatusText(), opts);
    }

    static void OpenBoard(UIManager ui, SimulationEngine e, Action refresh)
    {
        var queue = S.entries.Where(x => x.frame == 0).OrderByDescending(x => Mathf.Abs(x.truth)).Take(3).ToList();
        if (queue.Count == 0) { ui.Notify(LocalizationManager.Get("chr_board_empty"), false); return; }
        ShowEntry(ui, e, queue, 0, refresh);
    }

    static void ShowEntry(UIManager ui, SimulationEngine e, List<ChronicleEntry> queue, int i, Action refresh)
    {
        if (i >= queue.Count) { refresh?.Invoke(); return; }
        var en = queue[i];
        string reality = en.truth >= 15f ? LocalizationManager.Get("chr_real_good", en.truth)
                       : en.truth <= -15f ? LocalizationManager.Get("chr_real_bad", en.truth)
                       : LocalizationManager.Get("chr_real_mixed", en.truth);
        string title = LocalizationManager.Get("chr_entry_title", i + 1, queue.Count, Describe(en), reality);

        var opts = new List<(string label, Action onClick)>();
        var l = Preview(en, 1);
        opts.Add((LocalizationManager.Get("chr_f_legend", l.legit, l.debt), () => { Apply(e, en, 1, ui); ShowEntry(ui, e, queue, i + 1, refresh); }));
        var r = Preview(en, 2);
        opts.Add((LocalizationManager.Get("chr_f_record", r.legit, r.debt), () => { Apply(e, en, 2, ui); ShowEntry(ui, e, queue, i + 1, refresh); }));
        var b = Preview(en, 3);
        opts.Add((LocalizationManager.Get("chr_f_silence", b.debt), () => { Apply(e, en, 3, ui); ShowEntry(ui, e, queue, i + 1, refresh); }));
        if (Bad(en) >= 0.2f)
        {
            var s = Preview(en, 4);
            opts.Add((LocalizationManager.Get("chr_f_scapegoat", s.legit, s.debt), () => { Apply(e, en, 4, ui); ShowEntry(ui, e, queue, i + 1, refresh); }));
        }
        ui.ShowChoicePopup(title, opts);
    }

    // Opposition: expose the government's buried history
    static void Unearth(UIManager ui, SimulationEngine e, Action refresh)
    {
        if (e.PoliticalCapital < UnearthCost) { ui.WriteLog(LocalizationManager.Get("gm_041", UnearthCost)); return; }
        if (S.rivalDebt < UnearthMinDebt)
        {
            ui.WriteLog(LocalizationManager.Get("chr_unearth_fail", UnearthMinDebt));
            return;
        }
        e.PoliticalCapital -= UnearthCost;
        float gain = Mathf.Min(11f, 3f + S.rivalDebt * 0.08f);
        e.Legitimacy.AdjustLegitimacy(gain);
        S.rivalDebt = Mathf.Max(0f, S.rivalDebt - 25f);
        S.credibility = Clamp100(S.credibility + 3f);
        ui.WriteLog(LocalizationManager.Get("chr_unearth_ok", gain));
        ui.Notify(LocalizationManager.Get("chr_unearth_ok", gain), false);
        refresh?.Invoke();
    }

    // ---------------- Epitaph (shown on the ending screen) ----------------
    public static string Epitaph()
    {
        int v;
        if (S.pride >= 60f && S.debt < 25f) v = 1;                       // earned myth
        else if (S.pride >= 45f && S.debt >= 50f) v = 2;                  // monument of lies
        else if (S.credibility >= 65f && S.pride < 40f) v = 3;            // honest record
        else if (S.reckonings >= 1) v = 4;                                // exhumed
        else v = 5;                                                       // footnote
        return LocalizationManager.Get("chr_ep", LocalizationManager.Get("chr_ep_t" + v), LocalizationManager.Get("chr_ep_b" + v));
    }
}
