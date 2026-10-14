using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace ExpenseTracker.Api.OpenApi;

// Adds a "Bearer token" option to the generated API description
public sealed class BearerSecuritySchemeTransformer : IOpenApiDocumentTransformer
{
    private readonly IAuthenticationSchemeProvider _schemes;

    public BearerSecuritySchemeTransformer(IAuthenticationSchemeProvider schemes)
    {
        _schemes = schemes;
    }

    public async Task TransformAsync(
        OpenApiDocument document,
        OpenApiDocumentTransformerContext context,
        CancellationToken cancellationToken)
    {
        var schemes = await _schemes.GetAllSchemesAsync();
        if (!schemes.Any(s => s.Name == "Bearer")) return;

        document.Components ??= new OpenApiComponents();
        document.Components.SecuritySchemes = new Dictionary<string, IOpenApiSecurityScheme>
        {
            ["Bearer"] = new OpenApiSecurityScheme
            {
                Type = SecuritySchemeType.Http,
                Scheme = "bearer",
                BearerFormat = "JWT",
                In = ParameterLocation.Header
            }
        };

        // Mark every endpoint as using the token
        foreach (var operation in document.Paths.Values.SelectMany(path => path.Operations))
        {
            operation.Value.Security ??= [];
            operation.Value.Security.Add(new OpenApiSecurityRequirement
            {
                [new OpenApiSecuritySchemeReference("Bearer", document)] = []
            });
        }
    }
}