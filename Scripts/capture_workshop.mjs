/**
 * Run inside the Codex computer-use node_repl, after capture_workshop.py prepare.
 * Uses only the supported @oai/sky APIs for Windows UI actions. No desktop capture,
 * custom automation driver or screenshot synthesis is used.
 * Calibrated: Windows 150% display scaling, 1920x1200 game client, English 1.7 UI.
 * A changed capture size stops the script rather than clicking a different layout.
 * Actual screenshots are exported for the user-requested deliverables, not inspection.
 */
import fs from 'node:fs/promises';
import path from 'node:path';

const surface = [1283, 831];
const crop = [303, 167, 675, 525];
const tabs = [
  ['01-ui-tiny', 'UI: Tiny', 381],
  ['02-ui-small', 'UI: Small', 511],
  ['03-ui-medium', 'UI: Medium', 640],
  ['04-world', 'World', 768],
];
const delay = ms => new Promise(resolve => setTimeout(resolve, ms));

export async function run({sky, root, startAtSettings = false, report = () => {}}) {
  const work = path.join(root, '.task', 'workshop-screenshots');
  const session = JSON.parse(await fs.readFile(path.join(work, 'session.json'), 'utf8'));
  const expectedApp = 'process:' + path.join(session.game, 'RimWorld.exe');
  const windows = await sky.list_windows();
  const candidates = windows.filter(w => w.app.toLowerCase() === expectedApp.toLowerCase());
  if (candidates.length !== 1) throw Error(`Expected one screenshot game window, found ${candidates.length}. Close other RimWorld sessions.`);
  let window = await sky.get_window({id: candidates[0].id, app: candidates[0].app});
  let state;
  const captures = [];
  await fs.mkdir(path.join(work, 'raw'), {recursive: true});

  async function observe() {
    state = await sky.get_window_state({window});
    window = state.window;
    if (state.screenshots.length !== 1) throw Error('Unexpected additional screenshot surface or dialog. Review the game window.');
    const shot = state.screenshots[0];
    if (shot.width !== surface[0] || shot.height !== surface[1])
      throw Error(`Capture geometry changed: ${shot.width}x${shot.height}; expected ${surface.join('x')}. Recalibrate before continuing.`);
  }
  async function click(x, y) {
    if (!state) throw Error('Observe the selected window before input.');
    await sky.click({window: state.window, screenshotId: state.screenshots[0].id, x, y});
    await observe();
  }
  async function scroll(amount) {
    await sky.scroll({window: state.window, screenshotId: state.screenshots[0].id,
      x: 866, y: 545, scrollX: 0, scrollY: amount});
    await observe();
  }
  async function fontListScroll(amount) {
    await sky.scroll({window: state.window, screenshotId: state.screenshots[0].id,
      x: 560, y: 545, scrollX: 0, scrollY: amount});
    await observe();
  }
  async function capture(name, title, redactions = [], listScroll = null) {
    // Warm visible native-font previews; move the pointer outside the exported
    // settings region without clicking through or changing the selected controls.
    if (listScroll !== null) await fontListScroll(listScroll);
    await delay(2200);
    // A wrapped raster can increase row height after loading; anchor the final
    // viewport again so the selected first/last choice remains in the picture.
    if (listScroll !== null) await fontListScroll(listScroll);
    await sky.scroll({window: state.window, screenshotId: state.screenshots[0].id,
      x: 1100, y: 735, scrollX: 0, scrollY: 0});
    await observe();
    await delay(400);
    await observe();
    const shot = state.screenshots[0];
    if (!shot.url.startsWith('data:image/')) throw Error('Expected screenshot data URL.');
    await fs.writeFile(path.join(work, 'raw', name + '.png'), Buffer.from(shot.url.split(',')[1], 'base64'));
    captures.push({name, title, crop, redactions, capture_id: session.capture_id});
    await fs.writeFile(path.join(work, 'captures.json'), JSON.stringify(captures, null, 2));
    report(`Captured ${name}`);
  }

  await sky.activate_window({window});
  await observe();
  if (!startAtSettings) {
    // English main menu -> Options -> Mod options -> Custom Fonts.
    await click(983, 369);
    await click(468, 460);
    await click(625, 273);
  }
  for (const [name, title, x] of tabs) {
    await click(x, 265);
    await capture(name, title, [], name === '01-ui-tiny' || name === '04-world' ? 10000 : -10000);
  }
  await click(897, 265);
  await scroll(-10000);
  await capture('05-etc-preview-and-sources', 'etc: Preview and font sources', [{
    box: [10, 386, 649, 407], label: 'User font directory (profile path hidden)',
  }]);
  await scroll(10000);
  await capture('06-etc-font-packs', 'etc: Paths and external font packs', [{
    box: [10, 184, 649, 202], label: 'User font directory (profile path hidden)',
  }]);
  report('All tabs captured. Review raw states and run capture_workshop.py finish to export settings-window images.');
  return captures;
}
