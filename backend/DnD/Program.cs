using DataAccess;
using DataAccess.DependencyInjection;
using DnD.Data;
using Microsoft.AspNetCore.Identity;
using Domain.Entities.User;
using DnD.GraphQL;
using Services.Implementation.Extensions;
using System.Security.Claims;
using Services.Abstractions;
using Services.Implementation;
using MassTransit;
using static DnD.Data.WebApplicationExtensions;
using Serilog;
using Serilog.Events;
using Serilog.Sinks.PostgreSQL;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Hosting;

namespace DnD;

public class Program
{
    public static async Task Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);
        var configuration = builder.Configuration;
        var services = builder.Services;

        var mongoDbSettings = configuration.GetSection(nameof(MongoDbSettings))?.Get<MongoDbSettings>() 
            ?? throw new ArgumentNullException($"Provide {nameof(MongoDbSettings)}.");

        services
                .AddIdentity<User, UserRole>(options =>
                {
                    options.SignIn.RequireConfirmedEmail = true;
                    options.SignIn.RequireConfirmedAccount = true;
                    options.User.RequireUniqueEmail = true;
                    options.User.AllowedUserNameCharacters = new string(Enumerable.Range(65, 25)
                        .Select(upperEnglishLetterCode => (char)upperEnglishLetterCode)
                        .Concat(Enumerable.Range(97, 25)
                            .Select(lowerEnglishLetterCode => (char)lowerEnglishLetterCode)
                        )
                        .Concat(Enumerable.Range(48, 10)
                            .Select(numberCode => (char)numberCode)
                        )
                        .Concat(['_', '.', '@',])
                        .ToArray()
                    );
                    options.ClaimsIdentity.UserIdClaimType = ClaimTypes.NameIdentifier;
                })
                .AddMongoDbStores<User, UserRole, Guid>(mongoDbSettings.GetConnectionString(), Constants.DATABASE_NAME)
                .AddDefaultTokenProviders();

        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Override("Microsoft", LogEventLevel.Information)
            .Enrich.FromLogContext()
            .WriteTo.Console()
            .WriteTo.PostgreSQL(
                connectionString: configuration.GetConnectionString("LogsPostgresql"),
                tableName: "DnDServiceLogger",
                needAutoCreateTable: true)
            .CreateLogger();
        builder.Host.UseSerilog();

        services.AddLogging(x => x.AddConsole().AddDebug());

        if (builder.Environment.IsDevelopment())
        {
            services.AddCors(options =>
            {
                var allowHosts = configuration.GetValue<string>("CorsHost") ?? "http://localhost:3000";
                options.AddPolicy("DevFrontEnds",
                    builder =>
                        builder.WithOrigins(allowHosts)
                               .AllowAnyMethod()
                               .AllowAnyHeader()
                               .AllowCredentials()
                            .AllowAnyHeader()
                );
            });
        }

        services.AddSignalR();
        services.AddGraphQlApi();
        services.AddControllers();

        services.RegisterDatabaseServices(mongoDbSettings);
        services.AddDomainServicesImplementations(configuration);

        var app = builder.Build();

        if (app.Environment.IsDevelopment())
        {
            app.UseCors("DevFrontEnds");
        }

        app.UseRouting()
           .UseAuthentication()
           .UseAuthorization();

        app.MapHub<GameHub.GameHub>("/gamehub");
        app.MapControllers();
        app.MapGraphQL();

        if (configuration.IsDataSeedRequested())
        {
            await app.MigrateDatabaseAsync();
        }

        await app.RunAsync();
    }
}