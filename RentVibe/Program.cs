using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi;
using RentVibe.Data;
using RentVibe.Hubs;
using RentVibe.Models;
using RentVibe.Services;
using RentVibe.GraphQL.Mutations;
using RentVibe.GraphQL.Queries;
using RentVibe.Services.Caching;
using RentVibe.Data.Repositories;


var builder = WebApplication.CreateBuilder(args);


builder.Services.AddControllers();


builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo { Title = "RentVibe API", Version = "v1" });
    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Description = "JWT token — enter: Bearer {token}",
        Name = "Authorization",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.ApiKey,
        Scheme = "Bearer"
    });
    c.AddSecurityRequirement(doc => new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecuritySchemeReference("Bearer", doc),
            new List<string>()
        }
    });
});


builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));


builder.Services.AddIdentity<ApplicationUser, IdentityRole>(options =>
{
    options.Password.RequireDigit = true;
    options.Password.RequiredLength = 6;
    options.Password.RequireNonAlphanumeric = false;
    options.Password.RequireUppercase = true;
    options.Password.RequireLowercase = true;
})
.AddEntityFrameworkStores<AppDbContext>()
.AddDefaultTokenProviders();


var jwtSecret = builder.Configuration["JwtSettings:Secret"]!;
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
        ValidIssuer = builder.Configuration["JwtSettings:Issuer"],
        ValidAudience = builder.Configuration["JwtSettings:Audience"],
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSecret))
    };

    
    options.Events = new JwtBearerEvents
    {
        OnMessageReceived = context =>
        {
            var accessToken = context.Request.Query["access_token"];
            var path = context.HttpContext.Request.Path;
            if (!string.IsNullOrEmpty(accessToken) && path.StartsWithSegments("/notificationHub"))
            {
                context.Token = accessToken;
            }
            return Task.CompletedTask;
        }
    };
});


builder.Services.AddAuthorizationBuilder()
    .AddPolicy("AdminOnly", p => p.RequireRole("Admin"))
    .AddPolicy("LandlordOnly", p => p.RequireRole("Landlord"))
    .AddPolicy("TenantOnly", p => p.RequireRole("Tenant"))
    .AddPolicy("LandlordOrAdmin", p => p.RequireRole("Admin", "Landlord"));


builder.Services.AddSignalR();


builder.Services.AddScoped(typeof(DataRepository<>), typeof(DataRepository<>));
builder.Services.AddScoped<PropertyRepository>();
builder.Services.AddScoped<RentalApplicationRepository>();
builder.Services.AddScoped<VisitRepository>();
builder.Services.AddScoped<FavoriteRepository>();
builder.Services.AddScoped<ReviewRepository>();
builder.Services.AddScoped<NotificationRepository>();
builder.Services.AddScoped<ApprovalRepository>();


builder.Services.AddScoped<NotificationService>();

builder.Services
    .AddGraphQLServer()
    .AddAuthorization()
    .AddQueryType<PropertyQueries>()
    .AddMutationType<PropertyMutations>()
    .ModifyRequestOptions(opt => opt.IncludeExceptionDetails = true);



builder.Services.AddMemoryCache();
builder.Services.AddHttpContextAccessor();
builder.Services.Configure<CacheSettings>(builder.Configuration.GetSection("CacheSettings"));
builder.Services.AddStackExchangeRedisCache(options =>
{
    options.Configuration = builder.Configuration.GetConnectionString("Redis")
                            ?? builder.Configuration["Redis:Configuration"]
                            ?? "localhost:6379";
});
builder.Services.AddSingleton<CacheKeyBuilder>();
builder.Services.AddSingleton<ICacheTagStore, DistributedCacheTagStore>();
builder.Services.AddSingleton<IMultiLevelCache, MultiLevelCache>();



builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
        policy.WithOrigins("http://localhost:5173", "http://localhost:5139")
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials());
});

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

app.UseSwagger();
app.UseSwaggerUI(c =>
{
    c.SwaggerEndpoint("/swagger/v1/swagger.json", "RentVibe API v1");
});

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseWebSockets();
app.UseRouting();
app.UseCors();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
app.MapHub<NotificationHub>("/notificationHub");
app.MapGraphQL("/graphql");


app.MapFallbackToFile("index.html");

app.Run();