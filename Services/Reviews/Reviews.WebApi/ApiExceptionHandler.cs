using FluentValidation;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using MongoDB.Driver;
using Reviews.Domain.Exceptions;

namespace Reviews.WebApi;

public sealed class ApiExceptionHandler(
    IProblemDetailsService problemDetails,
    ILogger<ApiExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext http, Exception exception, CancellationToken ct)
    {
        var status = exception switch
        {
            NotFoundException => StatusCodes.Status404NotFound,
            ValidationException => StatusCodes.Status400BadRequest,
            DomainException => StatusCodes.Status400BadRequest,
            ConflictException => StatusCodes.Status409Conflict,
            MongoWriteException { WriteError.Category: ServerErrorCategory.DuplicateKey }
                => StatusCodes.Status409Conflict,
            MongoWriteException => StatusCodes.Status400BadRequest,
            MongoConnectionException => StatusCodes.Status503ServiceUnavailable,
            TimeoutException => StatusCodes.Status503ServiceUnavailable,
            _ => StatusCodes.Status500InternalServerError
        };

        http.Response.StatusCode = status;
        logger.LogError(exception, "Request {RequestPath} failed with status {StatusCode}",
            http.Request.Path, status);

        var context = new ProblemDetailsContext { HttpContext = http, Exception = exception };
        if (exception is ValidationException validation)
        {
            context.ProblemDetails.Extensions["errors"] = validation.Errors
                .GroupBy(error => error.PropertyName)
                .ToDictionary(
                    group => group.Key,
                    group => group.Select(error => error.ErrorMessage).ToArray());
        }

        return await problemDetails.TryWriteAsync(context);
    }
}
