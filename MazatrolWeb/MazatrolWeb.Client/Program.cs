using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using MazatrolWeb.Client;
using MazatrolWeb.Client.Services;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

builder.Services.AddScoped(_ => new HttpClient
{
    BaseAddress = new Uri(builder.HostEnvironment.BaseAddress)
});

builder.Services.AddScoped<MazatrolSessionState>();
builder.Services.AddScoped<StructureLoader>();
builder.Services.AddScoped<MazatrolAppService>();
builder.Services.AddScoped<ProgramEditorService>();
builder.Services.AddScoped<ThreeJsInterop>();
builder.Services.AddScoped<FileDownloadService>();
builder.Services.AddSingleton<IUnitHandler, MatUnitHandler>();
builder.Services.AddScoped<UnitHandlerRegistry>(sp =>
    new UnitHandlerRegistry(sp.GetServices<IUnitHandler>()));

await builder.Build().RunAsync();
