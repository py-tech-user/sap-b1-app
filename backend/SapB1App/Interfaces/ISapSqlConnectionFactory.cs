using Microsoft.Data.SqlClient;

namespace SapB1App.Interfaces;

public interface ISapSqlConnectionFactory
{
    string BuildConnectionString();
    Task<SqlConnection?> OpenConnectionAsync(CancellationToken cancellationToken);
    Task WarmUpAsync(CancellationToken cancellationToken);
}
