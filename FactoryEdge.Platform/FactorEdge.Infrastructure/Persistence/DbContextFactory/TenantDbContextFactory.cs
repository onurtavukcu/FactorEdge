using FactorEdge.Infrastructure.DbContexts;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Npgsql;

namespace FactorEdge.Infrastructure.Persistence.DbContextFactory
{
    public sealed class TenantDbContextFactory(IConfiguration configuration)
    {
        public TenantDbContext Create(string databaseName)
        {
            if (!databaseName.StartsWith("factoredge_tenant_", StringComparison.Ordinal) ||
                !Guid.TryParseExact(
                    databaseName["factoredge_tenant_".Length..],
                    "N",
                    out _))
            {
                throw new ArgumentException(
                    "Invalid tenant database name.",
                    nameof(databaseName));
            }

            var baseConnectionString = configuration.GetConnectionString("FactorEdgeBaseDatabase") ?? throw new InvalidOperationException("Base connection string not found.");

            var builder = new NpgsqlConnectionStringBuilder(baseConnectionString)
            {
                Database = databaseName
            };

            var options = new DbContextOptionsBuilder<TenantDbContext>()
                .UseNpgsql(builder.ConnectionString)
                .Options;

            return new TenantDbContext(options);
        }
    }
}
