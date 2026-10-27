using System.Text.Json;
using ExtraBooks.Api.Exceptions;
using ExtraBooks.Api.Middleware;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace ExtraBooks.Tests;

public sealed class ApiExceptionHandlerTests
{
    [Theory]
    [InlineData(true, 404)]
    [InlineData(false, 500)]
    public async Task Handler_ReturnsProblemDetails_WithoutExposingUnexpectedErrors(bool missingBook, int status)
    {
        using var services = new ServiceCollection().AddLogging().BuildServiceProvider();
        var context = new DefaultHttpContext { RequestServices = services, TraceIdentifier = "test-trace" };
        context.Request.Path = "/api/books/41";
        context.Response.Body = new MemoryStream();
        Exception exception = missingBook ? new BookNotFoundException(41) : new InvalidOperationException("private database credentials");
        var handler = new ApiExceptionHandler(NullLogger<ApiExceptionHandler>.Instance);

        Assert.True(await handler.TryHandleAsync(context, exception, CancellationToken.None));
        Assert.Equal(status, context.Response.StatusCode);
        Assert.StartsWith("application/problem+json", context.Response.ContentType);
        context.Response.Body.Position = 0;
        var problem = await JsonSerializer.DeserializeAsync<JsonElement>(context.Response.Body);
        Assert.Equal(status, problem.GetProperty("status").GetInt32());
        Assert.Equal("/api/books/41", problem.GetProperty("instance").GetString());
        Assert.Equal("test-trace", problem.GetProperty("traceId").GetString());
        Assert.False(problem.TryGetProperty("stackTrace", out _));
        if (missingBook)
            Assert.Equal(exception.Message, problem.GetProperty("detail").GetString());
        else
            Assert.DoesNotContain(exception.Message, problem.ToString());
    }
}
