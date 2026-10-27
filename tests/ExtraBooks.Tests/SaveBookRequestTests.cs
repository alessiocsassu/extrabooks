using System.ComponentModel.DataAnnotations;
using ExtraBooks.Contracts.Books;
using Xunit;

namespace ExtraBooks.Tests;

public sealed class SaveBookRequestTests
{
    public static IEnumerable<object[]> InvalidInputs()
    {
        yield return new object[] { new SaveBookRequest { Title = "", Author = "Autore" }, "Title" };
        yield return new object[] { new SaveBookRequest { Title = " ", Author = "Autore" }, "Title" };
        yield return new object[] { new SaveBookRequest { Title = "Titolo", Author = "" }, "Author" };
        yield return new object[] { new SaveBookRequest { Title = "Titolo", Author = " " }, "Author" };
        yield return new object[] { new SaveBookRequest { Title = new string('a', 161), Author = "Autore" }, "Title" };
        yield return new object[] { new SaveBookRequest { Title = "Titolo", Author = new string('a', 121) }, "Author" };
        yield return new object[] { ValidWithPages(-1, null), "CurrentPage" };
        yield return new object[] { ValidWithPages(100001, null), "CurrentPage" };
        yield return new object[] { ValidWithPages(0, 0), "TotalPages" };
        yield return new object[] { ValidWithPages(0, 100001), "TotalPages" };
        yield return new object[] { new SaveBookRequest { Title = "Titolo", Author = "Autore", Rating = 0 }, "Rating" };
        yield return new object[] { new SaveBookRequest { Title = "Titolo", Author = "Autore", Rating = 6 }, "Rating" };
        yield return new object[] { new SaveBookRequest { Title = "Titolo", Author = "Autore", Status = (BookStatus)99 }, "Status" };
        yield return new object[] { new SaveBookRequest { Title = "Titolo", Author = "Autore", Notes = new string('a', 2001) }, "Notes" };
    }

    [Theory]
    [MemberData(nameof(InvalidInputs))]
    public void InvalidInput_ReportsAffectedField(SaveBookRequest request, string field)
    {
        Assert.Contains(Validate(request), error => error.MemberNames.Contains(field));
    }

    [Theory]
    [InlineData(0, null)]
    [InlineData(100000, null)]
    [InlineData(0, 1)]
    [InlineData(1, 1)]
    [InlineData(100000, 100000)]
    public void ValidPageBoundaries_PassValidation(int current, int? total)
    {
        Assert.Empty(Validate(ValidWithPages(current, total)));
    }

    [Theory]
    [InlineData(null)]
    [InlineData(1)]
    [InlineData(5)]
    public void OptionalFields_AndTextLimits_PassValidation(int? rating)
    {
        var request = new SaveBookRequest
        {
            Title = new string('a', 160), Author = new string('a', 120),
            Notes = new string('a', 2000), Rating = rating
        };
        Assert.Empty(Validate(request));
    }

    private static SaveBookRequest ValidWithPages(int current, int? total) => new()
    {
        Title = "Titolo", Author = "Autore", CurrentPage = current, TotalPages = total
    };

    [Fact]
    public void CurrentPage_CannotExceedTotalPages()
    {
        var request = new SaveBookRequest
        {
            Title = "Dune", Author = "Frank Herbert", CurrentPage = 501, TotalPages = 500
        };

        var errors = Validate(request);

        var error = Assert.Single(errors);
        Assert.Equal(nameof(SaveBookRequest.CurrentPage), Assert.Single(error.MemberNames));
    }

    [Fact]
    public void ValidBook_PassesValidation()
    {
        var request = new SaveBookRequest
        {
            Title = "Dune", Author = "Frank Herbert", Status = BookStatus.Reading,
            CurrentPage = 100, TotalPages = 500, Rating = 5
        };

        Assert.Empty(Validate(request));
    }

    private static List<ValidationResult> Validate(SaveBookRequest request)
    {
        var errors = new List<ValidationResult>();
        Validator.TryValidateObject(request, new ValidationContext(request), errors, true);
        return errors;
    }
}
