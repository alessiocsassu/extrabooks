using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using ExtraBooks.Api.Data;
using ExtraBooks.Contracts.Books;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Testcontainers.MySql;
using Xunit;

namespace ExtraBooks.MySqlTests;

public sealed class MySqlFixture : IAsyncLifetime
{
    public MySqlContainer Container { get; } = new MySqlBuilder()
        .WithImage("mysql:8.4")
        .WithDatabase("extrabooks_tests")
        .Build();

    public Task InitializeAsync() => Container.StartAsync();
    public Task DisposeAsync() => Container.DisposeAsync().AsTask();

    public BooksDbContext CreateContext() => new(new DbContextOptionsBuilder<BooksDbContext>()
        .UseMySql(Container.GetConnectionString(), new MySqlServerVersion(new Version(8, 4, 0))).Options);

    public async Task ResetAsync()
    {
        await using var db = CreateContext();
        // La connessione proviene soltanto dal container creato dalla fixture.
        await db.Database.EnsureDeletedAsync();
    }
}

public sealed class MySqlDatabaseTests(MySqlFixture fixture) : IClassFixture<MySqlFixture>
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    [Fact]
    public async Task Migrations_CreateSchema_AndAreIdempotent()
    {
        await fixture.ResetAsync();
        await using var db = fixture.CreateContext();
        Assert.False(db.Database.HasPendingModelChanges());
        await db.Database.MigrateAsync();
        db.Books.Add(new Book { Title = "Dune", Author = "Frank Herbert" });
        await db.SaveChangesAsync();
        await db.Database.MigrateAsync();
        Assert.Single(await db.Database.GetAppliedMigrationsAsync());
        Assert.Empty(await db.Database.GetPendingMigrationsAsync());
        Assert.Equal("Dune", (await db.Books.AsNoTracking().SingleAsync()).Title);
    }

    [Fact]
    public async Task MySql_EnforcesColumnLengths()
    {
        await fixture.ResetAsync();
        await using var db = fixture.CreateContext();
        await db.Database.MigrateAsync();
        db.Books.Add(new Book { Title = new string('a', 161), Author = "Autore" });
        await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
    }

    [Fact]
    public async Task Api_UsesRealMySql_ForCrudSearchFiltersAndSort()
    {
        await fixture.ResetAsync();
        await using var factory = new MySqlApiFactory(fixture.Container.GetConnectionString());
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Api-Key", "mysql-test-key");
        var ids = new List<int>();
        foreach (var (title, status, rating) in new (string, BookStatus, int?)[]
        {
            ("Zeta", BookStatus.ToRead, null),
            ("Città", BookStatus.Reading, 5),
            ("Alfa", BookStatus.Finished, 2),
            ("Beta", BookStatus.Reading, 5)
        })
        {
            using var created = await client.PostAsJsonAsync("/api/books", new SaveBookRequest
            {
                Title = title, Author = "Autore", Status = status, Rating = rating,
                CurrentPage = 12, TotalPages = 100, Notes = "Note con accenti: è à"
            }, Json);
            Assert.Equal(HttpStatusCode.Created, created.StatusCode);
            ids.Add((await created.Content.ReadFromJsonAsync<BookDto>(Json))!.Id);
        }

        foreach (var (sort, expected) in new[]
        {
            ("status", new[] { 0, 3, 1, 2 }),
            ("status-desc", new[] { 2, 3, 1, 0 }),
            ("rating", new[] { 3, 1, 2, 0 }),
            ("rating-asc", new[] { 2, 3, 1, 0 }),
            ("title", new[] { 2, 3, 1, 0 }),
            ("recent", new[] { 3, 2, 1, 0 })
        })
        {
            var result = await client.GetFromJsonAsync<List<BookDto>>($"/api/books?sort={sort}", Json);
            Assert.Equal(expected.Select(i => ids[i]), result!.Select(b => b.Id));
        }
        var filtered = await client.GetFromJsonAsync<List<BookDto>>("/api/books?status=Reading&search=CITTA&sort=rating", Json);
        Assert.Equal(ids[1], Assert.Single(filtered!).Id);
        Assert.Equal("Note con accenti: è à", filtered![0].Notes);
        var update = new SaveBookRequest
        {
            Title = "Città aggiornata", Author = "Autore", Status = BookStatus.Finished,
            CurrentPage = 100, TotalPages = 100, Rating = 4, FinishedOn = new DateTime(2026, 10, 27)
        };
        Assert.Equal(HttpStatusCode.NoContent, (await client.PutAsJsonAsync($"/api/books/{ids[1]}", update, Json)).StatusCode);
        await using (var db = fixture.CreateContext())
        {
            var stored = await db.Books.AsNoTracking().SingleAsync(b => b.Id == ids[1]);
            Assert.Equal(update.Title, stored.Title);
            Assert.Equal(update.FinishedOn, stored.FinishedOn);
            Assert.Equal(BookStatus.Finished, stored.Status);
        }
        Assert.Equal(HttpStatusCode.NoContent, (await client.DeleteAsync($"/api/books/{ids[1]}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"/api/books/{ids[1]}")).StatusCode);
    }

    private sealed class MySqlApiFactory(string connectionString) : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Testing");
            builder.UseSetting("ConnectionStrings:Default", connectionString);
            builder.UseSetting("ApiKey", "mysql-test-key");
        }
    }
}
