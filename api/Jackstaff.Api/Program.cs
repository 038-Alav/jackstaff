using System.Text;
using Dapper;
using Jackstaff.Api.Infrastructure;
using Jackstaff.Api.Infrastructure.Security;
using Jackstaff.Api.Modules.Auth;
using Jackstaff.Api.Modules.Dashboard;
using Jackstaff.Api.Modules.Inventory;
using Jackstaff.Api.Modules.Tickets;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;

DefaultTypeMap.MatchNamesWithUnderscores = true;

var builder = WebApplication.CreateBuilder(args);

var connectionString = builder.Configuration["Database:ConnectionString"]
    ?? throw new InvalidOperationException("Database:ConnectionString is required.");
var signingKey = builder.Configuration["Jwt:SigningKey"]
    ?? throw new InvalidOperationException("Jwt:SigningKey is required.");
if (Encoding.UTF8.GetByteCount(signingKey) < 48)
    throw new InvalidOperationException("Jwt:SigningKey must contain at least 48 UTF-8 bytes.");

builder.Services.AddSingleton(new DatabaseOptions(connectionString));
builder.Services.AddScoped<TenantDbSession>();
builder.Services.AddScoped<AuditWriter>();
// Conventional middleware is activated by UseMiddleware; do not register it in DI.
builder.Services.AddSingleton<PasswordHasher>();
builder.Services.AddSingleton<TokenService>();
builder.Services.AddScoped<TicketService>();

builder.Services.AddProblemDetails();
builder.Services.AddOpenApi();
builder.Services.AddCors(options =>
{
    options.AddPolicy("web", policy =>
    {
        var origins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>()
            ?? ["http://localhost:5173"];
        policy.WithOrigins(origins).AllowAnyHeader().AllowAnyMethod();
    });
});

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.MapInboundClaims = false;
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = builder.Configuration["Jwt:Issuer"],
            ValidateAudience = true,
            ValidAudience = builder.Configuration["Jwt:Audience"],
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(signingKey)),
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromSeconds(30),
            NameClaimType = "name",
            RoleClaimType = "role"
        };
    });

builder.Services.AddAuthorization(options =>
{
    foreach (var permission in new[]
    {
        "dashboard.read", "sites.read", "sites.write", "radios.read", "radios.write",
        "groups.read", "groups.write", "tickets.read", "tickets.write", "tickets.manage"
    })
    {
        options.AddPolicy(permission, policy => policy.RequireClaim("permission", permission));
    }
});

var app = builder.Build();

app.UseExceptionHandler();
app.UseCors("web");
app.UseAuthentication();
app.UseMiddleware<TenantSessionMiddleware>();
app.UseAuthorization();

if (app.Environment.IsDevelopment())
    app.MapOpenApi();

app.MapGet("/api/health", () => Results.Ok(new
{
    status = "ok",
    service = "jackstaff-api",
    version = "0.1.0",
    utc = DateTimeOffset.UtcNow
})).AllowAnonymous();

app.MapAuthEndpoints();
app.MapDashboardEndpoints();
app.MapInventoryEndpoints();
app.MapTicketEndpoints();

app.Run();

public partial class Program { }
