using System.Net;

namespace ExtraBooks.Web.Services;

public sealed class ApiException(string message, HttpStatusCode statusCode)
    : HttpRequestException(message, null, statusCode);
