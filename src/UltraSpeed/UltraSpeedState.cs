using System;
using System.Collections.Generic;
using Core;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace NineKings.UltraSpeed;

/// <summary>
/// Estado de las velocidades extra y construccion de sus botones.
///
/// El enganche es <c>Core.Game.OverrideSpeed</c>: <c>Game.GetGameSpeed()</c> lo consulta
/// justo despues de resolver pausa/menus y justo antes del switch sobre
/// <c>FastingForward</c>, asi que escribirlo da la velocidad deseada sin romper la pausa.
/// </summary>
internal static class UltraSpeedState
{
    /// <summary>Indice en <see cref="UltraSpeedPlugin.Tiers"/>, o -1 si esta apagado.</summary>
    internal static int ActiveTier = -1;

    /// <summary>
    /// Velocidad extra que hay que restaurar al empezar cada oleada. El juego hace lo mismo
    /// con 1x/2x/3x via <c>m_PreferredFastingForward</c>, pero su enum no tiene sitio para
    /// las nuestras, asi que llevamos la preferencia en paralelo.
    /// </summary>
    internal static int PreferredTier = -1;

    /// <summary>Evita que nuestras propias llamadas a SetSuperFastSpeed se auto-apaguen.</summary>
    internal static bool Suppress;

    /// <summary>Cierto mientras el juego restaura su estado de velocidad, no el jugador.</summary>
    internal static bool Restoring;

    private static GameplayUI_ChangeSpeedView _view;
    private static readonly List<GameplayUI_SpeedControlButton> Buttons = new();

    // ---------------------------------------------------------------- overrides

    private static void ApplyOverride(float value)
    {
        var n = new Il2CppSystem.Nullable<float>(value);
        n.hasValue = true;
        Game.OverrideSpeed = n;
        Game.UpdateSpeed();
    }

    private static void ClearOverride()
    {
        // El setter de OverrideSpeed hace unbox del valor, asi que nunca puede recibir null:
        // se pasa un Nullable construido con hasValue = false.
        var n = new Il2CppSystem.Nullable<float>(0f);
        n.hasValue = false;
        Game.OverrideSpeed = n;
        Game.UpdateSpeed();
    }

    // ---------------------------------------------------------------- on / off

    internal static void Enable(int tier)
    {
        var tiers = UltraSpeedPlugin.Tiers;
        if (_view == null || tier < 0 || tier >= tiers.Length) return;

        // Dejar el estado interno del juego en SuperFast para que toda su logica
        // (tutoriales, GameplayManager, preferencia propia) siga siendo coherente.
        Suppress = true;
        try { _view.SetSuperFastSpeed(); }
        finally { Suppress = false; }

        ActiveTier = tier;
        if (UltraSpeedPlugin.Persist.Value) PreferredTier = tier;

        ApplyOverride(tiers[tier]);
        RefreshSelection();
    }

    internal static void Disable()
    {
        if (ActiveTier < 0) return;
        ActiveTier = -1;
        ClearOverride();
        RefreshSelection();
    }

    /// <summary>Vuelve al 3x original. Su postfix se encarga de apagar el override.</summary>
    internal static void DropToSuperFast()
    {
        if (_view == null) { Disable(); return; }
        _view.SetSuperFastSpeed();
    }

    /// <summary>Reaplica la velocidad preferida al empezar una oleada.</summary>
    internal static void RestorePreferred()
    {
        if (!UltraSpeedPlugin.Persist.Value) return;
        if (PreferredTier < 0 || PreferredTier >= UltraSpeedPlugin.Tiers.Length) return;
        if (ActiveTier == PreferredTier) return;
        Enable(PreferredTier);
    }

    /// <summary>El jugador eligio 1x/2x/3x a mano: deja de haber preferencia extra.</summary>
    internal static void ForgetPreferred() => PreferredTier = -1;

    private static void RefreshSelection()
    {
        for (int i = 0; i < Buttons.Count; i++)
        {
            var b = Buttons[i];
            if (b != null) b.SetSelected(i == ActiveTier);
        }

        if (ActiveTier >= 0 && _view != null && _view.m_SuperFastControl != null)
            _view.m_SuperFastControl.SetSelected(false);
    }

    /// <summary>Fuerza la reconstruccion de los botones tras un cambio de configuracion.</summary>
    internal static void Invalidate()
    {
        foreach (var b in Buttons)
            if (b != null && b.gameObject != null)
                UnityEngine.Object.Destroy(b.gameObject);

        Buttons.Clear();

        if (_autoRect != null) _autoRect.anchoredPosition = _autoBasePosition;
        _autoRect = null;

        _view = null;
        ActiveTier = -1;
    }

    // ---------------------------------------------------------------- botones

