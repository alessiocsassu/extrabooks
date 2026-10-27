using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace ExtraBooks.Api.Data;

public sealed class BooksDbContextFactory : IDesignTimeDbContextFactory<BooksDbContext>
{
    public BooksDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__Default")
            ?? "Server=127.0.0.1;Port=3307;Database=extrabooks;User=extrabooks;";
        var options = new DbContextOptionsBuilder<BooksDbContext>()
            .UseMySql(connectionString, new MySqlServerVersion(new Version(8, 4, 0)));
        return new BooksDbContext(options.Options);
    }
}
