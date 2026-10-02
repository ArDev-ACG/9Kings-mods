# 9 Kings — Investigación de modding

Fecha: 2026-08-27

## Juego

| Dato | Valor |
|---|---|
| Steam AppID | 2784470 |
| Ruta | `C:\Program Files (x86)\Steam\steamapps\common\9 Kings` |
| Motor | Unity **6000.3.8f1** |
| Backend scripting | **IL2CPP** (hay `GameAssembly.dll` + `il2cpp_data`, NO hay `Managed/`) |
| Metadata IL2CPP | versión **39** (sin cifrar, magic `0xFAB11BAF`) |
| Saves / settings | `C:\Users\<usuario>\AppData\LocalLow\SadSocket\9Kings` |
| Assemblies clave | `Assembly-CSharp`, usa Unity.Entities (DOTS), Odin, Unity.InputSystem |

## Cómo se moddea este juego

Al ser IL2CPP no se puede editar `Assembly-CSharp.dll` directamente. La vía estándar es
**BepInEx 6 (Unity.IL2CPP) + HarmonyX**, que inyecta código gestionado en runtime.

- Comunidad oficial: Thunderstore `9-kings` — https://thunderstore.io/c/9-kings/
- Loader: **BepInExPack IL2CPP 6.0.755** (BepInEx 6 Bleeding Edge)
  - Importante: BepInEx BE #755 es el primero con soporte de metadata IL2CPP v23–106,
    necesario para la v39 de Unity 6.x. Versiones anteriores fallan al generar interop.
- Mods existentes: sólo `9KCheatMode` (Caioreix) — desactualizado (~1 año).
  **No existe ningún mod de velocidad** → hueco libre.

### Herramientas usadas para el reverse

| Herramienta | Resultado |
|---|---|
| `Il2CppDumper v6.7.46` | ❌ no soporta metadata v39 |
| `Cpp2IL 2022.1.0-pre-release.21` | ✅ dump completo |

Comandos que funcionaron (en `tools/`):

```powershell
# DLLs "dummy" (referencias para compilar)
Cpp2IL.exe --game-path "<game>" --exe-name 9Kings --output-to dump\dummydll --output-as dummydll

# C# legible (firmas, campos, offsets)
Cpp2IL.exe --game-path "<game>" --exe-name 9Kings --output-to dump\cs --output-as diffable-cs

# Pseudo-ensamblador (cuerpos de método)
Cpp2IL.exe --game-path "<game>" --exe-name 9Kings --output-to dump\isil --output-as isil
```

## Sistema de velocidad del juego (ingeniería inversa)

### `Core.GameplayUI_ChangeSpeedView` (UISubController<GameplayUI>)

```csharp
internal enum Speed { Paused = 0, Normal = 1, Fast = 2, SuperFast = 3 }

public static Speed FastingForward { get; private set; }   // velocidad activa
public static bool  AutoAttackEnabled { get; private set; }
private Speed m_PreferredFastingForward;

// Botones (SerializeField) — el "row" de la UI:
public GameplayUI_SpeedControlButton m_PauseGameControl;
public GameplayUI_SpeedControlButton m_AutoShootControl;
public GameplayUI_SpeedControlButton m_NormalControl;
public GameplayUI_SpeedControlButton m_FastControl;
public GameplayUI_SpeedControlButton m_SuperFastControl;

void SetNormalSpeed(); void SetFastSpeed(); void SetSuperFastSpeed();
void EnableFastingForward(Speed value);
void SetPreferred(Speed enabled); void SetSpeedAsPreferred();
void CycleSpeed(); void SpeedUpGame(); void SlowDownGame();
void ApplyInitialState(); void Update();
```

### `Core.GameplayUI_SpeedControlButton` (MonoBehaviour, RequireComponent(Button))

