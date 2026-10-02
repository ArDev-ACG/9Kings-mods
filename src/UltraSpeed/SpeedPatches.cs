using System;
using System.Collections.Generic;
using Core;
using HarmonyLib;
using UnityEngine.InputSystem;

namespace NineKings.UltraSpeed;

using Speed = GameplayUI_ChangeSpeedView.Speed;

[HarmonyPatch(typeof(GameplayUI_ChangeSpeedView))]
internal static class SpeedPatches
{
    /// <summary>
    /// Update() de la vista solo lee atajos de teclado; no repinta la seleccion.
    /// Se aprovecha como tick: crea los botones si faltan y atiende nuestros atajos.
    /// </summary>
    [HarmonyPostfix]
    [HarmonyPatch(nameof(GameplayUI_ChangeSpeedView.Update))]
    private static void Update_Postfix(GameplayUI_ChangeSpeedView __instance)
    {
        UltraSpeedState.EnsureButtons(__instance);
        PollHotkeys();
    }

    /// <summary>
    /// Todos los cambios de velocidad del juego (Normal/Fast/SuperFast/Paused) pasan
    /// por aqui, asi que es el unico sitio donde hace falta apagar el override.
    /// </summary>
    [HarmonyPostfix]
    [HarmonyPatch(nameof(GameplayUI_ChangeSpeedView.EnableFastingForward))]
    private static void EnableFastingForward_Postfix()
    {
        if (!UltraSpeedState.Suppress)
            UltraSpeedState.Disable();
    }

    // ------------------------------------------------------- persistencia entre oleadas

    // Wave.StartWave() llama SetSpeedAsPreferred(), que reaplica m_PreferredFastingForward.
    // Ese enum solo llega hasta SuperFast, asi que aqui se vuelve a subir a la velocidad
    // extra que el jugador tenia elegida.

    [HarmonyPrefix]
    [HarmonyPatch(nameof(GameplayUI_ChangeSpeedView.SetSpeedAsPreferred))]
    private static void SetSpeedAsPreferred_Prefix() => UltraSpeedState.Restoring = true;

    [HarmonyPostfix]
    [HarmonyPatch(nameof(GameplayUI_ChangeSpeedView.SetSpeedAsPreferred))]
    private static void SetSpeedAsPreferred_Postfix()
    {
        UltraSpeedState.Restoring = false;
        UltraSpeedState.RestorePreferred();
    }

    [HarmonyPrefix]
    [HarmonyPatch(nameof(GameplayUI_ChangeSpeedView.ApplyInitialState))]
    private static void ApplyInitialState_Prefix() => UltraSpeedState.Restoring = true;

    [HarmonyPostfix]
    [HarmonyPatch(nameof(GameplayUI_ChangeSpeedView.ApplyInitialState))]
    private static void ApplyInitialState_Postfix()
    {
        UltraSpeedState.Restoring = false;
        UltraSpeedState.RestorePreferred();
    }

    // Elegir 1x/2x/3x a mano si cuenta como cambiar de preferencia; hacerlo el juego
    // mientras restaura estado, no.

    [HarmonyPostfix]
    [HarmonyPatch(nameof(GameplayUI_ChangeSpeedView.SetNormalSpeed))]
    private static void SetNormalSpeed_Postfix() => ForgetIfPlayerChose();

    [HarmonyPostfix]
    [HarmonyPatch(nameof(GameplayUI_ChangeSpeedView.SetFastSpeed))]
    private static void SetFastSpeed_Postfix() => ForgetIfPlayerChose();

    [HarmonyPostfix]
    [HarmonyPatch(nameof(GameplayUI_ChangeSpeedView.SetSuperFastSpeed))]
    private static void SetSuperFastSpeed_Postfix() => ForgetIfPlayerChose();

    private static void ForgetIfPlayerChose()
    {
        if (!UltraSpeedState.Suppress && !UltraSpeedState.Restoring)
            UltraSpeedState.ForgetPreferred();
    }

