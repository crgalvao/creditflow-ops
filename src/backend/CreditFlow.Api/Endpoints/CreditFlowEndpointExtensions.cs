namespace CreditFlow.Api.Endpoints;

public static class CreditFlowEndpointExtensions
{
    public static IEndpointRouteBuilder MapCreditFlowEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapApplicationEndpoints();
        endpoints.MapKycEndpoints();
        endpoints.MapCreditDecisioningEndpoints();

        return endpoints;
    }
}
