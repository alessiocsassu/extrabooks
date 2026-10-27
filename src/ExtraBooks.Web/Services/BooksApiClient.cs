using System.Net.Http.Json;
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

    public async Task<List<BookDto>> GetBooksAsync(string? search, string sort)
    {
        var url = $"api/books?search={Uri.EscapeDataString(search ?? string.Empty)}&sort={Uri.EscapeDataString(sort)}";
        return await _http.GetFromJsonAsync<List<BookDto>>(url, _jsonOptions) ?? new();
    }

    public async Task CreateAsync(SaveBookRequest book)
    {
        using var response = await _http.PostAsJsonAsync("api/books", book, _jsonOptions);
        response.EnsureSuccessStatusCode();
    }

    public async Task UpdateAsync(int id, SaveBookRequest book)
    {
        using var response = await _http.PutAsJsonAsync($"api/books/{id}", book, _jsonOptions);
        response.EnsureSuccessStatusCode();
    }

    public async Task DeleteAsync(int id)
    {
        using var response = await _http.DeleteAsync($"api/books/{id}");
        response.EnsureSuccessStatusCode();
    }
}
