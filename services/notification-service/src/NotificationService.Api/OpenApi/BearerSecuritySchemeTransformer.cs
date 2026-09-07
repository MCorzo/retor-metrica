using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace NotificationService.Api.OpenApi;

/// <summary>
/// Declares the JWT Bearer security scheme in the OpenAPI document so the Scalar
/// reference UI shows an "Authorize" button and presents every operation as
/// protected (a required <c>Authorization</c> header). Without a declared
/// scheme Scalar offers no way to attach the <c>Authorization</c> header at all.
/// </summary>
public sealed class BearerSecuritySchemeTransformer : IOpenApiDocumentTransformer
{
    public Task TransformAsync(
        OpenApiDocument document,
        OpenApiDocumentTransformerContext context,
        CancellationToken cancellationToken)
    {
        document.Components ??= new OpenApiComponents();
        document.Components.SecuritySchemes = new Dictionary<string, IOpenApiSecurityScheme>
        {
            ["Bearer"] = new OpenApiSecurityScheme
            {
                Type = SecuritySchemeType.Http,
                Scheme = "bearer",
                BearerFormat = "JWT",
                Description = "JWT Authorization header using the Bearer scheme. Example: \"Bearer {token}\"",
            },
        };

        document.Security =
        [
            new OpenApiSecurityRequirement
            {
                [new OpenApiSecuritySchemeReference("Bearer", document, null)] = [],
            },
        ];

        return Task.CompletedTask;
    }
}