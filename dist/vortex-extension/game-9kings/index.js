const path = require('path');
const { fs, log, util } = require('vortex-api');

const GAME_ID = '9kings';
const STEAM_APP_ID = '2784470';
const GAME_EXE = '9Kings.exe';

// 9 Kings corre sobre Unity 6 con IL2CPP (metadata v39). Solo BepInEx 6 bleeding edge
// build 755 o superior sabe leerla; las anteriores fallan al generar los interop.
const BEPINEX_MIN_BUILD = 755;
const BEPINEX_URL = 'https://thunderstore.io/c/9-kings/p/BepInEx/BepInExPack_IL2CPP/';

function findGame() {
  return util.GameStoreHelper.findByAppId([STEAM_APP_ID]).then(game => game.gamePath);
}

// Los mods se despliegan desde la raiz del juego: los archivos de 9 Kings traen ya la
// ruta BepInEx/plugins/... dentro, igual que en Thunderstore.
function queryModPath() {
  return '.';
}

function makeSetup(api) {
  return (discovery) => {
    const pluginsDir = path.join(discovery.path, 'BepInEx', 'plugins');
    const doorstop = path.join(discovery.path, 'winhttp.dll');

    return fs.ensureDirWritableAsync(pluginsDir)
      .then(() => fs.statAsync(doorstop).then(() => undefined).catch(() => {
        // Deliberadamente no se descarga BepInEx de forma automatica: el instalador
        // generico sirve builds mas antiguas que la 755 y esas rompen el juego.
        api.showDialog('info', 'BepInEx required', {
          text: '9 Kings needs BepInEx 6 for IL2CPP, build ' + BEPINEX_MIN_BUILD + ' or newer. '
              + 'Older builds cannot read this game IL2CPP metadata and will fail.\n\n'
              + 'Install it, launch the game once and let it finish generating its files, '
              + 'then deploy your mods.',
        }, [
          { label: 'Get BepInEx', action: () => util.opn(BEPINEX_URL).catch(() => undefined) },
          { label: 'Close' },
        ]);
      }));
  };
}

function testSupported(files, gameId) {
  return Promise.resolve({
    supported: gameId === GAME_ID,
    requiredFiles: [],
  });
}

// Acepta las dos formas en que se empaquetan los mods de este juego: con el arbol
// BepInEx/ dentro del archivo, o sueltos como .dll en la raiz.
function install(files, destinationPath) {
  const filtered = files.filter(f => !f.endsWith(path.sep));
  const hasTree = filtered.some(f => f.toLowerCase().split(path.sep).indexOf('bepinex') === 0);

  if (hasTree) {
    return Promise.resolve({
      instructions: filtered.map(source => ({ type: 'copy', source, destination: source })),
    });
  }

  const modName = path.basename(destinationPath, '.installing');
  const prefix = path.join('BepInEx', 'plugins', modName);

  return Promise.resolve({
    instructions: filtered.map(source => ({
      type: 'copy',
      source,
      destination: path.join(prefix, path.basename(source)),
    })),
  });
}

function main(context) {
  context.registerGame({
    id: GAME_ID,
    name: '9 Kings',
    mergeMods: true,
    queryPath: findGame,
    queryModPath,
    logo: 'gameart.jpg',
    executable: () => GAME_EXE,
    requiredFiles: [GAME_EXE, 'GameAssembly.dll'],
    setup: makeSetup(context.api),
    environment: { SteamAPPId: STEAM_APP_ID },
    details: { steamAppId: parseInt(STEAM_APP_ID, 10) },
  });

  context.registerInstaller('9kings-bepinex-plugin', 25, testSupported, install);

  return true;
}

module.exports = { default: main };
