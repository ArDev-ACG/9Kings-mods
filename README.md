# 9 Kings — MOD 1: Ultra Speed

Añade **velocidades extra** (por defecto **4x, 6x y 8x**), cada una con su propio icono
en la misma fila que Pausa / Auto / 1x / 2x / 3x, y las **mantiene entre oleadas** igual
que el juego hace con 1x/2x/3x.

## Estado

| Paso | Estado |
|---|---|
| Investigación del motor y del sistema de velocidad | ✅ ver `research/00-INVESTIGACION.md` |
| BepInEx 6 IL2CPP instalado en el juego | ✅ 6.0.755 |
| Assemblies interop generados | ✅ 138 dlls |
| Plugin compilado | ✅ `NineKings.UltraSpeed.dll` |
| Plugin cargado por BepInEx | ✅ verificado en `BepInEx/LogOutput.log` |
| Botón creado en runtime junto a SuperFast | ✅ verificado en el log |
| Clic/tecla → velocidad real en batalla | ✅ confirmado por el usuario (v1.0, 6x) |
| 3 botones (4x/6x/8x) + persistencia entre oleadas | ✅ v1.1 confirmado por el usuario |
| Botón de auto-disparo tapado por la fila | ✅ corregido en 1.1.4 |
| Paquete para Nexus | ✅ `dist/NineKings-UltraSpeed-1.1.4.zip` |
| Paquete para Thunderstore | ✅ `dist/ExtraGameSpeeds-1.1.4-thunderstore.zip` |
| Extensión de Vortex | ⏳ `dist/vortex-extension/`, instalada, falta verificar en la UI |

## Estructura

```
9Kings/
├─ README.md                  este archivo
├─ research/                  notas de ingeniería inversa
├─ tools/                     Cpp2IL, Il2CppDumper, BepInExPack
├─ dump/                      volcados del juego (dummydll, cs, isil, interop_cs)
└─ src/UltraSpeed/            código fuente del mod
```

## Cómo funciona

`Core.Game.GetGameSpeed()` consulta `Core.Game.OverrideSpeed` **después** de resolver
pausa y menús, y **antes** del `switch` sobre `FastingForward`. El mod escribe ese campo,
que es el mismo que usa el comando de consola interno `SetSpeed` del propio juego
(con tope 6.0). Por eso la pausa y los menús siguen funcionando con Ultra activo.

El botón se crea clonando `m_SuperFastControl` y colocándolo como hermano siguiente
dentro del mismo layout, así que hereda tamaño, estilo y animaciones. Se le apaga el
listener persistente heredado del prefab y se le engancha el nuestro.

### Parches (HarmonyX)

| Método | Tipo | Qué hace |
|---|---|---|
| `GameplayUI_ChangeSpeedView.Update` | postfix | crea los botones si faltan y atiende los atajos |
| `EnableFastingForward` | postfix | cualquier cambio de velocidad del juego apaga el override |
| `SetSpeedAsPreferred` | pre + post | `Wave.StartWave()` pasa por aquí; se reaplica la velocidad extra preferida |
| `ApplyInitialState` | pre + post | ídem al reabrirse la barra de velocidad |
| `SetNormalSpeed` / `SetFastSpeed` / `SetSuperFastSpeed` | postfix | si lo eligió el jugador, se olvida la preferencia extra |
| `SpeedUpGame` | prefix | 3x → 4x → 6x → 8x |
| `SlowDownGame` | prefix | 8x → 6x → 4x → 3x |
| `CycleSpeed` | prefix | 3x → 4x → 6x → 8x → 1x |

## Uso

- Clic en uno de los iconos nuevos, o teclas **4** / **5** / **6** (4x / 6x / 8x).
- Volver a pulsar el mismo baja a 3x; elegir 1x/2x/3x también desactiva.
- La velocidad elegida se conserva al empezar la siguiente oleada.

## Configuración

`<juego>/BepInEx/config/com.mods.ninekings.ultraspeed.cfg` (se crea al primer arranque):

| Clave | Def. | Descripción |
|---|---|---|
| `Speeds` | `4,6,8` | Velocidades extra separadas por coma. Una crea un botón. Se ordenan solas |
| `Hotkeys` | `Digit4,Digit5,Digit6` | Un atajo por velocidad, mismo orden. Nombres del enum `UnityEngine.InputSystem.Key`. Vacío = sin atajos |
| `IconTints` | `#FFD24A,#FF8A3D,#FF3D3D` | Color por botón, mismo orden; se reutilizan en ciclo si faltan. Vacío = sin teñir |
| `IncludeInCycle` | `true` | Incluir las nuevas en subir/bajar/ciclar velocidad |
| `Persist` | `true` | Conservar la velocidad elegida entre oleadas |

## Compilar

Requiere .NET SDK 8 y que el juego se haya arrancado **una vez** con BepInEx
(genera `BepInEx/interop`, de donde salen las referencias).

```powershell
dotnet build src\UltraSpeed\UltraSpeed.csproj -c Release
copy src\UltraSpeed\bin\Release\net6.0\NineKings.UltraSpeed.dll `
     "C:\Program Files (x86)\Steam\steamapps\common\9 Kings\BepInEx\plugins\NineKings.UltraSpeed\"
```

Si el juego está en otra ruta: `dotnet build ... -p:GameDir="D:\ruta\9 Kings"`.

## Desinstalar

- Sólo el mod: borrar `BepInEx/plugins/NineKings.UltraSpeed/`.
- Todo BepInEx: borrar del directorio del juego `winhttp.dll`, `doorstop_config.ini`,
  `.doorstop_version`, `changelog.txt`, y las carpetas `BepInEx/` y `dotnet/`.
  No se ha modificado ningún archivo original del juego.

## Avisos

- El juego se actualiza a menudo. Si un parche cambia `GameplayUI_ChangeSpeedView` o
  `Game.OverrideSpeed`, hay que regenerar `BepInEx/interop` (borrar la carpeta y
  arrancar el juego) y recompilar.
- **Sobre el 8x**: el tope de `6.0` sólo existe como validación de entrada del comando
  de consola `SetSpeed`. `Game.OverrideSpeed` no se recorta en ningún sitio —
  `Game.LateUpdate` y `Game.UpdateSpeed` escriben `Time.timeScale` sin clamp y el juego
  no toca `fixedDeltaTime`. Por eso 8x funciona. El coste es que cada frame avanza más
  simulación de golpe, así que a FPS bajos puede notarse impreciso.

## Licencia

Todos los derechos reservados — ver [LICENSE](LICENSE). Sin relación con Sad Socket.
