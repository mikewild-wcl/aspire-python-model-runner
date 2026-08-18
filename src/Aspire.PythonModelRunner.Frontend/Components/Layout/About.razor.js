let handler = null;

export function registerShortcut(dotNetRef) {
    unregisterShortcut();

    handler = (event) => {
        if (event.shiftKey && !event.ctrlKey && !event.altKey && !event.metaKey
            && !event.repeat && (event.code === 'KeyA' || event.key === 'A' || event.key === 'a')) {
            event.preventDefault();
            dotNetRef.invokeMethodAsync('OpenFromShortcut');
        }
    };

    window.addEventListener('keydown', handler);
}

export function unregisterShortcut() {
    if (handler) {
        window.removeEventListener('keydown', handler);
        handler = null;
    }
}