    // ------------------------------------------------------- subir / bajar / ciclar

    [HarmonyPrefix]
    [HarmonyPatch(nameof(GameplayUI_ChangeSpeedView.SpeedUpGame))]
    private static bool SpeedUpGame_Prefix()
    {
        if (!UltraSpeedPlugin.IncludeInCycle.Value) return true;

        int last = UltraSpeedPlugin.Tiers.Length - 1;
        if (last < 0) return true;

        if (UltraSpeedState.ActiveTier >= 0)
        {
            if (UltraSpeedState.ActiveTier < last)
                UltraSpeedState.Enable(UltraSpeedState.ActiveTier + 1);
            return false; // ya estamos por encima del rango original
        }

        if (GameplayUI_ChangeSpeedView.FastingForward == Speed.SuperFast)
        {
            UltraSpeedState.Enable(0);
            return false;
        }

        return true;
    }

    [HarmonyPrefix]
    [HarmonyPatch(nameof(GameplayUI_ChangeSpeedView.SlowDownGame))]
    private static bool SlowDownGame_Prefix()
    {
        if (!UltraSpeedPlugin.IncludeInCycle.Value) return true;
        if (UltraSpeedState.ActiveTier < 0) return true;

        if (UltraSpeedState.ActiveTier > 0)
            UltraSpeedState.Enable(UltraSpeedState.ActiveTier - 1);
        else
            UltraSpeedState.DropToSuperFast();

        return false;
    }

    [HarmonyPrefix]
    [HarmonyPatch(nameof(GameplayUI_ChangeSpeedView.CycleSpeed))]
    private static bool CycleSpeed_Prefix(GameplayUI_ChangeSpeedView __instance)
    {
        if (!UltraSpeedPlugin.IncludeInCycle.Value) return true;

        int last = UltraSpeedPlugin.Tiers.Length - 1;
        if (last < 0) return true;

        if (UltraSpeedState.ActiveTier >= 0)
        {
            if (UltraSpeedState.ActiveTier < last)
                UltraSpeedState.Enable(UltraSpeedState.ActiveTier + 1);
            else
                __instance.SetNormalSpeed(); // cierra el ciclo
            return false;
        }

        if (GameplayUI_ChangeSpeedView.FastingForward == Speed.SuperFast)
        {
            UltraSpeedState.Enable(0);
            return false;
        }

        return true;
    }

    // ------------------------------------------------------------------ atajos

    private static string _cachedRaw;
    private static Key[] _cachedKeys = Array.Empty<Key>();

    private static void PollHotkeys()
    {
        var keys = ResolveKeys(UltraSpeedPlugin.Hotkeys.Value);
        if (keys.Length == 0) return;

        var keyboard = Keyboard.current;
        if (keyboard == null) return;

        int count = Math.Min(keys.Length, UltraSpeedPlugin.Tiers.Length);
        for (int tier = 0; tier < count; tier++)
        {
            if (keys[tier] == Key.None) continue;

            var control = keyboard[keys[tier]];
            if (control == null || !control.wasPressedThisFrame) continue;

            UltraSpeedState.Toggle(tier);
            return;
        }
    }

    private static Key[] ResolveKeys(string raw)
    {
        if (string.Equals(raw, _cachedRaw, StringComparison.Ordinal))
            return _cachedKeys;

        _cachedRaw = raw;

        var names = UltraSpeedPlugin.SplitList(raw);
        var keys = new List<Key>(names.Length);

        foreach (var name in names)
        {
            if (Enum.TryParse<Key>(name.Trim(), true, out var parsed))
            {
                keys.Add(parsed);
                continue;
            }

            UltraSpeedPlugin.Logger.LogWarning(
                $"Hotkeys: {name.Trim()} no es un UnityEngine.InputSystem.Key valido; esa posicion queda sin atajo.");
            keys.Add(Key.None);
        }

        _cachedKeys = keys.ToArray();
        return _cachedKeys;
    }
}
