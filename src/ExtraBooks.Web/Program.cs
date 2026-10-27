using ExtraBooks.Web.Components;
using ExtraBooks.Web.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

// Le chiamate all'API partono dal server Blazor.
var baseUrl = builder.Configuration["Api:BaseUrl"]
    ?? throw new InvalidOperationException("Api:BaseUrl non configurata.");
var apiKey = builder.Configuration["Api:Key"]
    ?? throw new InvalidOperationException("Api:Key non configurata.");

if (string.IsNullOrWhiteSpace(apiKey))
{
    throw new InvalidOperationException("Api:Key non configurata.");
}

builder.Services.AddHttpClient<BooksApiClient>(client =>
{
    client.BaseAddress = new Uri(baseUrl);
    client.DefaultRequestHeaders.Add("X-Api-Key", apiKey);
});

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/error");
}

app.UseStaticFiles();
app.UseAntiforgery();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
