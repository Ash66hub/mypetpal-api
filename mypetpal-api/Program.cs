using Microsoft.EntityFrameworkCore;
using mypetpal.dbContext;
using mypetpal_api.Services;
using mypetpal.Services.Contracts;

var builder = WebApplication.CreateBuilder(args);

// Load configuration based on the environment before building the app.
builder.Configuration.AddEnvironmentVariables();

if (builder.Environment.IsDevelopment())
{
    builder.Configuration.AddJsonFile("appsettings.Development.json", optional: false, reloadOnChange: true);
}

// Add services to the container.
var connection = builder.Configuration.GetConnectionString("AZURE_SQL_CONNECTIONSTRING");

// Register ApplicationDbContext with dependency injection
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(connection));

// Add controllers for API
builder.Services.AddControllers();

// Add Swagger services
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Register Services so that they can be injected as needed
builder.Services.AddScoped<IUserService, UserService>();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}
else
{
    app.UseExceptionHandler("/Error");
}

app.UseRouting();

app.UseAuthorization();

app.MapControllers();


app.Run();
