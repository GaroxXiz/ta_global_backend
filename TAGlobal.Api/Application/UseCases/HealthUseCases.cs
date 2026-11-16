using TAGlobal.Api.Application.Abstractions;

namespace TAGlobal.Api.Application.UseCases;

public interface IHealthUseCases
{
    Task<bool> IsDatabaseAvailableAsync(CancellationToken cancellationToken);
}

public sealed class HealthUseCases(IHealthRepository repository) : IHealthUseCases
{
    public Task<bool> IsDatabaseAvailableAsync(CancellationToken cancellationToken) => repository.CanConnectAsync(cancellationToken);
}
