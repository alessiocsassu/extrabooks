using Microsoft.EntityFrameworkCore;

namespace ExtraBooks.Api.Data;

public sealed class BooksDbContext : DbContext
{
    public BooksDbContext(DbContextOptions<BooksDbContext> options) : base(options) { }

    public DbSet<Book> Books => Set<Book>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        var book = modelBuilder.Entity<Book>();
        book.ToTable("books");
        book.HasKey(x => x.Id);
        book.Property(x => x.Title).HasMaxLength(160).IsRequired();
        book.Property(x => x.Author).HasMaxLength(120).IsRequired();
        book.Property(x => x.Status).HasConversion<string>().HasMaxLength(20).IsRequired();
        book.Property(x => x.Notes).HasMaxLength(2000);
        book.HasIndex(x => x.Title);
    }
}
