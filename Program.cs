using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using System.Text.Json.Serialization;
using Zabota.Data;
using Zabota.Endpoints;
using Zabota.Models;

var builder = WebApplication.CreateBuilder(args);

builder.Services.ConfigureHttpJsonOptions(o =>
{
    // enum → русские строки
    o.SerializerOptions.Converters.Add(new FamilyRoleJsonConverter());
    // при желании запретить числа для других enum
    o.SerializerOptions.Converters.Add(new JsonStringEnumConverter(allowIntegerValues: false));
});
// ---- DB connection string (shows runtime host/port for clarity) ----
var cs = builder.Configuration.GetConnectionString("DefaultConnection")!;
if (builder.Environment.IsDevelopment() && Environment.GetEnvironmentVariable("USE_TUNNEL") == "1")
{
    var b = new Npgsql.NpgsqlConnectionStringBuilder(cs) { Host = "127.0.0.1", Port = 5433 };
    cs = b.ToString();
}
builder.Services.AddDbContext<AppDb>(opt => opt.UseNpgsql(cs));


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
    await db.Database.EnsureCreatedAsync();
}

// ---- Endpoints ----
app.UsePathBase("/api");
app.MapAuthEndpoints();
app.MapUserEndpoints(); 

app.Run();
