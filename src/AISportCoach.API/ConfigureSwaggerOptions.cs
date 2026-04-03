using Asp.Versioning.ApiExplorer;
using Microsoft.Extensions.Options;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace AISportCoach.API;

public sealed class ConfigureSwaggerOptions(IApiVersionDescriptionProvider provider)
    : IConfigureOptions<SwaggerGenOptions>
{
    public void Configure(SwaggerGenOptions options)
    {
        foreach (var description in provider.ApiVersionDescriptions)
        {
            var title = description.IsDeprecated
                ? $"AI Tennis Coach API {description.GroupName} (deprecated)"
                : $"AI Tennis Coach API {description.GroupName}";

            options.SwaggerDoc(description.GroupName, new()
            {
                Title = title,
                Version = description.GroupName
            });
        }
    }
}
