// The browser half of the sample: the three things a Blazor component cannot do on its own.
//
// 1. measure the font the page actually chose, so the painted spans and the layout agree;
// 2. swallow a key synchronously (preventDefault has to happen during the event, not after a .NET round trip);
// 3. write a file to the user's downloads.

let attached = null;

function chordOf(event) {
    const key = event.key === ' '
        ? 'Space'
        : event.key.length === 1
            ? event.key.toLowerCase()
            : event.key;

    return (event.ctrlKey ? 'ctrl+' : '')
        + (event.shiftKey ? 'shift+' : '')
        + (event.altKey ? 'alt+' : '')
        + key;
}

// One character column and one line, measured under the editor's own computed font.
export function measure(reference) {
    // The canvas is where the monospace font lives; the host around it carries the shell's UI font. Measuring the
    // wrong one would place every run at a width the glyphs do not have, so prefer the canvas when it exists.
    const canvas = reference?.querySelector?.('.pn-canvas');
    const style = getComputedStyle(canvas ?? reference);
    const probe = document.createElement('span');

    probe.style.position = 'absolute';
    probe.style.visibility = 'hidden';
    probe.style.whiteSpace = 'pre';
    probe.style.fontFamily = style.fontFamily;
    probe.style.fontSize = style.fontSize;
    probe.style.fontWeight = style.fontWeight;
    probe.style.lineHeight = style.lineHeight;
    probe.textContent = '0'.repeat(100);

    document.body.appendChild(probe);

    const rect = probe.getBoundingClientRect();
    probe.remove();

    return [rect.width / 100, rect.height];
}

export function attach(composer, dotNetRef, chords) {
    detach();

    const handled = new Set(chords);
    let composing = false;

    const onKeyDown = (event) => {
        if (event.isComposing || composing) {
            return;
        }

        // Meta combinations belong to the operating system or the browser, never to the editor.
        if (event.metaKey) {
            return;
        }

        if (event.key === 'Dead' || event.key === 'Process' || event.key === 'Unidentified') {
            return;
        }

        // A printable character is not a key meaning: the text area turns it into an input event, which is the
        // path that also carries input methods, dead keys and pasted text.
        const printable = event.key.length === 1 && !event.ctrlKey && !event.altKey && !event.metaKey;

        if (printable) {
            return;
        }

        if (!handled.has(chordOf(event))) {
            return;
        }

        event.preventDefault();

        dotNetRef.invokeMethodAsync(
            'OnKeyDown',
            event.key,
            event.ctrlKey,
            event.shiftKey,
            event.altKey);
    };

    const onInput = () => {
        if (composing) {
            return;
        }

        const text = composer.value;

        if (!text) {
            return;
        }

        composer.value = '';
        dotNetRef.invokeMethodAsync('OnTextInput', text);
    };

    const onCompositionStart = () => {
        composing = true;
    };

    const onCompositionEnd = (event) => {
        composing = false;

        const text = event.data ?? composer.value;
        composer.value = '';

        if (text) {
            dotNetRef.invokeMethodAsync('OnTextInput', text);
        }
    };

    // A native paste is the only way to see another application's clipboard without asking for a permission, and
    // preventing it keeps the text out of the hidden text area - the model gets it through the editor's own action.
    const onPaste = (event) => {
        event.preventDefault();

        const text = event.clipboardData?.getData('text/plain') ?? '';
        dotNetRef.invokeMethodAsync('OnPaste', text);
    };

    composer.addEventListener('keydown', onKeyDown);
    composer.addEventListener('input', onInput);
    composer.addEventListener('compositionstart', onCompositionStart);
    composer.addEventListener('compositionend', onCompositionEnd);
    composer.addEventListener('paste', onPaste);

    attached = { composer, onKeyDown, onInput, onCompositionStart, onCompositionEnd, onPaste };
}

export function detach() {
    if (!attached) {
        return;
    }

    const current = attached;

    current.composer.removeEventListener('keydown', current.onKeyDown);
    current.composer.removeEventListener('input', current.onInput);
    current.composer.removeEventListener('compositionstart', current.onCompositionStart);
    current.composer.removeEventListener('compositionend', current.onCompositionEnd);
    current.composer.removeEventListener('paste', current.onPaste);

    attached = null;
}

export function focusComposer(composer) {
    if (!composer) {
        return;
    }

    // After the event's default action, not during it: the browser moves the focus to the clicked, non-focusable
    // element as part of the pointer default, so focusing synchronously would be undone one step later.
    setTimeout(() => composer.focus({ preventScroll: true }), 0);
}

// Scroll only as far as the caret needs, so a document does not jump when a character is typed at its edge.
export function scrollCaretIntoView(host) {
    if (!host) {
        return;
    }

    const caret = host.querySelector('[data-role="caret"]');

    if (!caret) {
        return;
    }

    const scroll = host.closest('.pn-scroll') ?? host;
    const box = caret.getBoundingClientRect();
    const view = scroll.getBoundingClientRect();

    if (box.top < view.top) {
        scroll.scrollTop -= view.top - box.top + 8;
    } else if (box.bottom > view.bottom) {
        scroll.scrollTop += box.bottom - view.bottom + 8;
    }

    if (box.left < view.left) {
        scroll.scrollLeft -= view.left - box.left + 8;
    } else if (box.right > view.right) {
        scroll.scrollLeft += box.right - view.right + 8;
    }
}

export function download(name, text) {
    const blob = new Blob([text], { type: 'application/xml;charset=utf-8' });
    const url = URL.createObjectURL(blob);
    const anchor = document.createElement('a');

    anchor.href = url;
    anchor.download = name;

    document.body.appendChild(anchor);
    anchor.click();
    anchor.remove();

    URL.revokeObjectURL(url);
}

// Copying out of the editor has to happen inside the keyboard gesture, and the async clipboard API may ask for a
// permission or refuse a non-secure context. A selected off-screen text area plus execCommand is the synchronous
// path every browser still honours; the modern API is the fallback.
export function writeClipboard(text) {
    const area = document.createElement('textarea');

    area.value = text;
    area.setAttribute('readonly', '');
    area.style.position = 'fixed';
    area.style.top = '0';
    area.style.left = '-1000px';

    document.body.appendChild(area);
    area.select();

    let copied = false;

    try {
        copied = document.execCommand('copy');
    } catch {
        copied = false;
    }

    area.remove();

    if (!copied && navigator.clipboard?.writeText) {
        navigator.clipboard.writeText(text).catch(() => { });
    }
}
