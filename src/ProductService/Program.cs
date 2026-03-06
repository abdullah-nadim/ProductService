using Infrastructure.Messaging;
using Infrastructure.Redis.Extensions;
using Microsoft.EntityFrameworkCore;
using ProductService.Contracts;
using ProductService.Repository;
using ProductService.Models;
using ProductService.Services;
using System.Reflection;

var builder = WebApplication.CreateBuilder(args);

// ─── Database ─────────────────────────────────────────────────────────────────
builder.Services.AddDbContext<DatabaseContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

// ─── Redis Cache (via Infrastructure) ────────────────────────────────────────
builder.Services.AddRedisCache(builder.Configuration);

// ─── AutoMapper ───────────────────────────────────────────────────────────────
builder.Services.AddAutoMapper(cfg =>
{
    cfg.AddProfile<AutoMapperProfile>();
});

// ─── RabbitMQ (via Infrastructure) ───────────────────────────────────────────
// Single instance shared between IRabbitMQService consumers and IHostedService (host manages lifecycle)
builder.Services.AddSingleton<RabbitMQService>();
builder.Services.AddSingleton<IRabbitMQService>(sp => sp.GetRequiredService<RabbitMQService>());
builder.Services.AddHostedService(sp => sp.GetRequiredService<RabbitMQService>());
builder.Services.AddScoped<IProductEventPublisher, ProductEventPublisher>();
builder.Services.AddSingleton<IProductEventSubscriber, ProductEventSubscriber>();

// ─── Application Services ─────────────────────────────────────────────────────
builder.Services.AddScoped<IProductServices, ProductServices>();

// ─── Controllers ──────────────────────────────────────────────────────────────
builder.Services.AddControllers()
    .AddApplicationPart(typeof(ProductService.API.ProductAPI).Assembly);

// ─── Swagger ──────────────────────────────────────────────────────────────────
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new()
    {
        Title = "Product Microservice API",
        Version = "v1",
        Description = "A .NET 9 microservice template demonstrating clean layered architecture"
    });

    var xmlFile = $"{Assembly.GetExecutingAssembly().GetName().Name}.xml";
    var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
    if (File.Exists(xmlPath)) c.IncludeXmlComments(xmlPath);
});

// ─── CORS ─────────────────────────────────────────────────────────────────────
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
    {
        policy.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader();
    });
});

// ─── Health Checks ────────────────────────────────────────────────────────────
builder.Services.AddHealthChecks()
    .AddDbContextCheck<DatabaseContext>();

var app = builder.Build();

// ─── Auto-migrate on startup ──────────────────────────────────────────────────
using (var scope = app.Services.CreateScope())
{
    var context = scope.ServiceProvider.GetRequiredService<DatabaseContext>();
    try { context.Database.Migrate(); }
    catch (Exception ex) { Console.WriteLine($"Migration failed: {ex.Message}"); }
}

// ─── Middleware pipeline ──────────────────────────────────────────────────────
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "Product API V1");
        c.RoutePrefix = string.Empty;
    });
}

app.UseHttpsRedirection();
app.UseCors("AllowAll");
app.UseAuthorization();
app.MapHealthChecks("/health");
app.MapControllers();

// ─── Start event subscriber ───────────────────────────────────────────────────
var eventSubscriber = app.Services.GetRequiredService<IProductEventSubscriber>();
await eventSubscriber.StartAsync(CancellationToken.None);

app.Run();