```csharp
public Button     m_Button;
public GameObject m_Image;
public GameObject m_Selected;      // marco/halo de "seleccionado"
public GameObject m_TutorialFocus;

void SetActive(bool);  void SetScale(Vector3);
void SetSelected(bool); void SetTutorialFocus(bool);
```

### `Core.Game`

```csharp
public static float? OverrideSpeed;      // static field, offset 0x2C
public static float  GetGameSpeed();
public static void   UpdateSpeed();      // Time.timeScale = GetGameSpeed()
```

`UpdateSpeed()` es literalmente `Time.timeScale = Game.GetGameSpeed();`

### `Game.GetGameSpeed()` — lógica reconstruida desde ISIL

```csharp
static float GetGameSpeed()
{
    if (!Wave.<enBatalla>)              return 1f;          // const 0x182D56BB0 = 1.0
    if (GameplayUI.PauseMenu?.IsOpen)   return 0f;
    if (ShopView abierto / estados no-combate) return 1f;

    if (Game.OverrideSpeed.HasValue)    return Game.OverrideSpeed.Value;   // ← PUNTO DE ENGANCHE

    if (tutorialStage.UsesCombatSlowMotion()) return 0.05f;   // const 0x182D56C9C

    switch (GameplayUI_ChangeSpeedView.FastingForward) {
        case Speed.Fast:      return Wave.Options.FastSpeed;       // WaveOptionsSo +0x4C
        case Speed.SuperFast: return Wave.Options.SuperFastSpeed;  // WaveOptionsSo +0x50
        default:              return Wave.Options.NormalSpeed;     // WaveOptionsSo +0x48
    }
}
```

### `Core.Levels.WaveOptionsSo` (ScriptableObject)

```csharp
public float NormalSpeed;     // 0x48
public float FastSpeed;       // 0x4C
public float SuperFastSpeed;  // 0x50
public bool  EnableGameConsole;
public bool  EnableGameKeys;
```
Acceso: `Core.Wave.Options` (static property → `WaveOptionsSo`).

### Consola de desarrollador: `Console.Command_SetSpeed`

El juego trae un comando `SetSpeed <float>` que hace exactamente:

```csharp
if (float.TryParse(arg, out v) && v >= 0f && v <= 6f)   // cap: const 0x182D56DBC = 6.0
    Game.OverrideSpeed = v;
else
    Game.OverrideSpeed = null;   // "Invalid value, override reset"
```

> **El propio juego admite hasta 6.0x vía `Game.OverrideSpeed`.** No hay que parchear
> `GetGameSpeed`: basta escribir ese campo. Es el camino más limpio y estable.

### Input (`Core.Input` / `GameInput`)

Acciones ya existentes en el InputActionAsset:

- `ChangeSpeed`, `SpeedUpGame`
- `EnableNormalSpeed`, `EnableFastSpeed`, `EnableFastestSpeed`

## Diseño del MOD 1 — "Ultra Speed"

1. Clonar `m_SuperFastControl` (GameObject) como hermano siguiente dentro del mismo
   layout → el icono aparece junto a los demás, mismo tamaño y estilo.
2. Teñir el icono clonado para distinguirlo (no requiere assets nuevos).
3. `onClick` → activa modo Ultra: `Game.OverrideSpeed = <valor>` y
   `SetSelected(true)` en el nuestro / `false` en los originales.
4. Postfix Harmony en `SetNormalSpeed` / `SetFastSpeed` / `SetSuperFastSpeed` /
   `EnableFastingForward` → si el jugador elige otra velocidad,
   `Game.OverrideSpeed = null` y deseleccionar el botón Ultra.
5. Postfix en `CycleSpeed` / `SpeedUpGame` para incluir Ultra en la rotación.
6. Tecla configurable (por defecto `4`).
7. Valor configurable en `BepInEx/config`.

Pausa y menús siguen funcionando: esos `return` ocurren **antes** del chequeo de
`OverrideSpeed` en `GetGameSpeed()`.
