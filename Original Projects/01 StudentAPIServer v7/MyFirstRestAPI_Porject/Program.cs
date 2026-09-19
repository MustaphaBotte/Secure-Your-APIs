using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Cors.Infrastructure;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using StudentApi.Authorization;
using System.Text;
using System.Threading.RateLimiting;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddControllers();
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();

builder.Services.AddSwaggerGen(options=>{
    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization",

        Type = SecuritySchemeType.Http,

        Scheme = "Bearer",

        BearerFormat = "JWT",

        In = ParameterLocation.Header,

        Description = "Enter: Bearer {your JWT token}"
    });

    // This tells Swagger that endpoints protected by [Authorize]
    // require the Bearer token defined above.
    options.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                // Reference the previously defined "Bearer" security scheme.
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            // No scopes are required for JWT Bearer authentication.
            // This array is empty because JWT does not use OAuth scopes here.
            new string[] {}
        }
    });
});




builder.Services.AddCors((CorsOptions opt) =>
{
    opt.AddPolicy("StudentAPICorsPolicy", (CorsPolicyBuilder policy) =>
    {
        policy.WithOrigins("https://localhost:7217",
                           "http://localhost:5215")
        .AllowAnyHeader()
        .AllowAnyMethod();
    });
});

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer(
    options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,

            ValidateAudience = true,

            ValidateLifetime = true,

            ValidateIssuerSigningKey = true,

            ValidIssuer = "AtlasSchool",

            ValidAudience = "students",

            IssuerSigningKey =
                 new SymmetricSecurityKey(Encoding.UTF8.GetBytes("THIS_IS_A_VERY_SECRET_KEY_123456")),

            ClockSkew = TimeSpan.Zero

        };
    });

builder.Services.AddSingleton<IAuthorizationHandler, StudentOwnerOrAdminHandler>();

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("StudentOwnerOrAdmin", policy =>
        policy.Requirements.Add(new StudentOwnerOrAdminRequirement()));
});

builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

    options.AddPolicy("AuthLimiter", httpContext =>
    {
    var ip = httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";

    return RateLimitPartition.GetFixedWindowLimiter(
        partitionKey: ip,
        factory: _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = 5,
            Window = TimeSpan.FromMinutes(1),
            QueueLimit = 0
        });

    });
});


var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}


app.UseHttpsRedirection();

app.UseCors("StudentAPICorsPolicy");

app.Use(async (context, next) =>
{
    await next(); // calling the rate limiting middleware

    if (context.Response.StatusCode == StatusCodes.Status429TooManyRequests)
    {
        await context.Response.WriteAsync("Too many login attempts. Please try again later.");
    }
});
app.UseRateLimiter();

app.UseAuthentication();

app.UseAuthorization();

app.MapControllers();

app.Run();