    /// <summary>Crea un boton por velocidad clonando el de SuperFast. Idempotente.</summary>
    internal static void EnsureButtons(GameplayUI_ChangeSpeedView view)
    {
        if (view == null) return;

        var tiers = UltraSpeedPlugin.Tiers;
        if (tiers.Length == 0) return;

        if (_view == view && Buttons.Count == tiers.Length && AllAlive())
            return;

        var source = view.m_SuperFastControl;
        if (source == null || source.gameObject == null) return;

        var sourceGo = source.gameObject;
        var parent = sourceGo.transform.parent;
        if (parent == null) return;

        Buttons.Clear();
        _view = view;
        ActiveTier = -1;

        int baseIndex = sourceGo.transform.GetSiblingIndex();
        var step = MeasureStep(view, sourceGo);

        LogLayout(parent, sourceGo, step);

        for (int i = 0; i < tiers.Length; i++)
        {
            var clone = BuildButton(sourceGo, parent, i, baseIndex + 1 + i);
            if (clone == null) continue;
            Buttons.Add(clone);
        }

        NudgeAutoButton(view, step, tiers.Length);

        UltraSpeedPlugin.Logger.LogInfo($"Botones extra creados: {Buttons.Count}.");

        // Tras rehacer la barra (cambio de oleada, reapertura de la UI) hay que
        // devolver la velocidad preferida, si la habia.
        RestorePreferred();
    }

    private static bool AllAlive()
    {
        foreach (var b in Buttons)
            if (b == null || b.gameObject == null) return false;
        return true;
    }

    /// <summary>
    /// Separacion entre dos botones consecutivos de la fila, medida sobre 2x y 3x.
    /// Sirve para colocar los nuestros a mano sin depender del layout group.
    /// </summary>
    private static Vector2 MeasureStep(GameplayUI_ChangeSpeedView view, GameObject sourceGo)
    {
        var superFast = sourceGo.GetComponent<RectTransform>();
        var fast = view.m_FastControl != null && view.m_FastControl.gameObject != null
            ? view.m_FastControl.gameObject.GetComponent<RectTransform>()
            : null;

        if (superFast != null && fast != null)
        {
            var delta = superFast.anchoredPosition - fast.anchoredPosition;
            if (delta.sqrMagnitude > 0.01f) return delta;
        }

        // Sin referencia utilizable: separar por el ancho del propio boton.
        float width = superFast != null ? superFast.rect.width : 48f;
        return new Vector2(width > 1f ? width : 48f, 0f);
    }

    private static void LogLayout(Transform parent, GameObject sourceGo, Vector2 step)
    {
        var rt = sourceGo.GetComponent<RectTransform>();
        var pos = rt != null ? rt.anchoredPosition : Vector2.zero;
        var size = rt != null ? rt.rect.size : Vector2.zero;

        bool hLayout = parent.GetComponent<HorizontalLayoutGroup>() != null;
        bool vLayout = parent.GetComponent<VerticalLayoutGroup>() != null;
        bool gLayout = parent.GetComponent<GridLayoutGroup>() != null;
        bool fitter = parent.GetComponent<ContentSizeFitter>() != null;

        var holder = parent.GetComponent<RectTransform>();
        var holderRect = holder != null ? holder.rect.size : Vector2.zero;

        UltraSpeedPlugin.Logger.LogInfo(
            $"Layout '{parent.name}' (padre '{(parent.parent != null ? parent.parent.name : "-")}'): " +
            $"hLayout={hLayout} vLayout={vLayout} grid={gLayout} fitter={fitter}; " +
            $"holder rect={holderRect}; 3x pos={pos} size={size}; step={step}");
    }

    private static GameplayUI_SpeedControlButton BuildButton(
        GameObject sourceGo, Transform parent, int tier, int siblingIndex)
    {
        GameObject cloneGo;
        try
        {
            cloneGo = UnityEngine.Object.Instantiate(sourceGo, parent);
        }
        catch (Exception e)
        {
            UltraSpeedPlugin.Logger.LogError($"No se pudo clonar el boton de velocidad: {e}");
            return null;
        }

        cloneGo.name = $"UltraSpeedControl_{tier}";
        cloneGo.transform.SetSiblingIndex(siblingIndex);

        var clone = cloneGo.GetComponent<GameplayUI_SpeedControlButton>();
        if (clone == null)
        {
            UltraSpeedPlugin.Logger.LogError("El clon no tiene GameplayUI_SpeedControlButton.");
            UnityEngine.Object.Destroy(cloneGo);
            return null;
        }

        RewireClick(clone, tier);
        Tint(clone, tier);
        clone.SetTutorialFocus(false);
        clone.SetSelected(false);
        return clone;
    }

    private static RectTransform _autoRect;
    private static Vector2 _autoBasePosition;

