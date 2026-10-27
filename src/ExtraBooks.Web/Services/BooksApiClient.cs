using System.Net.Http.Json;
using System.Net;
using System.Text.Json;
using System.Text.Json.Serialization;
using ExtraBooks.Contracts.Books;

namespace ExtraBooks.Web.Services;

public sealed class BooksApiClient
{
    private readonly HttpClient _http;
    private readonly JsonSerializerOptions _jsonOptions = new(JsonSerializerDefaults.Web);

    public BooksApiClient(HttpClient http)
    {
        _http = http;
        _jsonOptions.Converters.Add(new JsonStringEnumConverter());
    }

    public async Task<List<BookDto>> GetBooksAsync(string? search, string sort, BookStatus? status)
    {
        var url = $"api/books?search={Uri.EscapeDataString(search ?? string.Empty)}&sort={Uri.EscapeDataString(sort)}";
        if (status.HasValue) url += $"&status={status.Value}";
        using var response = await _http.GetAsync(url);
        await EnsureSuccessAsync(response);
        return await response.Content.ReadFromJsonAsync<List<BookDto>>(_jsonOptions) ?? new();
    }

    public async Task CreateAsync(SaveBookRequest book)
    {
        using var response = await _http.PostAsJsonAsync("api/books", book, _jsonOptions);
        await EnsureSuccessAsync(response);
    }

    public async Task UpdateAsync(int id, SaveBookRequest book)
    {
        using var response = await _http.PutAsJsonAsync($"api/books/{id}", book, _jsonOptions);
        await EnsureSuccessAsync(response);
    }

    public async Task DeleteAsync(int id)
    {
        using var response = await _http.DeleteAsync($"api/books/{id}");
        await EnsureSuccessAsync(response);
    }

    private static async Task EnsureSuccessAsync(HttpResponseMessage response)
    {
        if (response.IsSuccessStatusCode) return;

        var message = response.StatusCode switch
        {
            HttpStatusCode.BadRequest => "Controlla i dati inseriti.",
            HttpStatusCode.Unauthorized => "Accesso all'API non autorizzato. Controlla la configurazione della chiave.",
            HttpStatusCode.NotFound => "Il libro non esiste più. Aggiorna l'elenco.",
            _ => "Il servizio non è disponibile. Riprova più tardi."
        };

        if (response.StatusCode == HttpStatusCode.BadRequest &&
            response.Content.Headers.ContentType?.MediaType == "application/problem+json")
        {
            try
            {
                var problem = await response.Content.ReadFromJsonAsync<ValidationProblem>();
                if (problem?.Errors is { Count: > 0 })
                    message = string.Join(" ", problem.Errors.Values.SelectMany(errors => errors).Distinct());
            }
            catch (JsonException)
            {
                // Usa il messaggio di ripiego se la risposta non contiene JSON valido.
            }
        }

        throw new ApiException(message, response.StatusCode);
    }

    private sealed class ValidationProblem
    {
        public Dictionary<string, string[]>? Errors { get; set; }
    }
}
