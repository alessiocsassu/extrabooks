using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using ExtraBooks.Contracts.Books;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace ExtraBooks.Tests;

// L'esecuzione completa usa il database temporaneo del profilo Docker "tests".
public sealed class MySqlFactAttribute : FactAttribute
{
    public MySqlFactAttribute()
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("TEST_MYSQL_CONNECTION_STRING")))
            Skip = "Richiede TEST_MYSQL_CONNECTION_STRING. Usare il profilo Docker tests.";
    }
}

public sealed class BooksApiTests
{
    private const string ApiKey = "integration-test-key";
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    private sealed class ApiFactory : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Testing");
            builder.UseSetting("ConnectionStrings:Default",
                Environment.GetEnvironmentVariable("TEST_MYSQL_CONNECTION_STRING"));
            builder.UseSetting("ApiKey", ApiKey);
        }
    }

    [MySqlFact]
    public async Task Authentication_RejectsMissingAndWrongKeys_ButAllowsHealth()
    {
        await using var factory = new ApiFactory();
        using var client = factory.CreateClient();
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/health")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/books")).StatusCode);
        client.DefaultRequestHeaders.Add("X-Api-Key", "wrong");
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/books")).StatusCode);
    }

    [MySqlFact]
    public async Task Crud_SearchAndSort_PersistChangesInMySql()
    {
        await using var factory = new ApiFactory();
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Api-Key", ApiKey);
        var marker = Guid.NewGuid().ToString("N");
        var ids = new List<int>();
        try
        {
            var first = await CreateAsync(client, new SaveBookRequest
            {
                Title = $"  Zeta {marker}  ", Author = "  Autore  ",
                Status = BookStatus.Reading, CurrentPage = 20, TotalPages = 200,
                Rating = 4, Notes = "  Note  "
            });
            ids.Add(first.Id);
            Assert.Equal($"Zeta {marker}", first.Title);
            Assert.Equal("Autore", first.Author);
            Assert.Equal("Note", first.Notes);
            Assert.Equal(BookStatus.Reading, first.Status);
            Assert.Equal(20, first.CurrentPage);
            Assert.Equal(200, first.TotalPages);
            Assert.Equal(4, first.Rating);

            var second = await CreateAsync(client, new SaveBookRequest
            {
                Title = $"Alfa {marker}", Author = $"Autore {marker}"
            });
            ids.Add(second.Id);
            var sorted = await client.GetFromJsonAsync<List<BookDto>>($"/api/books?search=%20{marker}%20&sort=title", Json);
            Assert.Equal(new[] { second.Id, first.Id }, sorted!.Select(b => b.Id));
            var recent = await client.GetFromJsonAsync<List<BookDto>>($"/api/books?search={marker}&sort=recent", Json);
            Assert.Equal(new[] { second.Id, first.Id }, recent!.Select(b => b.Id));
            var author = await client.GetFromJsonAsync<List<BookDto>>($"/api/books?search=Autore%20{marker}", Json);
            Assert.Equal(second.Id, Assert.Single(author!).Id);

            var update = new SaveBookRequest
            {
                Title = first.Title, Author = first.Author, Status = BookStatus.Finished,
                CurrentPage = 200, TotalPages = 200, Rating = 5,
                Notes = "   ", FinishedOn = new DateTime(2026, 10, 27)
            };
            using var response = await client.PutAsJsonAsync($"/api/books/{first.Id}", update, Json);
            Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
            var saved = await client.GetFromJsonAsync<BookDto>($"/api/books/{first.Id}", Json);
            Assert.Equal(BookStatus.Finished, saved!.Status);
            Assert.Equal(200, saved.CurrentPage);
            Assert.Equal(5, saved.Rating);
            Assert.Equal(update.FinishedOn, saved.FinishedOn);
            Assert.Null(saved.Notes);

            Assert.Equal(HttpStatusCode.NoContent, (await client.DeleteAsync($"/api/books/{first.Id}")).StatusCode);
            Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"/api/books/{first.Id}")).StatusCode);
            Assert.Equal(HttpStatusCode.NotFound, (await client.DeleteAsync($"/api/books/{first.Id}")).StatusCode);
            Assert.Equal(HttpStatusCode.NotFound, (await client.PutAsJsonAsync($"/api/books/{first.Id}", update, Json)).StatusCode);
        }
        finally
        {
            foreach (var id in ids) await client.DeleteAsync($"/api/books/{id}");
        }
    }

    [MySqlFact]
    public async Task InvalidRequests_ReturnBadRequest_AndDoNotCreateBooks()
    {
        await using var factory = new ApiFactory();
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Api-Key", ApiKey);
        var marker = Guid.NewGuid().ToString("N");
        foreach (var request in new[]
        {
            new SaveBookRequest { Title = marker, Author = " " },
            new SaveBookRequest { Title = marker, Author = "Autore", CurrentPage = 11, TotalPages = 10 },
            new SaveBookRequest { Title = marker, Author = "Autore", Status = (BookStatus)99 },
            new SaveBookRequest { Title = marker, Author = "Autore", Rating = 6 }
        })
        {
            using var response = await client.PostAsJsonAsync("/api/books", request, Json);
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            var problem = await response.Content.ReadFromJsonAsync<JsonElement>();
            Assert.True(problem.TryGetProperty("errors", out _));
        }
        using var malformed = await client.PostAsync("/api/books", new StringContent("{", System.Text.Encoding.UTF8, "application/json"));
        Assert.Equal(HttpStatusCode.BadRequest, malformed.StatusCode);
        var books = await client.GetFromJsonAsync<List<BookDto>>($"/api/books?search={marker}", Json);
        Assert.Empty(books!);
    }

    [MySqlFact]
    public async Task StatusFilter_CombinesWithSearch_AndSortingHandlesMissingRatings()
    {
        await using var factory = new ApiFactory();
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Api-Key", ApiKey);
        var marker = Guid.NewGuid().ToString("N");
        var books = new List<BookDto>();
        try
        {
            foreach (var (title, status, rating) in new (string, BookStatus, int?)[]
            {
                ("Zeta", BookStatus.ToRead, null),
                ("Alfa", BookStatus.Reading, 5),
                ("Beta", BookStatus.Finished, 1),
                ("Gamma", BookStatus.Reading, 5),
                ("Delta", BookStatus.Finished, null)
            })
            {
                books.Add(await CreateAsync(client, new SaveBookRequest
                {
                    Title = $"{title} {marker}", Author = $"Autore {marker}", Status = status, Rating = rating
                }));
            }

            foreach (var (sort, expected) in new[]
            {
                ("status", new[] { 0, 1, 3, 2, 4 }),
                ("status-desc", new[] { 2, 4, 1, 3, 0 }),
                ("rating", new[] { 1, 3, 2, 4, 0 }),
                ("rating-asc", new[] { 2, 1, 3, 4, 0 })
            })
            {
                var result = await client.GetFromJsonAsync<List<BookDto>>($"/api/books?search={marker}&sort={sort}", Json);
                Assert.Equal(expected.Select(i => books[i].Id), result!.Select(b => b.Id));
            }

            foreach (var status in Enum.GetValues<BookStatus>())
            {
                var result = await client.GetFromJsonAsync<List<BookDto>>($"/api/books?search=Autore%20{marker}&status={status}&sort=title", Json);
                Assert.Equal(books.Where(b => b.Status == status).OrderBy(b => b.Title).Select(b => b.Id), result!.Select(b => b.Id));
            }
            var combined = await client.GetFromJsonAsync<List<BookDto>>($"/api/books?search=Alfa%20{marker}&status=Reading&sort=rating", Json);
            Assert.Equal(books[1].Id, Assert.Single(combined!).Id);
            var empty = await client.GetFromJsonAsync<List<BookDto>>($"/api/books?search=Alfa%20{marker}&status=Finished", Json);
            Assert.Empty(empty!);
            foreach (var invalid in new[] { "99", "unknown" })
                Assert.Equal(HttpStatusCode.BadRequest, (await client.GetAsync($"/api/books?status={invalid}")).StatusCode);
        }
        finally
        {
            foreach (var book in books) await client.DeleteAsync($"/api/books/{book.Id}");
        }
    }

    private static async Task<BookDto> CreateAsync(HttpClient client, SaveBookRequest request)
    {
        using var response = await client.PostAsJsonAsync("/api/books", request, Json);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var book = await response.Content.ReadFromJsonAsync<BookDto>(Json);
        Assert.NotNull(book);
        Assert.EndsWith($"/api/books/{book.Id}", response.Headers.Location!.ToString());
        return book;
    }
}
