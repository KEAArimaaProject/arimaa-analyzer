// Optional module path (HotkeyHost primarily uses index.html + __arimaaHotkeysSetRef).

export function connect(dotnet) {
    window.arimaaHotkeys = window.arimaaHotkeys || { ref: null, bound: false };
    window.arimaaHotkeys.ref = dotnet;
}

export function disconnect() {
    if (window.arimaaHotkeys) {
        window.arimaaHotkeys.ref = null;
    }
}