using Amazon;
using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.DataModel;
using Amazon.S3;
using COMP306_Group15_OpenLectureAPI.Data;
using COMP306_Group15_OpenLectureAPI.Mapping;
using COMP306_Group15_OpenLectureAPI.Models;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

namespace COMP306_Group15_OpenLectureAPI
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            // Controllers
            builder.Services.AddControllers();

            // Swagger (explicit Bearer header)
            builder.Services.AddEndpointsApiExplorer();
            builder.Services.AddSwaggerGen(c =>
            {
                c.SwaggerDoc("v1", new OpenApiInfo { Title = "OpenLecture API", Version = "v1" });

                c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
                {
                    Description = "JWT in the Authorization header. Format: **Bearer {token}**",
                    Name = "Authorization",
                    In = ParameterLocation.Header,
                    Type = SecuritySchemeType.ApiKey,
                    Scheme = "Bearer"
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

            // AutoMapper
            builder.Services.AddAutoMapper(typeof(MappingProfile));

            // AWS clients (region from appsettings)
            var regionName = builder.Configuration["AWS:Region"] ?? "us-east-1";
            var region = RegionEndpoint.GetBySystemName(regionName);
            builder.Services.AddSingleton<IAmazonDynamoDB>(_ => new AmazonDynamoDBClient(region));
            builder.Services.AddSingleton<IDynamoDBContext, DynamoDBContext>();
            builder.Services.AddSingleton<IAmazonS3>(_ => new AmazonS3Client(region));

            // Repositories
            builder.Services.AddScoped<IDynamoRepo<UserItem>, DynamoRepo<UserItem>>();
            builder.Services.AddScoped<IDynamoRepo<VideoItem>, DynamoRepo<VideoItem>>();
            builder.Services.AddScoped<IDynamoRepo<CommentItem>, DynamoRepo<CommentItem>>();
            builder.Services.AddScoped<IDynamoRepo<ReactionItem>, DynamoRepo<ReactionItem>>();
            builder.Services.AddScoped<IDynamoRepo<PlaylistItem>, DynamoRepo<PlaylistItem>>();

            // JWT Authentication
            var issuer = builder.Configuration["Jwt:Issuer"];
            var audience = builder.Configuration["Jwt:Audience"];
            var key = builder.Configuration["Jwt:Key"] ?? throw new InvalidOperationException("Missing Jwt:Key");
            var signingKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key));

            builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
                .AddJwtBearer(o =>
                {
                    o.RequireHttpsMetadata = false;   // local/dev
                    o.SaveToken = true;

                    o.TokenValidationParameters = new TokenValidationParameters
                    {
                        ValidateIssuer = true,
                        ValidateAudience = true,
                        ValidateIssuerSigningKey = true,
                        ValidateLifetime = true,
                        ValidIssuer = issuer,
                        ValidAudience = audience,
                        IssuerSigningKey = signingKey,
                        ClockSkew = TimeSpan.FromMinutes(2),

                        // Be explicit about which claims represent role/name
                        RoleClaimType = ClaimTypes.Role,
                        NameClaimType = JwtRegisteredClaimNames.Sub
                    };

                    // Diagnostics: prints exact reason if token fails
                    o.Events = new JwtBearerEvents
                    {
                        OnAuthenticationFailed = ctx =>
                        {
                            Console.WriteLine("[JWT] AuthenticationFailed: " + ctx.Exception.Message);
                            if (ctx.Exception.InnerException != null)
                                Console.WriteLine("[JWT] Inner: " + ctx.Exception.InnerException.Message);
                            return Task.CompletedTask;
                        },
                        OnChallenge = ctx =>
                        {
                            // Triggered when token missing/invalid
                            Console.WriteLine("[JWT] Challenge: " + ctx.Error + " | " + ctx.ErrorDescription);
                            return Task.CompletedTask;
                        },
                        OnMessageReceived = ctx =>
                        {
                            if (string.IsNullOrWhiteSpace(ctx.Token))
                                Console.WriteLine("[JWT] No token found for " + ctx.Request.Method + " " + ctx.Request.Path);
                            return Task.CompletedTask;
                        }
                    };
                });

            // CORS (open for demo)
            builder.Services.AddCors(opt =>
            {
                opt.AddPolicy("AllowAll", p => p.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod());
            });

            var app = builder.Build();

            if (app.Environment.IsDevelopment())
            {
                app.UseSwagger();
                app.UseSwaggerUI(c =>
                {
                    c.SwaggerEndpoint("/swagger/v1/swagger.json", "OpenLecture API v1");
                    c.EnablePersistAuthorization(); // keep JWT in Swagger UI
                });
            }

            app.UseHttpsRedirection();
            app.UseCors("AllowAll");
            app.UseAuthentication();
            app.UseAuthorization();

            app.MapControllers();
            app.MapGet("/", () => "OpenLecture API is running");

            app.Run();
        }
    }
}
