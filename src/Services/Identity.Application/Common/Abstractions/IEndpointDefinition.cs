using Microsoft.AspNetCore.Routing;

namespace Identity.Application.Common.Abstractions;

/// <summary>
/// Marker interface for endpoint definitions that are auto-scanned
/// and registered by <c>MapIdentityEndpoints</c> extension method.
/// </summary>
public interface IEndpointDefinition
{
    void DefineEndpoints(IEndpointRouteBuilder app);
}
