using ExtraBooks.Contracts.Books;

namespace ExtraBooks.Api.Data;

public sealed class Book
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Author { get; set; } = string.Empty;
    public BookStatus Status { get; set; }
    public int CurrentPage { get; set; }
    public int? TotalPages { get; set; }
    public int? Rating { get; set; }
    public string? Notes { get; set; }
    public DateTime? FinishedOn { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}
