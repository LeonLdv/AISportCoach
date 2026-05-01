using AISportCoach.Domain.Exceptions;
using System.Net;

namespace AISportCoach.API.Middleware;

public class ExceptionHandlingMiddleware(RequestDelegate next, ILogger<ExceptionHandlingMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await next(context);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Unhandled exception: {Message}", ex.Message);
            await HandleExceptionAsync(context, ex);
        }
    }

    private static Task HandleExceptionAsync(HttpContext context, Exception exception)
    {
        var (statusCode, title) = exception switch
        {
            // Auth exceptions
            InvalidCredentialsException or EmailNotConfirmedException or WebAuthnVerificationException
                => (HttpStatusCode.Unauthorized, "Authentication Failed"),
            UserNotFoundException
                => (HttpStatusCode.NotFound, "User Not Found"),
            UserAlreadyExistsException
                => (HttpStatusCode.Conflict, "User Already Exists"),
            InvalidTokenException
                => (HttpStatusCode.BadRequest, "Invalid Token"),
            SubscriptionRequiredException
                => (HttpStatusCode.Forbidden, "Premium Subscription Required"),

            // Existing exceptions
            VideoNotFoundException or ReportNotFoundException
                => (HttpStatusCode.NotFound, "Resource Not Found"),
            VideoTooLargeException or UnsupportedVideoFormatException
                => (HttpStatusCode.BadRequest, "Invalid Request"),
            _
                => (HttpStatusCode.InternalServerError, "Internal Server Error")
        };

        context.Response.ContentType = "application/problem+json";
        context.Response.StatusCode = (int)statusCode;

        var problem = new Microsoft.AspNetCore.Mvc.ProblemDetails
        {
            Status = (int)statusCode,
            Title = title,
            Detail = exception.Message,
            Instance = context.Request.Path
        };

        return context.Response.WriteAsJsonAsync(problem);
    }
}
