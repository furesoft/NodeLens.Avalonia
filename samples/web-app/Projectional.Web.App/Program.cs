using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;

using Projectional.Web.App;
using Projectional.Web.App.Shell;
using Projectional.Web.App.Storage;

var builder = WebAssemblyHostBuilder.CreateDefault(args);

builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

// Synchronous localStorage: the workspace's storage seam is deliberately synchronous, and a browser is the one
// host where that is both possible and the right shape - a save is a string assignment, not a request.
builder.Services.AddSingleton<BrowserDocumentStorage>();
builder.Services.AddSingleton<EditorInterop>();

await builder.Build().RunAsync();
