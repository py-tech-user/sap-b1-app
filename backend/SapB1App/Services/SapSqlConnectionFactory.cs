using System.Diagnostics;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Caching.Memory;
using SapB1App.Interfaces;

namespace SapB1App.Services;

public class SapSqlConnectionFactory : ISapSqlConnectionFactory
{
    private const string WorkingDataSourceCacheKey = "sap:sql:working-datasource";
    private readonly IConfiguration _configuration;
    private readonly ILogger<SapSqlConnectionFactory> _logger;
    private readonly IMemoryCache _cache;

    public SapSqlConnectionFactory(
        IConfiguration configuration,
        ILogger<SapSqlConnectionFactory> logger,
        IMemoryCache cache)
    {
        _configuration = configuration;
        _logger = logger;
        _cache = cache;
    }

    public string BuildConnectionString()
    {
        var sw = Stopwatch.StartNew();
        var server = _configuration["SapB1:SqlServer"];
        if (string.IsNullOrWhiteSpace(server))
            server = _configuration["SapB1:Server"];

        var sqlInstance = _configuration["SapB1:SqlInstance"];
        var sqlPort = _configuration["SapB1:SqlPort"];
        var appConn = _configuration.GetConnectionString("DefaultConnection");
        string? appDataSource = null;
        if (!string.IsNullOrWhiteSpace(appConn))
        {
            try
            {
                var appBuilder = new SqlConnectionStringBuilder(appConn);
                appDataSource = appBuilder.DataSource;
            }
            catch
            {
            }
        }

        var hasExplicitInstanceOrPort = !string.IsNullOrWhiteSpace(sqlInstance) || !string.IsNullOrWhiteSpace(sqlPort) ||
                                       (!string.IsNullOrWhiteSpace(server) && (server.Contains('\\') || server.Contains(',')));

        if (!hasExplicitInstanceOrPort &&
            !string.IsNullOrWhiteSpace(server) &&
            !string.IsNullOrWhiteSpace(appDataSource))
        {
            var normalizedServer = server.Trim().ToLowerInvariant();
            if (normalizedServer is "localhost" or "." or "(local)" &&
                (appDataSource.Contains('\\') || appDataSource.Contains(',')))
            {
                server = appDataSource;
            }
        }

        if (!string.IsNullOrWhiteSpace(server) &&
            !string.IsNullOrWhiteSpace(sqlInstance) &&
            !server.Contains('\\') &&
            !server.Contains(','))
        {
            server = $"{server}\\{sqlInstance}";
        }

        if (!string.IsNullOrWhiteSpace(server) &&
            !string.IsNullOrWhiteSpace(sqlPort) &&
            !server.Contains(',') &&
            !server.Contains('\\'))
        {
            server = $"{server},{sqlPort}";
        }

        if (string.IsNullOrWhiteSpace(server))
        {
            server = appDataSource;
        }

        var database = _configuration["SapB1:SqlCompanyDB"];
        if (string.IsNullOrWhiteSpace(database))
            database = _configuration["SapB1:CompanyDB"];
        if (string.IsNullOrWhiteSpace(database))
            database = _configuration["SapB1ServiceLayer:CompanyDB"];

        var dbUser = _configuration["SapB1:DbUserName"];
        if (string.IsNullOrWhiteSpace(dbUser))
            dbUser = _configuration["SapB1:UserName"];

        var dbPassword = _configuration["SapB1:DbPassword"];
        if (string.IsNullOrWhiteSpace(dbPassword))
            dbPassword = _configuration["SapB1:Password"];

        var useTrusted = bool.TryParse(_configuration["SapB1:UseTrusted"], out var trusted) && trusted;
        var useSqlAuth = !string.IsNullOrWhiteSpace(dbUser);
        var useIntegratedSecurity = useTrusted && !useSqlAuth;

        if (string.IsNullOrWhiteSpace(server) || string.IsNullOrWhiteSpace(database))
            return string.Empty;

        var builder = new SqlConnectionStringBuilder
        {
            DataSource = server,
            InitialCatalog = database,
            TrustServerCertificate = true,
            Encrypt = false,
            IntegratedSecurity = useIntegratedSecurity,
            ConnectTimeout = GetConnectTimeoutSeconds(),
            ConnectRetryCount = 0,
            Pooling = true,
            MinPoolSize = GetMinPoolSize()
        };

        if (useSqlAuth)
        {
            builder.UserID = dbUser;
            builder.Password = dbPassword;
        }

        sw.Stop();
        _logger.LogInformation(
            "[HYBRID-MODE] SQL SAP target resolved. DataSource={DataSource}, Database={Database}, AuthMode={AuthMode}, Pooling={Pooling}, MinPoolSize={MinPoolSize}, ConnectTimeout={ConnectTimeout}, ResolveElapsedMs={ElapsedMs}",
            builder.DataSource,
            builder.InitialCatalog,
            builder.IntegratedSecurity ? "IntegratedSecurity" : "SqlAuth",
            builder.Pooling,
            builder.MinPoolSize,
            builder.ConnectTimeout,
            sw.ElapsedMilliseconds);

        return builder.ConnectionString;
    }

