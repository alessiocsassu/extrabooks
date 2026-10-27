using System.ComponentModel.DataAnnotations;

namespace ExtraBooks.Contracts.Books;

public sealed class SaveBookRequest : IValidatableObject
{
    [Required(ErrorMessage = "Il titolo è obbligatorio.")]
    [StringLength(160, ErrorMessage = "Il titolo non può superare 160 caratteri.")]
    public string Title { get; set; } = string.Empty;

    [Required(ErrorMessage = "L'autore è obbligatorio.")]
    [StringLength(120, ErrorMessage = "L'autore non può superare 120 caratteri.")]
    public string Author { get; set; } = string.Empty;

    public BookStatus Status { get; set; } = BookStatus.ToRead;

    [Range(0, 100000, ErrorMessage = "La pagina attuale deve essere compresa tra 0 e 100000.")]
    public int CurrentPage { get; set; }

    [Range(1, 100000, ErrorMessage = "Il totale delle pagine deve essere compreso tra 1 e 100000.")]
    public int? TotalPages { get; set; }

    [Range(1, 5, ErrorMessage = "Il voto deve essere compreso tra 1 e 5.")]
    public int? Rating { get; set; }

    [StringLength(2000, ErrorMessage = "Le note non possono superare 2000 caratteri.")]
    public string? Notes { get; set; }

    // È la data scelta dall'utente, non un timestamp di sistema.
    public DateTime? FinishedOn { get; set; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (TotalPages.HasValue && CurrentPage > TotalPages.Value)
        {
            yield return new ValidationResult(
                "La pagina corrente non può superare il numero totale di pagine.",
                new[] { nameof(CurrentPage) });
        }

        if (!Enum.IsDefined(Status))
        {
            yield return new ValidationResult("Stato non valido.", new[] { nameof(Status) });
        }
    }
}
