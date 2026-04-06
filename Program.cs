using Microsoft.EntityFrameworkCore;
using Npgsql.EntityFrameworkCore.PostgreSQL;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using mypetpal.dbContext;
using mypetpal.Services;
using mypetpal.Services.Contracts;
using System.Text;
using Microsoft.OpenApi.Models;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.SignalR;
using mypetpal.Hubs;
using mypetpal.Models;
using mypetpal.MinimalApiEndpoints;


var builder = WebApplication.CreateBuilder(args);


// Allow CORS for now. Later limit by appUrl
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll",
        policy =>
        {
            policy.AllowAnyHeader() 
                  .AllowAnyMethod()
                  .SetIsOriginAllowed(_ => true) // Required for SignalR with allow credentials
                  .AllowCredentials(); 
        });
});

// Load configuration based on the environment before building the app.
builder.Configuration
    .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
    .AddJsonFile(
        $"appsettings.{builder.Environment.EnvironmentName}.json",
        optional: true,
        reloadOnChange: true)
    .AddEnvironmentVariables();

// Add services to the container.
var connection = builder.Configuration.GetConnectionString("DefaultConnection");

// Register ApplicationDbContext with dependency injection
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseNpgsql(connection));

// Add controllers for API
builder.Services.AddControllers().AddJsonOptions(options =>
{
    options.JsonSerializerOptions.PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase;
    options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
});

builder.Services.AddSignalR();
builder.Services.AddMemoryCache();

// Add Swagger services
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "MyPetPal-Api",
        Version = "v1"
    });

    // Define the BearerAuth scheme
    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        In = ParameterLocation.Header,
        Description = "Enter JWT with Bearer prefix in this format: Bearer {token}",
        Name = "Authorization",  
        Type = SecuritySchemeType.ApiKey,
        Scheme = "Bearer"
    });

    // Apply Bearer token globally
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

// Register Services so that they can be injected as needed
builder.Services.AddScoped<IUserService, UserService>();
builder.Services.AddScoped<IEmailService, SmtpEmailService>();
builder.Services.AddSingleton<IPasswordResetCodeStore, PasswordResetCodeStore>();
builder.Services.AddScoped<IProfilePictureStorageService, ProfilePictureStorageService>();
builder.Services.AddScoped<IPetService, PetService>();
builder.Services.AddScoped<IDecorService, DecorService>();
builder.Services.AddScoped<IUserSettingsService, UserSettingsService>();
builder.Services.AddScoped<IExperienceService, ExperienceService>();
builder.Services.AddScoped<IFriendshipService, FriendshipService>();
builder.Services.AddHostedService<PlayerExperienceWorker>();
builder.Services.AddHttpClient();

// JWT Authorization
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidIssuer = builder.Configuration["Jwt:Issuer"],
            ValidAudience = builder.Configuration["Jwt:Audience"],
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(builder.Configuration["Jwt:Key"]!))
        };
    });
   
 

builder.Services.AddAuthorization();

var app = builder.Build();

app.UseSwagger();
app.UseSwaggerUI();

app.UseRouting();
app.UseCors("AllowAll");

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
app.MapHub<SocialHub>("/socialHub");
app.MapHub<GameHub>("/gameHub");
app.MapSocialEndpoints();

// This allows both standard browser (GET) and pings (HEAD)
app.MapMethods("/health", new[] { "GET", "HEAD" }, () => Results.Ok("Healthy"));

// DB migration.
if (app.Environment.IsProduction()) 
{
    using (var scope = app.Services.CreateScope())
    {
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        db.Database.Migrate();
    }
}

app.Run();