    /// <summary>
    /// Aparta el boton de auto-disparo para que la fila ensanchada no lo tape.
    ///
    /// AutoShootBtn no vive dentro del Holder: es hermano suyo, anclado por su cuenta a la
    /// esquina. El Holder lleva un HorizontalLayoutGroup alineado a la derecha, asi que al
    /// anadir botones la fila crece hacia la izquierda y acaba pasando por encima de auto.
    /// Como el Holder va despues en la jerarquia, se dibuja encima y se queda con el clic.
    ///
    /// El desplazamiento necesario es exactamente lo que crece la fila, un ancho de boton
    /// por cada velocidad nueva.
    /// </summary>
    private static void NudgeAutoButton(GameplayUI_ChangeSpeedView view, Vector2 step, int extraButtons)
    {
        var auto = view.m_AutoShootControl;
        if (auto == null || auto.gameObject == null) return;

        var rect = auto.gameObject.GetComponent<RectTransform>();
        if (rect == null) return;

        // La posicion base se guarda una sola vez por boton: remedirla en cada
        // reconstruccion iria acumulando el desplazamiento.
        if (_autoRect == null || _autoRect.GetInstanceID() != rect.GetInstanceID())
        {
            _autoRect = rect;
            _autoBasePosition = rect.anchoredPosition;
        }

        float shift = -Mathf.Abs(step.x) * extraButtons + UltraSpeedPlugin.AutoButtonNudgeX.Value;
        rect.anchoredPosition = _autoBasePosition + new Vector2(shift, 0f);

        UltraSpeedPlugin.Logger.LogInfo(
            $"Boton auto: {_autoBasePosition} -> {rect.anchoredPosition} (desplazamiento {shift}).");
    }

    /// <summary>Vuelca la jerarquia de la barra de velocidad para diagnosticar el layout.</summary>
    private static void DumpHierarchy(GameplayUI_ChangeSpeedView view)
    {
        if (view == null || view.transform == null) return;

        var sb = new System.Text.StringBuilder();
        sb.AppendLine("Jerarquia de la barra de velocidad:");
        Describe(sb, view.transform, 0, 3);

        sb.AppendLine($"  pause  = {NameOf(view.m_PauseGameControl)}");
        sb.AppendLine($"  auto   = {NameOf(view.m_AutoShootControl)}");
        sb.AppendLine($"  normal = {NameOf(view.m_NormalControl)}");
        sb.AppendLine($"  fast   = {NameOf(view.m_FastControl)}");
        sb.AppendLine($"  super  = {NameOf(view.m_SuperFastControl)}");

        UltraSpeedPlugin.Logger.LogInfo(sb.ToString());
    }

    private static string NameOf(GameplayUI_SpeedControlButton button)
    {
        if (button == null || button.gameObject == null) return "(nulo)";
        var t = button.gameObject.transform;
        var parent = t.parent != null ? t.parent.name : "-";
        return $"{parent}/{t.name} (indice {t.GetSiblingIndex()})";
    }

    private static void Describe(System.Text.StringBuilder sb, Transform t, int depth, int maxDepth)
    {
        var pad = new string(' ', 2 + depth * 2);
        var rect = t.GetComponent<RectTransform>();

        if (rect != null)
            sb.AppendLine($"{pad}{t.name} rect={rect.rect.size} pos={rect.anchoredPosition} " +
                          $"sizeDelta={rect.sizeDelta} aMin={rect.anchorMin} aMax={rect.anchorMax} pivot={rect.pivot}");
        else
            sb.AppendLine($"{pad}{t.name} (sin RectTransform)");

        if (depth >= maxDepth) return;
        for (int i = 0; i < t.childCount; i++)
            Describe(sb, t.GetChild(i), depth + 1, maxDepth);
    }

    /// <summary>
    /// El clon hereda el listener persistente del prefab, que apunta a SetSuperFastSpeed
    /// sobre la vista original. Hay que apagarlo antes de enganchar el nuestro.
    /// </summary>
    private static void RewireClick(GameplayUI_SpeedControlButton clone, int tier)
    {
        var button = clone.m_Button;
        if (button == null) button = clone.GetComponent<Button>();
        if (button == null)
        {
            UltraSpeedPlugin.Logger.LogError("El clon no tiene Button.");
            return;
        }

        var click = button.onClick;
        for (int i = click.GetPersistentEventCount() - 1; i >= 0; i--)
            click.SetPersistentListenerState(i, UnityEventCallState.Off);
        click.RemoveAllListeners();
        click.AddListener((UnityAction)new Action(() => Toggle(tier)));
    }

    internal static void Toggle(int tier)
    {
        if (ActiveTier == tier) DropToSuperFast();
        else Enable(tier);
    }

    private static void Tint(GameplayUI_SpeedControlButton clone, int tier)
    {
        var palette = UltraSpeedPlugin.SplitList(UltraSpeedPlugin.IconTints.Value);
        if (palette.Length == 0) return;

        var hex = palette[tier % palette.Length].Trim();
        if (hex.Length == 0) return;

        if (!ColorUtility.TryParseHtmlString(hex, out var color))
        {
            UltraSpeedPlugin.Logger.LogWarning($"IconTints: {hex} no es un color valido; se deja sin tenir.");
            return;
        }

        var root = clone.m_Image != null ? clone.m_Image : clone.gameObject;
        var images = root.GetComponentsInChildren<Image>(true);
        for (int i = 0; i < images.Length; i++)
            images[i].color = color;
    }
}
