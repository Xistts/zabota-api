using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Zabota.Data;
using Zabota.Endpoints;
using Zabota.Models;

var builder = WebApplication.CreateBuilder(args);

// ---- Services ----
builder.Services.AddScoped<FamilyService>();

// JSON для минимальных эндпоинтов
builder.Services.ConfigureHttpJsonOptions(o =>
{
    o.SerializerOptions.PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase;
    o.SerializerOptions.DefaultIgnoreCondition = System
        .Text
        .Json
        .Serialization
        .JsonIgnoreCondition
        .WhenWritingNull;
    o.SerializerOptions.Converters.Add(new FamilyRoleJsonConverter());
    o.SerializerOptions.Converters.Add(new JsonStringEnumConverter(allowIntegerValues: false));
});

// 🔹 ВКЛЮЧАЕМ контроллеры (это как раз то, чего не хватало)
builder
    .Services.AddControllers()
    .AddJsonOptions(o =>
    {
        o.JsonSerializerOptions.PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase;
        o.JsonSerializerOptions.DefaultIgnoreCondition = System
            .Text
            .Json
            .Serialization
            .JsonIgnoreCondition
            .WhenWritingNull;
        o.JsonSerializerOptions.Converters.Add(
            new System.Text.Json.Serialization.JsonStringEnumConverter()
        );
        o.JsonSerializerOptions.Converters.Add(new FamilyRoleJsonConverter());
    });

// БД
builder.Services.AddDbContext<AppDb>(opt =>
    opt.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection"))
);

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

// ---- Middleware ----
app.UseSwagger();
app.UseSwaggerUI();

// 🔹 Health-check до всего — поможет быстро отличить 502 прокси от падения приложения
app.MapGet("/ping", () => Results.Ok("ok"));
app.MapGet(
    "/_routes",
    (IEnumerable<EndpointDataSource> sources) =>
    {
        var routes = sources
            .SelectMany(s => s.Endpoints)
            .OfType<RouteEndpoint>()
            .Select(e => new
            {
                Route = e.RoutePattern.RawText,
                Method = string.Join(
                    ',',
                    e.Metadata.OfType<HttpMethodMetadata>().FirstOrDefault()?.HttpMethods
                    ?? Array.Empty<string>()
                ),
            });
        return Results.Ok(routes);
    }
);

// 🔹 Пробуем миграции, но НЕ валим весь процесс при ошибке
using (var scope = app.Services.CreateScope())
{
    try
    {
        var db = scope.ServiceProvider.GetRequiredService<AppDb>();
        await db.Database.MigrateAsync();
    }
    catch (Exception ex)
    {
        // Логируем и продолжаем, чтобы хотя бы /ping и статические ендпоинты работали
        Console.Error.WriteLine($"[MIGRATE] {ex.GetType().Name}: {ex.Message}");
    }
}

// ---- Endpoints ----
app.MapAuthEndpoints();
app.MapUserEndpoints();
app.MapFamiliesEndpoints();

// контроллеры (теперь точно работают, т.к. AddControllers() включён)
app.MapControllers();

app.Run();
