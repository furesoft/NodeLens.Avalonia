import { dotnet } from './_framework/dotnet.js'

const is_browser = typeof window != "undefined";
if (!is_browser) throw new Error(`Expected to be running in a browser`);

const dotnetRuntime = await dotnet
    .withDiagnosticTracing(false)
    .withApplicationArgumentsFromQuery()
    .create();

// The boot screen is plain HTML; remove it once Avalonia has attached its canvas, rather than overlaying the shell.
const host = document.getElementById('out');
const observer = new MutationObserver(() => {
    if (host.querySelector('canvas')) {
        host.querySelectorAll('.pn-boot').forEach((element) => element.remove());
        observer.disconnect();
    }
});
observer.observe(host, { childList: true, subtree: true });

const config = dotnetRuntime.getConfig();

await dotnetRuntime.runMain(config.mainAssemblyName, [globalThis.location.href]);
