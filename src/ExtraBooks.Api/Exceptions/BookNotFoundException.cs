namespace ExtraBooks.Api.Exceptions;

public sealed class BookNotFoundException(int id)
    : DomainException($"Il libro con ID {id} non esiste più.");
