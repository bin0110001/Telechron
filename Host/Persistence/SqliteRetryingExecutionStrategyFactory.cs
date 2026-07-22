using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage;

namespace Telechron.Host.Persistence;

public sealed class SqliteRetryingExecutionStrategyFactory(ExecutionStrategyDependencies dependencies)
    : IExecutionStrategyFactory
{
    public IExecutionStrategy Create() => new SqliteRetryingExecutionStrategy((TelechronDbContext)dependencies.CurrentContext.Context);
}
