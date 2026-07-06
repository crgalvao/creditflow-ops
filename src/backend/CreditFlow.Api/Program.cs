using CreditFlow.Api.Endpoints;
using CreditFlow.Api.Extensions;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddCreditFlowApi();

var app = builder.Build();

app.UseCreditFlowApi();

app.MapCreditFlowEndpoints();

await app.RunAsync();

public partial class Program;
