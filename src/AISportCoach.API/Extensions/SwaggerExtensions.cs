using Microsoft.OpenApi;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace AISportCoach.API.Extensions;

public static class SwaggerExtensions
{
    public static SwaggerGenOptions AddJwtAuthentication(this SwaggerGenOptions options)
    {
        options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
        {
            Name = "Authorization",
            Type = SecuritySchemeType.Http,
            Scheme = "bearer",
            BearerFormat = "JWT",
            In = ParameterLocation.Header,
            Description = "JWT Authorization header using the Bearer scheme. Enter your token in the text input below."
        });

        options.AddSecurityRequirement(doc =>
        {
            var scheme = new OpenApiSecuritySchemeReference("Bearer", doc, null);
            return new OpenApiSecurityRequirement
            {
                [scheme] = []
            };
        });

        return options;
    }
}
