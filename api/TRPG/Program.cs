using Microsoft.AspNetCore.Builder;
using TRPG.Application.Common.Extensions;
using TRPG.Extensions;

var builder = WebApplication.CreateBuilder(
    new WebApplicationOptions { Args = args, ContentRootPath = AppContext.BaseDirectory }
);

builder.Services.AddTrpgHostServices(builder.Configuration).AddTrpgApplicationServices();

var app = builder.Build();

app.UseTrpgServices();
app.MapTrpgEndpoints();

await app.RunAsync();
