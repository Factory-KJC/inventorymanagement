using System.Net;
using System.Text;
using System.Text.Json.Serialization;
using InventoryAPI.Application.Auth;
using InventoryAPI.Application.Dashboard;
using InventoryAPI.Application.Inventory;
using InventoryAPI.Application.Printing;
using InventoryAPI.Application.Shopping;
using InventoryAPI.Data;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using System.Threading.RateLimiting;

namespace InventoryAPI.Configuration;

/// <summary>
/// Web APIで使用する依存関係を機能単位で登録します。
/// </summary>
public static class ServiceCollectionExtensions
{
    public const string WebClientCorsPolicy = "WebClient";

    public static IServiceCollection AddHomeStockApi(
        this IServiceCollection services,
        IConfiguration configuration,
        IHostEnvironment environment)
    {
        var connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("ConnectionStrings:DefaultConnection is required.");
        var jwt = ReadAndValidateJwtOptions(configuration);
        var allowedOrigins = configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];

        services.Configure<JwtOptions>(configuration.GetRequiredSection(JwtOptions.SectionName));
        services.Configure<ForwardedHeadersOptions>(options =>
        {
            options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
            options.ForwardLimit = 1;
            options.KnownNetworks.Clear();
            options.KnownProxies.Clear();

            foreach (var knownProxy in configuration.GetSection("ReverseProxy:KnownProxies").Get<string[]>() ?? [])
            {
                if (!IPAddress.TryParse(knownProxy, out var address))
                {
                    throw new InvalidOperationException($"ReverseProxy:KnownProxies contains an invalid IP address: {knownProxy}");
                }

                options.KnownProxies.Add(address);
            }
        });
        services.AddCors(options => options.AddPolicy(WebClientCorsPolicy, policy =>
        {
            if (allowedOrigins.Length > 0)
                policy.WithOrigins(allowedOrigins).AllowAnyHeader().AllowAnyMethod();
        }));
        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options => ConfigureJwtBearer(options, jwt, environment));
        services.AddAuthorization();
        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            options.AddPolicy("login", context => RateLimitPartition.GetFixedWindowLimiter(
                context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = 5,
                    Window = TimeSpan.FromMinutes(1),
                    QueueLimit = 0
                }));
        });

        services.AddDbContext<ApplicationDbContext>(options => options.UseNpgsql(connectionString));
        services.AddScoped<InventoryService>();
        services.AddScoped<DashboardService>();
        services.AddScoped<ShoppingListService>();
        services.AddScoped<ShoppingReceiptService>();
        services.AddScoped<PrintJobService>();
        services.AddSingleton<PrintJobNotifier>();
        services.AddScoped<JwtTokenService>();
        services.AddSingleton(TimeProvider.System);

        services.AddProblemDetails();
        services.AddControllers().AddJsonOptions(options =>
            options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));
        services.AddEndpointsApiExplorer();
        services.AddSwaggerGen(ConfigureSwagger);
        return services;
    }

    private static JwtOptions ReadAndValidateJwtOptions(IConfiguration configuration)
    {
        var jwt = configuration.GetRequiredSection(JwtOptions.SectionName).Get<JwtOptions>()
            ?? throw new InvalidOperationException("Jwt configuration is required.");

        if (string.IsNullOrWhiteSpace(jwt.Issuer))
            throw new InvalidOperationException("Jwt:Issuer is required.");
        if (string.IsNullOrWhiteSpace(jwt.Audience))
            throw new InvalidOperationException("Jwt:Audience is required.");
        if (Encoding.UTF8.GetByteCount(jwt.Key) < JwtOptions.MinimumKeyLengthInBytes)
            throw new InvalidOperationException($"Jwt:Key must contain at least {JwtOptions.MinimumKeyLengthInBytes} bytes.");

        return jwt;
    }

    private static void ConfigureJwtBearer(
        JwtBearerOptions options,
        JwtOptions jwt,
        IHostEnvironment environment)
    {
        options.RequireHttpsMetadata = !environment.IsDevelopment();
        options.SaveToken = false;
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = jwt.Issuer,
            ValidAudience = jwt.Audience,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.Key)),
            ClockSkew = TimeSpan.FromMinutes(1)
        };
    }

    private static void ConfigureSwagger(Swashbuckle.AspNetCore.SwaggerGen.SwaggerGenOptions options)
    {
        options.SwaggerDoc("v1", new OpenApiInfo { Title = "Home Stock API", Version = "v1" });
        options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
        {
            Name = "Authorization",
            Type = SecuritySchemeType.Http,
            Scheme = "Bearer",
            BearerFormat = "JWT",
            In = ParameterLocation.Header
        });
        options.AddSecurityRequirement(new OpenApiSecurityRequirement
        {
            [new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" }
            }] = []
        });
    }
}
