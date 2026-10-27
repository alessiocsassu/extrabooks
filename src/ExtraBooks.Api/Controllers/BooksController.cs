using ExtraBooks.Api.Services;
using ExtraBooks.Contracts.Books;
using Microsoft.AspNetCore.Mvc;

namespace ExtraBooks.Api.Controllers;

[ApiController]
[Route("api/books")]
public sealed class BooksController : ControllerBase
{
    private readonly BookService _service;

    public BooksController(BookService service) => _service = service;

    [HttpGet]
    public async Task<ActionResult<List<BookDto>>> GetAll(
        [FromQuery] string? search, [FromQuery] string? sort, CancellationToken ct)
        => Ok(await _service.GetAllAsync(search, sort, ct));

    [HttpGet("{id:int}")]
    public async Task<ActionResult<BookDto>> GetById(int id, CancellationToken ct)
    {
        var book = await _service.GetByIdAsync(id, ct);
        return book is null ? NotFound() : Ok(book);
    }

    [HttpPost]
    public async Task<ActionResult<BookDto>> Create([FromBody] SaveBookRequest request, CancellationToken ct)
    {
        var book = await _service.CreateAsync(request, ct);
        return CreatedAtAction(nameof(GetById), new { id = book.Id }, book);
    }

    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, [FromBody] SaveBookRequest request, CancellationToken ct)
        => await _service.UpdateAsync(id, request, ct) ? NoContent() : NotFound();

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id, CancellationToken ct)
        => await _service.DeleteAsync(id, ct) ? NoContent() : NotFound();
}
