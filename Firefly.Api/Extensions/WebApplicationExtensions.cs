using Firefly.Api.Models;
using Firefly.Api.Seeders;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.EntityFrameworkCore;

namespace Firefly.Api.Extensions;

public static class WebApplicationExtensions
{
    public static void LoadDotEnv()
    {
        var currentDirectory = new DirectoryInfo(AppDomain.CurrentDomain.BaseDirectory);
        string? envFilePath = null;

        while (currentDirectory != null && !File.Exists(envFilePath = Path.Combine(currentDirectory.FullName, ".env")))
        {
            currentDirectory = currentDirectory.Parent;
        }

        if (envFilePath != null && File.Exists(envFilePath))
        {
            DotNetEnv.Env.Load(envFilePath);
        }
        else
        {
            Console.WriteLine("OSTRZEŻENIE: Nie znaleziono pliku .env.");
        }
    }

    public static WebApplication UseCustomMiddlewarePipeline(this WebApplication app)
    {
        var forwardedHeadersOptions = new ForwardedHeadersOptions
        {
            ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto,
            ForwardLimit = 1
        };

        if (string.Equals(app.Configuration["TRUST_FORWARDED_HEADERS"], "true", StringComparison.OrdinalIgnoreCase))
        {
            forwardedHeadersOptions.KnownNetworks.Clear();
            forwardedHeadersOptions.KnownProxies.Clear();
        }

        app.UseForwardedHeaders(forwardedHeadersOptions);
        app.UseExceptionHandler();
        app.UseStatusCodePages();
        app.UseResponseCompression();

        var policyCollection = new HeaderPolicyCollection()
            .AddDefaultApiSecurityHeaders()
            .AddContentSecurityPolicy(csp =>
            {
                csp.AddDefaultSrc().Self();

                csp.AddImgSrc()
                    .Self()
                    .From("https:")
                    .Blob()
                    .Data();

                var connectSrc = csp.AddConnectSrc().Self();
                if (app.Environment.IsDevelopment())
                {
                    connectSrc.From("https://localhost:7128");
                }

                var issuer = app.Configuration["JWT:Issuer"] ?? app.Configuration["JWT__Issuer"];
                if (!string.IsNullOrEmpty(issuer))
                {
                    connectSrc.From(issuer);
                }

                if (app.Environment.IsDevelopment())
                {
                    csp.AddStyleSrc().Self().UnsafeInline();
                    csp.AddScriptSrc().Self().UnsafeInline();
                }
                else
                {
                    csp.AddStyleSrc().Self();
                    csp.AddScriptSrc().Self();
                }
            })
            .AddCustomHeader("X-Permitted-Cross-Domain-Policies", "none")
            .AddPermissionsPolicy(p =>
            {
                p.AddCamera().None();
                p.AddMicrophone().None();
                p.AddGeolocation().None();
            })
            .RemoveServerHeader();

        app.UseSecurityHeaders(policyCollection);

        if (app.Environment.IsDevelopment())
        {
            app.UseSwagger();
            app.UseSwaggerUI();
        }

        app.UseCors();
        app.UseAuthentication();
        app.UseAuthorization();
        app.UseRateLimiter();

        app.MapHealthChecks("/healthz");
        app.MapControllers();

        return app;
    }

    public static async Task InitializeDatabaseAsync(this WebApplication app)
    {
        using var scope = app.Services.CreateScope();
        var ctx = scope.ServiceProvider.GetRequiredService<FireflyDbContext>();
        await ctx.Database.MigrateAsync();
        var seedManager = scope.ServiceProvider.GetRequiredService<SeedManager>();
        await seedManager.Seed();
    }
}