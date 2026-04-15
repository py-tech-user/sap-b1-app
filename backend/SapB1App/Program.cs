using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using Serilog;
using System.Text;
using SapB1App.Data;
using SapB1App.Services;
using SapB1App.Interfaces;
using SapB1App.Middleware;
using SapB1App.Models;
using FluentValidation;

var builder = WebApplication.CreateBuilder(args);

// ─── Kestrel - Écouter sur HTTP uniquement ─────────────────────────────────
builder.WebHost.ConfigureKestrel(options =>
{
    options.ListenAnyIP(5000); // HTTP sur toutes les interfaces (0.0.0.0:5000)
});

// ─── Serilog ───────────────────────────────────────────────────────────────
Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(builder.Configuration)
    .Enrich.FromLogContext()
    .WriteTo.Console()
    .WriteTo.File("logs/sapb1app-.log", rollingInterval: RollingInterval.Day)
    .CreateLogger();

builder.Host.UseSerilog();

// ─── Database ───────────────────────────────────────────────────────────────
builder.Services.AddDbContext<AppDbContext>(options =>
{
    options.UseSqlServer(
        builder.Configuration.GetConnectionString("DefaultConnection"),
        sqlOptions =>
        {
            sqlOptions.CommandTimeout(30); // Timeout de 30 secondes pour les commandes SQL
            sqlOptions.EnableRetryOnFailure(
                maxRetryCount: 3,
                maxRetryDelay: TimeSpan.FromSeconds(5),
                errorNumbersToAdd: null);
        });
    
    // Désactiver le tracking global pour améliorer les performances
    options.UseQueryTrackingBehavior(QueryTrackingBehavior.NoTracking);
});

// ─── AutoMapper ────────────────────────────────────────────────────────────
builder.Services.AddAutoMapper(typeof(Program));

// ─── FluentValidation ──────────────────────────────────────────────────────
builder.Services.AddValidatorsFromAssemblyContaining<Program>();

// ─── Services DI ───────────────────────────────────────────────────────────
builder.Services.AddScoped<ICustomerService, CustomerService>();
builder.Services.AddScoped<IOrderService, OrderService>();
builder.Services.AddScoped<IOrderLineService, OrderLineService>();
builder.Services.AddScoped<IProductService, ProductService>();
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<IVisitService, VisitService>();
builder.Services.AddScoped<IPaymentService, PaymentService>();
builder.Services.AddScoped<IQuoteService, QuoteService>();
builder.Services.AddScoped<IDeliveryNoteService, DeliveryNoteService>();
builder.Services.AddScoped<IInvoiceService, InvoiceService>();
builder.Services.AddScoped<ICreditNoteService, CreditNoteService>();
builder.Services.AddScoped<IReturnService, ReturnService>();
builder.Services.AddScoped<ITrackingService, TrackingService>();
builder.Services.AddScoped<IReportingService, ReportingService>();

// ─── SAP B1 DI API Service (Scoped pour gestion de connexion par requête) ───
builder.Services.AddScoped<ISapB1Service, SapB1Service>();

// ─── JWT Authentication ─────────────────────────────────────────────────────
var jwtSettings = builder.Configuration.GetSection("JwtSettings");
var secretKey = jwtSettings["Secret"] ?? throw new InvalidOperationException("JWT Secret not configured");

builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,
        ValidIssuer = jwtSettings["Issuer"],
        ValidAudience = jwtSettings["Audience"],
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretKey)),
        ClockSkew = TimeSpan.FromMinutes(5)
    };

    options.Events = new JwtBearerEvents
    {
        OnAuthenticationFailed = context =>
        {
            Log.Warning("JWT Auth failed: {Error}", context.Exception.Message);
            return Task.CompletedTask;
        }
    };
});

// ─── Authorization Policies ─────────────────────────────────────────────────
builder.Services.AddAuthorization(options =>
{
    // Admin uniquement
    options.AddPolicy(Policies.AdminOnly, policy =>
        policy.RequireRole(Roles.Admin));

    // Manager ou Admin
    options.AddPolicy(Policies.ManagerOrAdmin, policy =>
        policy.RequireRole(Roles.Admin, Roles.Manager));

    // Tous les rôles authentifiés
    options.AddPolicy(Policies.AllRoles, policy =>
        policy.RequireRole(Roles.Admin, Roles.Manager, Roles.Commercial));
});

// ─── CORS ───────────────────────────────────────────────────────────────────
builder.Services.AddCors(options =>
{
    options.AddPolicy("AngularApp", policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyHeader()
              .AllowAnyMethod();
    });
});

// ─── Controllers & Swagger ─────────────────────────────────────────────────
builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase;
        options.JsonSerializerOptions.DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull;
    });
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "SAP Business One Integration API",
        Version = "v1",
        Description = "API REST pour l'intégration SAP Business One"
    });

    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.ApiKey,
        Scheme = "Bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "Entrez: Bearer {votre_token}"
    });

    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            Array.Empty<string>()
        }
    });
});

// ─── Build ──────────────────────────────────────────────────────────────────
var app = builder.Build();

// ─── Migrate & Seed ────────────────────────────────────────────────────────
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    db.Database.Migrate();
    await DbSeeder.SeedAsync(db);
}

// ─── Middleware Pipeline ────────────────────────────────────────────────────
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c => c.SwaggerEndpoint("/swagger/v1/swagger.json", "SapB1App v1"));
}

// Note: HTTPS désactivé - utiliser un reverse proxy (IIS, nginx) pour HTTPS en production

// CORS MUST be FIRST (before any middleware that might reject the request)
app.UseCors("AngularApp");

// ─── Servir les fichiers statiques Angular (wwwroot) ─────────────────────────
app.UseDefaultFiles(); // Cherche index.html par défaut
app.UseStaticFiles();  // Sert les fichiers depuis wwwroot

app.UseMiddleware<ExceptionMiddleware>();
app.UseSerilogRequestLogging();

app.UseAuthentication();
app.UseAuthorization();

// ─── Map API Controllers (/api/*) ────────────────────────────────────────────
app.MapControllers();

// ─── Fallback pour Angular SPA (routes client-side) ──────────────────────────
app.MapFallbackToFile("index.html");

app.Run();
