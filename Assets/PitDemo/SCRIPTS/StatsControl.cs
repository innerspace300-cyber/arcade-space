using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class StatsControl : MonoBehaviour
{
    public PlayerStats playerStats;

    /// ARcade: a heal or some mana came in - even onto a full bar, so the
    /// player's outline and the HUD flash for every pickup (PlayerGlow,
    /// HudJuice).
    public static event System.Action Healed, ManaGained;

    /// ARcade: mana from a pickup, never past the bar's end.
    public void AddMP(int amount)
    {
        if (playerStats == null || amount <= 0) return;
        ManaGained?.Invoke();
        if (playerStats.MaxMP > 0) playerStats.mana = Mathf.Min(playerStats.MaxMP, playerStats.mana + amount);
    }

    void Start()
    {
        if (!playerStats)
        {
            playerStats = FindObjectOfType<PlayerStats>();
        }
    }

    public void IncreaseHP(int value)
    {
        if ((playerStats.health + value) < 0)
        {
            return;
        }

        // ARcade: the Shield Buff - nothing hurts (PlayerBuffs).
        if (value < 0 && PlayerBuffs.Shield) return;

        if (value > 0) Healed?.Invoke();
        playerStats.health += value;
    }

    public void IncreaseMP(int value)
    {
        if ((playerStats.mana + value) < 0)
        {
            return;
        }

        playerStats.mana += value;
    }

    public void DecreaseMaxHP(int value)
    {
        if (playerStats.MaxHP + value < Mathf.Abs(value))
        {
            return;
        }

        playerStats.MaxHP += value;
    }

    public void DecreaseMaxMP(int value)
    {
        if (playerStats.MaxMP + value < Mathf.Abs(value))
        {
            return;
        }

        playerStats.MaxMP += value;
    }

    public int GetCurrentHP()
    {
        return playerStats != null ? playerStats.health : 0;
    }

    public int GetMaxHP()
    {
        return playerStats != null ? playerStats.MaxHP : 0;
    }

    /// <summary>
    /// Heals the player by amount but never exceeds MaxHP.
    /// Does nothing if already at full health.
    /// </summary>
    public void HealHP(int amount)
    {
        if (playerStats == null) return;
        if (amount > 0) Healed?.Invoke();
        int current = playerStats.health;
        int max     = playerStats.MaxHP;
        if (current >= max) return;                          // already full
        int clamped = Mathf.Min(amount, max - current);     // don't overheal
        playerStats.health += clamped;
    }
}