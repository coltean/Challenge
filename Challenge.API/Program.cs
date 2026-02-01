using Serilog;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Challenge.API.Data;
using Challenge.API.Authentication;
using Challenge.API.Services;
using Challenge.API.Validation;
using Challenge.API.Models.Dto;
using Challenge.API.Configuration;

var builder = WebApplication.CreateBuilder(args);

// ============================================================================
// LOGGING & OBSERVABILITY
// ============================================================================
Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Information()
    .WriteTo.Console(outputTemplate: "[{Timestamp:yyyy-MM-dd HH:mm:ss}] [{Level:u3}] {Message:lj}{NewLine}{Exception}")
    .WriteTo.File(
        "logs/cms-webhook-.txt",
        rollingInterval: RollingInterval.Day,
        outputTemplate: "[{Timestamp:yyyy-MM-dd HH:mm:ss}] [{Level:u3}] {Message:lj}{NewLine}{Exception}")
    .CreateLogger();

builder.Host.UseSerilog();

// ============================================================================
// DEPENDENCY INJECTION - CORE SERVICES
// ============================================================================
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// ============================================================================
// DATABASE CONFIGURATION
// ============================================================================
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? "Host=localhost;Port=5432;Database=ChallengeDB;Username=challenge_user;Password=Challenge123!@;";

if (builder.Environment.IsEnvironment("Testing"))
{
    // ApplicationDbContext: Used for webhook processing (read/write)
    builder.Services.AddDbContext<ApplicationDbContext>(options =>
        options.UseInMemoryDatabase("ChallengeTests"));

    // ReadOnlyDbContext: Used for API queries (optimized for reads)
    builder.Services.AddDbContext<ReadOnlyDbContext>(options =>
        options.UseInMemoryDatabase("ChallengeTests")
            .UseQueryTrackingBehavior(QueryTrackingBehavior.NoTracking));
}
else
{
    // ApplicationDbContext: Used for webhook processing (read/write)
    builder.Services.AddDbContext<ApplicationDbContext>(options =>
        options.UseNpgsql(connectionString));

    // ReadOnlyDbContext: Used for API queries (optimized for reads)
    builder.Services.AddDbContext<ReadOnlyDbContext>(options =>
        options.UseNpgsql(connectionString)
            .UseQueryTrackingBehavior(QueryTrackingBehavior.NoTracking));
}

builder.Services.AddAuthentication("BasicAuthentication")
    .AddScheme<BasicAuthenticationOptions, BasicAuthenticationHandler>(
        "BasicAuthentication",
        options =>
        {
            // CMS Webhook credentials
            options.Users.Add("cmswh_challenge", "a1b2c3d4-e5f6-7890-abcd-ef1234567890");
            options.Roles.Add("cmswh_challenge", "CMS_WEBHOOK");

            // API User credentials
            options.Users.Add("apiuser_demo", "f0e9d8c7-b6a5-4321-8765-fedcba987654");
            options.Roles.Add("apiuser_demo", "API_USER");

            // Admin credentials
            options.Users.Add("admin", "12345678-1234-1234-1234-123456789012");
            options.Roles.Add("admin", "ADMIN");
        });

builder.Services.AddAuthorization();


builder.Services.Configure<RabbitMqSettings>(builder.Configuration.GetSection("RabbitMq"));
builder.Services.AddSingleton<IEventQueueService, RabbitMqEventQueueService>();
builder.Services.AddHostedService<RabbitMqEventConsumer>();

// Event Processing Service: Handles synchronous webhook event processing
// Rationale:
// - Version sequencing must be guaranteed (events processed in order)
// - Idempotency checks must be atomic
// - Transaction scope keeps consistency high
builder.Services.AddScoped<IEventProcessingService, EventProcessingService>();


builder.Services.AddScoped<IValidator<List<CmsEventDto>>, BatchEventValidator>();
builder.Services.AddScoped<IValidator<CmsEventDto>, CmsEventValidator>();

// ============================================================================
// BUILD AND CONFIGURE MIDDLEWARE
// ============================================================================
var app = builder.Build();

// Configure OpenAPI/Swagger in Development
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// ============================================================================
// MIDDLEWARE PIPELINE
// ============================================================================
app.UseHttpsRedirection();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

// ============================================================================
// DATABASE INITIALIZATION
// ============================================================================
if (!app.Environment.IsEnvironment("Testing"))
{
    try
    {
        using (var scope = app.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            
            Log.Information("Applying database migrations...");
            await dbContext.Database.MigrateAsync();
            Log.Information("Database migrations completed successfully");
        }
    }
    catch (Exception ex)
    {
        Log.Fatal(ex, "Database initialization failed");
        throw;
    }
}

Log.Information("CMS Webhook API starting...");

try
{
    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Application terminated unexpectedly");
}
finally
{
    Log.CloseAndFlush();
}

// Make Program public for testing
public partial class Program { }