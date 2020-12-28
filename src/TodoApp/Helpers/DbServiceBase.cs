using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Stl.Time;

namespace TodoApp.Helpers
{
    public abstract class DbServiceBase<TDbContext>
        where TDbContext : DbContext
    {
        protected IServiceProvider Services { get; }
        protected IDbContextFactory<TDbContext> DbContextFactory { get; }
        protected ITransactionExecutor<TDbContext> TransactionExecutor { get; }
        protected IMomentClock Clock { get; }

        protected DbServiceBase(IServiceProvider services)
        {
            Services = services;
            DbContextFactory = services.GetRequiredService<IDbContextFactory<TDbContext>>();
            TransactionExecutor = services.GetRequiredService<ITransactionExecutor<TDbContext>>();
            Clock = services.GetService<IMomentClock>() ?? SystemClock.Instance;
        }

        protected virtual TDbContext CreateDbContext(DbContextMode mode = DbContextMode.ReadOnly)
        {
            var dbContext = DbContextFactory.CreateDbContext();
            dbContext.ConfigureMode(mode);
            return dbContext;
        }

        protected Task ReadOnlyTransactionAsync(
            Func<TDbContext, Task> transaction,
            CancellationToken cancellationToken = default)
            => TransactionExecutor.ReadOnlyTransactionAsync(transaction, cancellationToken);
    }
}
