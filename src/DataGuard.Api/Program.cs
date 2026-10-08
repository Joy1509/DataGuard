using DataGuard.Api.Services;
using DataGuard.Core.Interfaces;
using DataGuard.Infrastructure.Reporting;
using DataGuard.Infrastructure.Repositories;
using DataGuard.Infrastructure.Sql;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

// Configure Serilog
Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(builder.Configuration)
    .Enrich.FromLogContext()
    .WriteTo.Console()
    .CreateLogger();

builder.Host.UseSerilog();

// Add services
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new Microsoft.OpenApi.Models.OpenApiInfo
    {
        Title = "DataGuard API",
        Version = "v1",
        Description = "Automated Data Validation and Migration Integrity Engine for SQL Server"
    });
});

builder.Services.AddProblemDetails();

// CORS for React Dashboard
builder.Services.AddCors(options =>
{
    options.AddPolicy("DashboardCors", policy =>
    {
        policy.WithOrigins("http://localhost:5173", "http://localhost:3000")
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials();
    });
});

var metadataConn = builder.Configuration.GetConnectionString("DataGuardDB");

// Register DI services (Uses in-memory demo repositories for seamless showcase if DB not configured)
builder.Services.AddSingleton<ISqlSanitizer, SqlSanitizer>();

if (string.IsNullOrWhiteSpace(metadataConn) || metadataConn.Contains("localhost"))
{
    builder.Services.AddSingleton<ISqlExecutor, InMemoryDemoSqlExecutor>();
    builder.Services.AddSingleton<IRuleRepository, InMemoryDemoRuleRepository>();
    builder.Services.AddSingleton<IRunRepository, InMemoryDemoRunRepository>();
    builder.Services.AddSingleton<IDefectRepository, InMemoryDemoDefectRepository>();
}
else
{
    builder.Services.AddScoped<ISqlExecutor, SqlExecutor>();
    builder.Services.AddScoped<IRuleRepository>(_ => new RuleRepository(metadataConn));
    builder.Services.AddScoped<IRunRepository>(_ => new RunRepository(metadataConn));
    builder.Services.AddScoped<IDefectRepository>(_ => new DefectRepository(metadataConn));
}

builder.Services.AddSingleton<IPdfReportService, QuestPdfReportService>();

// Hosted Background Execution Queue
builder.Services.AddSingleton<ValidationJobQueue>();
builder.Services.AddHostedService<ValidationBackgroundWorker>();

var app = builder.Build();

app.UseSerilogRequestLogging();
app.UseExceptionHandler();

if (app.Environment.IsDevelopment() || true)
{
    app.UseSwagger();
    app.UseSwaggerUI(c => c.SwaggerEndpoint("/swagger/v1/swagger.json", "DataGuard API v1"));
}

app.UseCors("DashboardCors");
app.UseAuthorization();
app.MapControllers();

app.Run();

public partial class Program { }
