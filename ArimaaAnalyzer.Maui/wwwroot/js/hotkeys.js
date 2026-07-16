// ES module for global keyboard shortcuts. Loaded via Blazor "import" from HotkeyHost.

let dotnetRef = null;
let boundHandler = null;

function isEditableTarget(target) {
    if (!target) return false;
    const el = target;
    const tag = el.tagName ? el.tagName.toLowerCase() : '';
    if (tag === 'input' || tag === 'textarea' || tag === 'select') return true;
    if (el.isContentEditable) return true;
    return !!el.closest('[contenteditable="true"]');
}

export function connect(dotnet) {
    disconnect();
    dotnetRef = dotnet;
    boundHandler = (e) => {
        if (e.defaultPrevented || e.repeat) return;
        if (e.ctrlKey || e.altKey || e.metaKey) return;
        if (isEditableTarget(e.target)) return;

        const key = e.key;
        if (!key || key.length !== 1) return;

        try {
            dotnetRef.invokeMethodAsync('OnKeyDown', key.toLowerCase());
        } catch {
            /* host disconnected */
        }
    };

    document.addEventListener('keydown', boundHandler, true);
}

export function disconnect() {
    if (boundHandler) {
        document.removeEventListener('keydown', boundHandler, true);
        boundHandler = null;
    }
    dotnetRef = null;
}