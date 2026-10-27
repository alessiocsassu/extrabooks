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
        [FromQuery] string? search, [FromQuery] string? sort, [FromQuery] BookStatus? status, CancellationToken ct)
    {
        if (status.HasValue && !Enum.IsDefined(status.Value))
        {
            ModelState.AddModelError(nameof(status), "Stato non valido.");
            return ValidationProblem(ModelState);
        }
        return Ok(await _service.GetAllAsync(search, sort, status, ct));
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<BookDto>> GetById(int id, CancellationToken ct)
    {
        return Ok(await _service.GetByIdAsync(id, ct));
    }

    [HttpPost]
    public async Task<ActionResult<BookDto>> Create([FromBody] SaveBookRequest request, CancellationToken ct)
    {
        var book = await _service.CreateAsync(request, ct);
        return CreatedAtAction(nameof(GetById), new { id = book.Id }, book);
    }

    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, [FromBody] SaveBookRequest request, CancellationToken ct)
    {
        await _service.UpdateAsync(id, request, ct);
        return NoContent();
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id, CancellationToken ct)
    {
        await _service.DeleteAsync(id, ct);
        return NoContent();
    }
}
