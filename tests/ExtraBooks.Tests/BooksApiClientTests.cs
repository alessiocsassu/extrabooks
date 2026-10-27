using System.Net;
using System.Text;
using ExtraBooks.Contracts.Books;
using ExtraBooks.Web.Services;
using Xunit;

namespace ExtraBooks.Tests;

public sealed class BooksApiClientTests
{
    [Theory]
    [InlineData(400, "{\"errors\":{\"Title\":[\"Titolo obbligatorio.\"]}}", "Titolo obbligatorio.")]
    [InlineData(400, "not json", "Controlla i dati inseriti.")]
    [InlineData(404, "{}", "Il libro non esiste più. Aggiorna l'elenco.")]
    [InlineData(401, "{}", "Accesso all'API non autorizzato. Controlla la configurazione della chiave.")]
    [InlineData(500, "{\"detail\":\"private credentials\"}", "Il servizio non è disponibile. Riprova più tardi.")]
    public async Task FailedResponse_ProducesReadableMessage(int status, string body, string message)
    {
        using var http = new HttpClient(new ResponseHandler((HttpStatusCode)status, body))
        {
            BaseAddress = new Uri("http://localhost/")
        };
        var client = new BooksApiClient(http);

        var exception = await Assert.ThrowsAsync<ApiException>(() => client.CreateAsync(new SaveBookRequest()));
        Assert.Equal(message, exception.Message);
        Assert.Equal((HttpStatusCode)status, exception.StatusCode);
    }

    private sealed class ResponseHandler(HttpStatusCode status, string body) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => Task.FromResult(new HttpResponseMessage(status)
            {
                Content = new StringContent(body, Encoding.UTF8, "application/problem+json")
            });
    }
}
