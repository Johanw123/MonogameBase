const fs = require('fs'), vm = require('vm'), assert = require('assert'), path = require('path'), os = require('os');
const storage = new Map(), events = {}, contexts = new Map();
const context = {
 window: { addEventListener: (event, fn) => events[event] = fn },
 document: { addEventListener: (event, fn) => events[event] = fn },
 localStorage: { getItem: key => storage.get(key) ?? null, setItem: (key, value) => storage.set(key, value) },
 console: { ...console, warn() {} },
 nkAudioContext: { Create() { return 1; }, Create1() { return 2; } },
 nkJSObject: { GetObject: uid => contexts.get(uid) }
};
vm.createContext(context);
// Load the actual dependency script so the regression exercises its fractional
// time result and registered event callback, rather than a substitute.
const packages = process.env.NUGET_PACKAGES || path.join(os.homedir(), '.nuget/packages');
vm.runInContext(fs.readFileSync(path.join(packages, 'nkast.wasm.dom/8.0.11/staticwebassets/js/Media.8.0.11.js'), 'utf8'), context);
context.nkMedia = context.window.nkMedia;
let mediaUpdates = 0;
context.DotNet = { invokeMethod(assembly, method, uid) {
    assert.strictEqual(method, 'JsMediaOnOnTimeUpdate');
    assert(Number.isInteger(context.nkMedia.GetCurrentTime(uid)), 'Media time must cross integer interop as whole milliseconds');
    mediaUpdates++;
} };
const media = { currentTime: 0.019791 };
contexts.set(3, media);
context.nkMedia.RegisterEvents(3);
assert.throws(() => media.ontimeupdate(), /whole milliseconds/);
vm.runInContext(fs.readFileSync('web/browser.js', 'utf8'), context);
context.window.btbFixMediaTime();
for (const seconds of [0.019791, 9.587375, 9.868937, 10.144562, 0]) {
    media.currentTime = seconds;
    media.ontimeupdate();
    assert.strictEqual(context.nkMedia.GetCurrentTime(3), Math.trunc(seconds * 1000));
}
assert.strictEqual(mediaUpdates, 5);
const api = context.window.btbStorage;
assert.strictEqual(Object.keys(api.read()).length, 0);
assert(api.write('progress', '{"Version":1}'));
assert.strictEqual(api.read().progress, '{"Version":1}');
assert(api.write('settings', '{"language":"ja"}'));
assert.strictEqual(api.read().progress, '{"Version":1}');
assert(api.write('progress', null));
assert.strictEqual(api.read().progress, undefined);
storage.set('beyond-the-belt.save', 'null');
assert.strictEqual(Object.keys(api.read()).length, 0);
storage.set('beyond-the-belt.save', '"bad"');
assert.strictEqual(Object.keys(api.read()).length, 0);
storage.set('beyond-the-belt.save', '{');
assert.strictEqual(Object.keys(api.read()).length, 0);
context.localStorage.setItem = () => { throw new Error('quota'); };
assert.strictEqual(api.write('progress', 'retry'), false);
let resumes = 0;
contexts.set(1, { state: 'suspended', resume() { resumes++; return Promise.resolve(); } });
contexts.set(2, { state: 'running', resume() { throw new Error('unexpected resume'); } });
context.window.btbEnableAudio();
context.nkAudioContext.Create(); context.nkAudioContext.Create1();
events.pointerdown();
assert.strictEqual(resumes, 1);
let flushes = 0;
context.window.theInstance = { invokeMethod(method) { assert.strictEqual(method, 'PersistProgress'); flushes++; } };
events.beforeunload(); context.document.hidden = true; events.visibilitychange();
assert.strictEqual(flushes, 2);
console.log('Browser JavaScript checks passed: real media time callbacks, storage, malformed data, quota failure, audio activation and lifecycle flush.');

const loading = { style: {} }, label = {}, bar = { removeAttribute(name) { delete this[name]; } };
context.document.getElementById = id => ({ 'game-loading': loading, 'game-loading-message': label, 'game-loading-progress': bar })[id];
context.window.btbLoading('Downloading game assets…', 0.25);
assert.strictEqual(loading.style.display, 'block');
assert.strictEqual(label.textContent, 'Downloading game assets…');
assert.strictEqual(bar.value, 0.25);
context.window.btbLoading('Preparing upgrades…', 0);
assert(!('value' in bar));
context.window.btbLoading(null, 0);
assert.strictEqual(loading.style.display, 'none');
console.log('Browser loading progress checks passed.');
