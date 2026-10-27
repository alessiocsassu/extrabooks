using ExtraBooks.Api.Data;
using ExtraBooks.Contracts.Books;
using Microsoft.EntityFrameworkCore;

namespace ExtraBooks.Api.Services;

public sealed class BookService
{
    private readonly BooksDbContext _db;

    public BookService(BooksDbContext db) => _db = db;

    public async Task<List<BookDto>> GetAllAsync(string? search, string? sort, CancellationToken ct)
    {
        IQueryable<Book> query = _db.Books.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            query = query.Where(book => book.Title.Contains(term) || book.Author.Contains(term));
        }

        query = string.Equals(sort, "title", StringComparison.OrdinalIgnoreCase)
            ? query.OrderBy(book => book.Title).ThenBy(book => book.Id)
            : query.OrderByDescending(book => book.CreatedAtUtc).ThenByDescending(book => book.Id);

        var books = await query.ToListAsync(ct);
        return books.Select(ToDto).ToList();
    }

    public async Task<BookDto?> GetByIdAsync(int id, CancellationToken ct)
    {
        var book = await _db.Books.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, ct);
        return book is null ? null : ToDto(book);
    }

    public async Task<BookDto> CreateAsync(SaveBookRequest request, CancellationToken ct)
    {
        var book = new Book();
        Apply(book, request);
        _db.Books.Add(book);
        await _db.SaveChangesAsync(ct);
        return ToDto(book);
    }

    public async Task<bool> UpdateAsync(int id, SaveBookRequest request, CancellationToken ct)
    {
        var book = await _db.Books.FindAsync(new object[] { id }, ct);
        if (book is null) return false;

        Apply(book, request);
        await _db.SaveChangesAsync(ct);
        return true;
    }

    public async Task<bool> DeleteAsync(int id, CancellationToken ct)
    {
        var book = await _db.Books.FindAsync(new object[] { id }, ct);
        if (book is null) return false;

        _db.Books.Remove(book);
        await _db.SaveChangesAsync(ct);
        return true;
    }

    private static void Apply(Book book, SaveBookRequest request)
    {
        book.Title = request.Title.Trim();
        book.Author = request.Author.Trim();
        book.Status = request.Status;
        book.CurrentPage = request.CurrentPage;
        book.TotalPages = request.TotalPages;
        book.Rating = request.Rating;
        book.Notes = string.IsNullOrWhiteSpace(request.Notes) ? null : request.Notes.Trim();
        book.FinishedOn = request.FinishedOn;
    }

    private static BookDto ToDto(Book book) => new()
    {
        Id = book.Id,
        Title = book.Title,
        Author = book.Author,
        Status = book.Status,
        CurrentPage = book.CurrentPage,
        TotalPages = book.TotalPages,
        Rating = book.Rating,
        Notes = book.Notes,
        FinishedOn = book.FinishedOn,
        CreatedAtUtc = book.CreatedAtUtc
    };
}
