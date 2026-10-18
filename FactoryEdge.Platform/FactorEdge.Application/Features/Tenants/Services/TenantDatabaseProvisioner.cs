using FactorEdge.Application.Interfaces.Services.Tenants;
using Microsoft.Extensions.Configuration;
using Npgsql;

namespace FactorEdge.Application.Features.Tenants.Services;

public sealed class TenantDatabaseProvisioner(IConfiguration configuration) : ITenantDatabaseProvisioner
{
    public async Task<bool> CreateDatabaseAsync(string databaseName, CancellationToken cancellationToken = default)
    {
        ValidateDatabaseName(databaseName);

        await using var connection = CreateAdminConnection();

        await connection.OpenAsync(cancellationToken);

        var quotedName = new NpgsqlCommandBuilder().QuoteIdentifier(databaseName);

        await using var command = new NpgsqlCommand($"CREATE DATABASE {quotedName}", connection);

        await command.ExecuteNonQueryAsync(cancellationToken);

        return true;
    }

    public async Task DeleteDatabaseAsync(string databaseName, CancellationToken cancellationToken = default)
    {
        ValidateDatabaseName(databaseName);

        await using var connection = CreateAdminConnection();

        await connection.OpenAsync(cancellationToken);

        var quotedName = new NpgsqlCommandBuilder().QuoteIdentifier(databaseName);

        await using var command = new NpgsqlCommand(
            $"DROP DATABASE IF EXISTS {quotedName} WITH (FORCE)",
            connection);

        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private NpgsqlConnection CreateAdminConnection()
    {
        var connectionString = configuration
            .GetConnectionString("FactorEdgeBaseDatabase")
            ?? throw new InvalidOperationException(
                "Base database connection string not found.");

        var builder = new NpgsqlConnectionStringBuilder(connectionString)
        {
            Database = "postgres"
        };

        return new NpgsqlConnection(builder.ConnectionString);
    }

    private static void ValidateDatabaseName(string databaseName)
    {
        const string prefix = "factoredge_tenant_";

        if (string.IsNullOrWhiteSpace(databaseName) || !databaseName.StartsWith(prefix, StringComparison.Ordinal) || !Guid.TryParseExact(databaseName[prefix.Length..], "N", out _))
        {
            throw new ArgumentException("Invalid tenant database name.", nameof(databaseName));
        }
    }
}
