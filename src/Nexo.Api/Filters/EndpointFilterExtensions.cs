namespace Nexo.Api.Filters;

internal static class EndpointFilterExtensions
{
    public static RouteHandlerBuilder WithValidation<T>(this RouteHandlerBuilder builder) where T : class =>
        builder.AddEndpointFilter<ValidationFilter<T>>().ProducesValidationProblem();

    public static RouteHandlerBuilder RequireAdminKey(this RouteHandlerBuilder builder) =>
        builder.AddEndpointFilter<AdminKeyFilter>().ProducesProblem(StatusCodes.Status403Forbidden);
}
