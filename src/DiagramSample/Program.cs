using DiagramSample.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSingleton<DiagramConfigLoader>();

var app = builder.Build();

app.UseStaticFiles();

app.MapGet("/", () => Results.Redirect("/index.html"));

app.MapGet("/api/diagram/config", (DiagramConfigLoader loader) =>
{
    try
    {
        var config = loader.LoadConfig();
        return Results.Ok(config);
    }
    catch (Exception ex)
    {
        return Results.Problem(detail: ex.Message, statusCode: 500);
    }
});

app.MapGet("/api/diagram/graph", (DiagramConfigLoader loader) =>
{
    try
    {
        var graph = loader.BuildGraph();
        return Results.Ok(graph);
    }
    catch (Exception ex)
    {
        return Results.Problem(detail: ex.Message, statusCode: 500);
    }
});

app.MapGet("/api/diagram/processes", (DiagramConfigLoader loader) =>
{
    try
    {
        var procs = loader.GetProcesses();
        return Results.Ok(procs);
    }
    catch (Exception ex)
    {
        return Results.Problem(detail: ex.Message, statusCode: 500);
    }
});

app.Run();
