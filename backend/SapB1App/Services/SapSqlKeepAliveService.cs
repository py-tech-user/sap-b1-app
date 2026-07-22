using SapB1App.Interfaces;

namespace SapB1App.Services;

public class SapSqlKeepAliveService : BackgroundService
{
    private readonly ISapSqlConnectionFactory _connectionFactory;
    private readonly IConfiguration _configuration;
    private readonly ILogger<SapSqlKeepAliveService> _logger;

    public SapSqlKeepAliveService(
        ISapSqlConnectionFactory connectionFactory,
        IConfiguration configuration,
        ILogger<SapSqlKeepAliveService> logger)
    {
        _connectionFactory = connectionFactory;
        _configuration = configuration;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_configuration.GetValue("SapB1:SqlKeepAliveEnabled", true))
        {
            _logger.LogInformation("[SAP-SQL-KEEPALIVE] Disabled by configuration.");
            return;
        }

        var intervalSeconds = Math.Clamp(
            _configuration.GetValue("SapB1:SqlKeepAliveIntervalSeconds", 30),
            10,
            300);
        var interval = TimeSpan.FromSeconds(intervalSeconds);

        _logger.LogInformation("[SAP-SQL-KEEPALIVE] Started. IntervalSeconds={IntervalSeconds}", intervalSeconds);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await _connectionFactory.WarmUpAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "[SAP-SQL-KEEPALIVE] Warm-up ping failed.");
            }

            try
            {
                await Task.Delay(interval, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
        }
    }
}
