using Firefly.Api.Extensions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Firefly.Api.Models;

public class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<FireflyDbContext>
{
    public FireflyDbContext CreateDbContext(string[] args)
    {
        WebApplicationExtensions.LoadDotEnv();

        var configuration = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json", optional: true)
            .AddJsonFile("appsettings.Development.json", optional: true)
            .AddEnvironmentVariables()
            .Build();

        var rawConnectionString = configuration.GetConnectionString("DefaultConnection_LOCAL");

        if (string.IsNullOrWhiteSpace(rawConnectionString))
        {
            throw new InvalidOperationException(
                "Nie można odnaleźć connection stringa 'DefaultConnection_LOCAL' dla narzędzi Design-Time. " +
                "Upewnij się, że jest on zdefiniowany w pliku .env lub appsettings."
            );
        }

        var connectionString = ConnectionStringConverter.Convert(rawConnectionString);

        var builder = new DbContextOptionsBuilder<FireflyDbContext>();
        builder.UseNpgsql(connectionString);

        return new FireflyDbContext(builder.Options);
    }
}