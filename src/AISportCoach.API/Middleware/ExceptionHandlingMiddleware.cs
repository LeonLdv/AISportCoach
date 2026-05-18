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
        var (statusCode, title, detail) = exception switch
        {
            // Auth exceptions
            InvalidCredentialsException or EmailNotConfirmedException or WebAuthnVerificationException or InvalidTokenException
                => (HttpStatusCode.Unauthorized, "Authentication Failed", exception.Message),
            UserNotFoundException
                => (HttpStatusCode.NotFound, "User Not Found", exception.Message),
            UserAlreadyExistsException
                => (HttpStatusCode.Conflict, "User Already Exists", exception.Message),
            SubscriptionRequiredException
                => (HttpStatusCode.Forbidden, "Premium Subscription Required", exception.Message),

            // Existing exceptions
            VideoNotFoundException or ReportNotFoundException
                => (HttpStatusCode.NotFound, "Resource Not Found", exception.Message),
            VideoTooLargeException or UnsupportedVideoFormatException
                => (HttpStatusCode.BadRequest, "Invalid Request", exception.Message),
            _
                => (HttpStatusCode.InternalServerError, "Internal Server Error", "An unexpected error occurred. Please try again later.")
        };

        context.Response.ContentType = "application/problem+json";
        context.Response.StatusCode = (int)statusCode;

        var problem = new Microsoft.AspNetCore.Mvc.ProblemDetails
        {
            Status = (int)statusCode,
            Title = title,
            Detail = detail,
            Instance = context.Request.Path
        };

        return context.Response.WriteAsJsonAsync(problem);
    }
}
