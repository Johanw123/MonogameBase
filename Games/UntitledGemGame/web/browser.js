// Saves remain on this origin across reloads. Keep the current JSON format.
window.btbStorage = {
    read() {
        try {
            const saves = JSON.parse(localStorage.getItem('beyond-the-belt.save') || '{}');
            return saves && typeof saves === 'object' && !Array.isArray(saves)
                ? Object.fromEntries(Object.entries(saves).filter(([, value]) => typeof value === 'string'))
                : {};
        } catch (error) {
            console.warn('Could not read browser saves.', error);
            return {};
        }
    },
    write(key, value) {
        try {
            const saves = this.read();
            if (value === null) delete saves[key];
            else saves[key] = value;
            localStorage.setItem('beyond-the-belt.save', JSON.stringify(saves));
            return true;
        } catch (error) {
            console.warn('Could not store browser saves.', error);
            return false;
        }
    }
};
window.btbShowError = message => {
    const error = document.getElementById('blazor-error-ui');
    error.textContent = message;
    error.style.display = 'block';
};
function flushProgress() {
    if (window.theInstance) window.theInstance.invokeMethod('PersistProgress');
}
window.addEventListener('beforeunload', flushProgress);
document.addEventListener('visibilitychange', () => {
    if (document.hidden) flushProgress();
});
// Web Audio requires a user gesture before playback. KNI creates its contexts
// during startup, so resume them on the first click or key press in the game.
window.btbFixMediaTime = () => {
    // nkast.Wasm.Dom 8.0.11 imports this result as an integer, while browser
    // currentTime has fractional seconds. Preserve playback; return whole ms.
    const getCurrentTime = nkMedia.GetCurrentTime;
    nkMedia.GetCurrentTime = function (...args) {
        return Math.trunc(getCurrentTime.apply(this, args));
    };
};
window.btbEnableAudio = () => {
    const contexts = new Set();
    for (const method of ['Create', 'Create1']) {
        const create = nkAudioContext[method];
        nkAudioContext[method] = function (...args) {
            const uid = create.apply(this, args);
            contexts.add(nkJSObject.GetObject(uid));
            return uid;
        };
    }
    function resume() {
        if (window.theInstance) window.theInstance.invokeMethod('EnableAudio');
        for (const context of contexts) {
            if (context.state === 'suspended') context.resume().catch(console.warn);
        }
    }
    document.addEventListener('pointerdown', resume);
    document.addEventListener('keydown', resume);
};

window.btbLoading = (message, progress) => {
    const panel = document.getElementById('game-loading');
    if (!panel) return;
    panel.style.display = message ? 'block' : 'none';
    if (!message) return;
    document.getElementById('game-loading-message').textContent = message;
    const bar = document.getElementById('game-loading-progress');
    if (progress > 0) bar.value = Math.min(1, progress);
    else bar.removeAttribute('value');
};