    public async Task<SqlConnection?> OpenConnectionAsync(CancellationToken cancellationToken)
    {
        var totalSw = Stopwatch.StartNew();
        var buildSw = Stopwatch.StartNew();
        var baseConnectionString = BuildConnectionString();
        buildSw.Stop();
        if (string.IsNullOrWhiteSpace(baseConnectionString))
            return null;

        var parseSw = Stopwatch.StartNew();
        var baseBuilder = new SqlConnectionStringBuilder(baseConnectionString);
        var baseDataSource = baseBuilder.DataSource?.Trim() ?? string.Empty;
        parseSw.Stop();
        if (string.IsNullOrWhiteSpace(baseDataSource))
            return null;

        var candidates = new List<string>();
        if (_cache.TryGetValue(WorkingDataSourceCacheKey, out string? cachedDataSource) &&
            !string.IsNullOrWhiteSpace(cachedDataSource))
        {
            candidates.Add(cachedDataSource);
        }

        candidates.Add(baseDataSource);

        var distinctCandidates = candidates
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(x => x.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        _logger.LogInformation(
            "[HYBRID-MODE][OpenSql] Prepared candidates. DataSource={DataSource}, Database={Database}, CandidateCount={CandidateCount}, BuildElapsedMs={BuildElapsedMs}, ParseElapsedMs={ParseElapsedMs}",
            baseBuilder.DataSource,
            baseBuilder.InitialCatalog,
            distinctCandidates.Count,
            buildSw.ElapsedMilliseconds,
            parseSw.ElapsedMilliseconds);

        foreach (var dataSource in distinctCandidates)
        {
            var builder = new SqlConnectionStringBuilder(baseConnectionString)
            {
                DataSource = dataSource
            };

            var createSw = Stopwatch.StartNew();
            var conn = new SqlConnection(builder.ConnectionString);
            createSw.Stop();

            try
            {
                var openSw = Stopwatch.StartNew();
                await conn.OpenAsync(cancellationToken);
                openSw.Stop();
                totalSw.Stop();

                _logger.LogInformation(
                    "[HYBRID-MODE][OpenSql] Connection opened. DataSource={DataSource}, Database={Database}, OpenElapsedMs={OpenElapsedMs}, CreateElapsedMs={CreateElapsedMs}, TotalElapsedMs={TotalElapsedMs}, ClientConnectionId={ClientConnectionId}",
                    dataSource,
                    builder.InitialCatalog,
                    openSw.ElapsedMilliseconds,
                    createSw.ElapsedMilliseconds,
                    totalSw.ElapsedMilliseconds,
                    conn.ClientConnectionId);

                _cache.Set(WorkingDataSourceCacheKey, dataSource, TimeSpan.FromHours(1));
                return conn;
            }
            catch (Exception ex)
            {
                totalSw.Stop();
                await conn.DisposeAsync();
                _logger.LogWarning(
                    "[HYBRID-MODE][OpenSql] Connection failed. DataSource={DataSource}, Database={Database}, TotalElapsedMs={ElapsedMs}, Error={Error}",
                    dataSource,
                    builder.InitialCatalog,
                    totalSw.ElapsedMilliseconds,
                    ex.Message);
                totalSw.Start();
            }
        }

        totalSw.Stop();
        return null;
    }

    public async Task WarmUpAsync(CancellationToken cancellationToken)
    {
        var totalSw = Stopwatch.StartNew();
        var connectionString = BuildConnectionString();
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            _logger.LogWarning("[SAP-SQL-WARMUP] Skipped because SAP SQL connection string is empty.");
            return;
        }

        var builder = new SqlConnectionStringBuilder(connectionString);
        _logger.LogInformation(
            "[SAP-SQL-WARMUP] Starting. DataSource={DataSource}, Database={Database}, Pooling={Pooling}, MinPoolSize={MinPoolSize}, ConnectTimeout={ConnectTimeout}",
            builder.DataSource,
            builder.InitialCatalog,
            builder.Pooling,
            builder.MinPoolSize,
            builder.ConnectTimeout);

        var warmConnectionCount = Math.Max(1, GetMinPoolSize());
        var connections = new List<SqlConnection>(warmConnectionCount);
        try
        {
            for (var i = 0; i < warmConnectionCount; i++)
            {
                var conn = await OpenConnectionAsync(cancellationToken);
                if (conn is null)
                {
                    totalSw.Stop();
                    _logger.LogWarning("[SAP-SQL-WARMUP] Failed to open SAP SQL connection #{Index}. ElapsedMs={ElapsedMs}", i + 1, totalSw.ElapsedMilliseconds);
                    return;
                }

                connections.Add(conn);
            }

            var selectSw = Stopwatch.StartNew();
            foreach (var conn in connections)
            {
                await using var cmd = conn.CreateCommand();
                cmd.CommandText = "SELECT 1";
                cmd.CommandTimeout = 5;
                await cmd.ExecuteScalarAsync(cancellationToken);
            }
            selectSw.Stop();
            totalSw.Stop();

            _logger.LogInformation(
                "[SAP-SQL-WARMUP] Completed. DataSource={DataSource}, Database={Database}, WarmConnections={WarmConnections}, OpenAndSelectElapsedMs={ElapsedMs}, SelectElapsedMs={SelectElapsedMs}",
                connections[0].DataSource,
                connections[0].Database,
                connections.Count,
                totalSw.ElapsedMilliseconds,
                selectSw.ElapsedMilliseconds);
        }
        finally
        {
            foreach (var conn in connections)
                await conn.DisposeAsync();
        }
    }

    private int GetConnectTimeoutSeconds()
    {
        var raw = _configuration["SapB1:SqlConnectTimeoutSeconds"];
        if (!int.TryParse(raw, out var timeout))
            timeout = 5;

        return Math.Clamp(timeout, 5, 60);
    }

    private int GetMinPoolSize()
    {
        var raw = _configuration["SapB1:SqlMinPoolSize"];
        if (!int.TryParse(raw, out var minPoolSize))
            minPoolSize = 5;

        return Math.Clamp(minPoolSize, 1, 20);
    }
}
