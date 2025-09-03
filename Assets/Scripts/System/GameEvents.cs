using System;
using UnityEngine;

public static class GameEvents
{
    // Player Events
    public static Action<float, float> OnPlayerHealthChanged;
    public static Action OnPlayerDied;
    public static Action<float> OnPlayerSpeedChanged;

    // Weapon Events
    public static Action<int, int> OnAmmoChanged;
    public static Action OnWeaponFired;
    public static Action OnWeaponReloaded;

    // Enemy Events
    public static Action<Vector3> OnTurretDestroyed;
    public static Action<int> OnEnemyKilled;

    // Game State Events
    public static Action<int> OnScoreChanged;
    public static Action<float> OnTimeChanged;
    public static Action OnGameStarted;
    public static Action OnGamePaused;
    public static Action OnGameOver;

    // UI Events
    public static Action<string> OnShowMessage;
    public static Action OnHideMessage;

    // Audio Events
    public static Action<AudioClip> OnPlaySFX;
    public static Action<string> OnPlayNamedSFX;
}
