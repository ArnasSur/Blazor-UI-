using ApexCharts;
using Microsoft.AspNetCore.Http.Connections;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.DependencyInjection;
using ProfileUI.Components;
using ProfileUI.ProfileEditor;
using System.Reflection;

var assemblyLocation = Assembly.GetExecutingAssembly().Location;
var directory = Path.GetDirectoryName(assemblyLocation)
    ?? throw new InvalidOperationException("Cannot determine executable directory.");

var linkFile = Path.Combine(directory, "link.txt");
if (!File.Exists(linkFile))
    throw new FileNotFoundException("Missing link.txt", linkFile);

var url = File.ReadLines(linkFile).First().Trim();

if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
    throw new InvalidOperationException($"Invalid URL in link.txt: {url}");

var port = uri.Port;

var builder = WebApplication.CreateBuilder(args);

//builder.WebHost.UseUrls(line.First());
builder.WebHost.ConfigureKestrel(options =>
{
    options.ListenAnyIP(port);
});

// Add services to the container.
builder.Services.AddRazorComponents().AddInteractiveServerComponents();
builder.Services.AddBlazorBootstrap();
builder.Services.AddSignalR(options =>
{
    options.EnableDetailedErrors = true;
});
builder.Services.Configure<Microsoft.AspNetCore.Http.Connections.HttpConnectionDispatcherOptions>(options =>
{
    options.Transports =
        HttpTransportType.LongPolling |
        HttpTransportType.ServerSentEvents;
});
builder.Services.AddScoped<ServerDB>();
builder.Services.AddScoped<ProfileCore>();
builder.Services.AddScoped<FeedingProfile>();

var app = builder.Build();

app.Use(async (context, next) =>
{
    // Leisti iframe
    context.Response.Headers.Remove("X-Frame-Options");
    context.Response.Headers["Content-Security-Policy"] = "frame-ancestors *";

    await next();
});


app.UseStaticFiles();
app.UseAntiforgery();
app.MapRazorComponents<App>().AddInteractiveServerRenderMode();
app.Run();
