using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using System.Text.Json.Serialization;
using Zabota.Data;
using Zabota.Endpoints;
using Zabota.Models;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddScoped<FamilyService>();
builder.Services.ConfigureHttpJsonOptions(o =>
{
    // camelCase: id, firstName, ...
    o.SerializerOptions.PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase;

    // не писать null-поля
    o.SerializerOptions.DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull;

    // твои конвертеры как были:
    o.SerializerOptions.Converters.Add(new FamilyRoleJsonConverter());
    o.SerializerOptions.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter(allowIntegerValues: false));
});
// ---- DB connection string (shows runtime host/port for clarity) ----
var cs = builder.Configuration.GetConnectionString("DefaultConnection")!;
var csb = new NpgsqlConnectionStringBuilder(cs)
{
    // пример: меняем параметры под SSH-туннель
    Host = "localhost",
    Port = 5433
};
Console.WriteLine($"[DB at runtime] {csb.Host}:{csb.Port} / {csb.Database} / user={csb.Username}");

// ---- Services ----
builder.Services.AddDbContext<AppDb>(opt =>
    opt.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

// ---- Middleware ----
app.UseSwagger();
app.UseSwaggerUI();

// создать БД/таблицы (для демо)
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDb>();
    await db.Database.MigrateAsync();
}

// ---- Endpoints ----
app.MapAuthEndpoints();
app.MapUserEndpoints(); 
app.MapFamiliesEndpoints();
app.Run();
