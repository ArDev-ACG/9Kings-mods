using System;
using System.Collections.Generic;
using System.Globalization;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using BepInEx.Unity.IL2CPP;
using HarmonyLib;

namespace NineKings.UltraSpeed;

[BepInPlugin(Guid, Name, Version)]
public class UltraSpeedPlugin : BasePlugin
{
    public const string Guid = "com.mods.ninekings.ultraspeed";
    public const string Name = "9 Kings - Ultra Speed";
    public const string Version = "1.0.0";

    internal static ManualLogSource Logger;

    internal static ConfigEntry<string> Speeds;
    internal static ConfigEntry<string> Hotkeys;
    internal static ConfigEntry<string> IconTints;
    internal static ConfigEntry<bool> IncludeInCycle;
    internal static ConfigEntry<bool> Persist;
    internal static ConfigEntry<float> AutoButtonNudgeX;

    /// <summary>Velocidades extra ya parseadas, en orden ascendente de rapidez.</summary>
    internal static float[] Tiers = Array.Empty<float>();

    public override void Load()
    {
        Logger = base.Log;

        Speeds = Config.Bind(
            "General", "Speeds", "4,6,8",
            "Velocidades extra, separadas por coma, de menor a mayor. Cada una crea un boton " +
            "propio a la derecha del 3x. El juego solo valida hasta 6.0 en su comando de consola " +
            "SetSpeed; no hay tope real, pero por encima de 6 la simulacion da pasos mas grandes " +
            "por frame y puede volverse imprecisa.");

        Hotkeys = Config.Bind(
            "General", "Hotkeys", "Digit4,Digit5,Digit6",
            "Atajos por velocidad, en el mismo orden que Speeds. Nombres del enum " +
            "UnityEngine.InputSystem.Key (Digit1..Digit9, Numpad4, F5...). Vacio = sin atajos.");

        IconTints = Config.Bind(
            "Appearance", "IconTints", "#FFD24A,#FF8A3D,#FF3D3D",
            "Color hex de cada boton nuevo, en el mismo orden que Speeds. Si hay menos colores " +
            "que velocidades se reutilizan en ciclo. Vacio = sin tenir.");

        IncludeInCycle = Config.Bind(
            "General", "IncludeInCycle", true,
            "Incluir las velocidades nuevas en los controles de subir/bajar/ciclar velocidad.");

        Persist = Config.Bind(
            "General", "Persist", true,
            "Mantener la velocidad elegida entre oleadas, igual que hace el juego con 1x/2x/3x. " +
            "El juego restaura su velocidad preferida al empezar cada oleada; con esto la " +
            "preferencia tambien puede ser una de las velocidades nuevas.");

        AutoButtonNudgeX = Config.Bind(
            "Appearance", "AutoButtonNudgeX", 0f,
            "Ajuste manual en pixeles de la posicion del boton de auto-disparo. El mod ya lo " +
            "aparta solo lo que crece la fila (un ancho de boton por velocidad nueva); esto se " +
            "suma a ese desplazamiento. Negativo = mas a la izquierda.");

        Tiers = ParseSpeeds(Speeds.Value);
        Speeds.SettingChanged += OnSpeedsChanged;
        AutoButtonNudgeX.SettingChanged += OnTintsChanged;
        IconTints.SettingChanged += OnTintsChanged;

        new Harmony(Guid).PatchAll(typeof(SpeedPatches));

        Logger.LogInfo($"Cargado. Velocidades extra: {string.Join(", ", FormatTiers())}.");
    }

    public override bool Unload()
    {
        UltraSpeedState.Disable();
        return true;
    }

    // Handlers con nombre en vez de lambdas: BepInEx.Core esta anotado con nullable y el
    // compilador intentaria emitir NullableAttribute, que no existe en Il2Cppmscorlib.
    private static void OnSpeedsChanged(object sender, EventArgs args)
    {
        Tiers = ParseSpeeds(Speeds.Value);
        UltraSpeedState.Invalidate();
    }

    private static void OnTintsChanged(object sender, EventArgs args)
    {
        UltraSpeedState.Invalidate();
    }

    private static IEnumerable<string> FormatTiers()
    {
        foreach (var t in Tiers)
            yield return t.ToString("0.##", CultureInfo.InvariantCulture) + "x";
    }

    internal static string[] SplitList(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return Array.Empty<string>();
        return raw.Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries);
    }

    private static float[] ParseSpeeds(string raw)
    {
        var parts = SplitList(raw);
        var list = new List<float>(parts.Length);

        foreach (var part in parts)
        {
            if (!float.TryParse(part.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out var value))
            {
                Logger.LogWarning($"Speeds: '{part.Trim()}' no es un numero; se ignora.");
                continue;
            }
            if (value <= 0f)
            {
                Logger.LogWarning($"Speeds: {value} no es positivo; se ignora.");
                continue;
            }
            list.Add(value);
        }

        list.Sort();
        return list.ToArray();
    }
}
