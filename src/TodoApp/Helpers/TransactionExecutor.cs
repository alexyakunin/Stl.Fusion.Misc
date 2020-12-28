using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Stl.Time;

namespace TodoApp.Helpers
{
    public interface ITransactionExecutor<out TDbContext>
        where TDbContext : DbContext
    {
        Task ReadOnlyTransactionAsync(
            Func<TDbContext, Task> transaction,
            CancellationToken cancellationToken = default);
        Task ReadWriteTransactionAsync(
            ICommand command,
            Func<TDbContext, Task> transaction,
            CancellationToken cancellationToken = default);
    }

    public class TransactionExecutor<TDbContext> : ITransactionExecutor<TDbContext>
        where TDbContext : DbContext
    {
        protected IServiceProvider Services { get; }
        protected IDbContextFactory<TDbContext> DbContextFactory { get; }
        protected IMomentClock Clock { get; }

        protected TransactionExecutor(IServiceProvider services)
        {
            Services = services;
            DbContextFactory = services.GetRequiredService<IDbContextFactory<TDbContext>>();
            Clock = services.GetService<IMomentClock>() ?? SystemClock.Instance;
        }

        public virtual async Task ReadOnlyTransactionAsync(
            Func<TDbContext, Task> transaction,
            CancellationToken cancellationToken = default)
        {
            var dbContext = DbContextFactory.CreateDbContext();
            await using var _1 = dbContext.ConfigureAwait(false);
            dbContext.ConfigureMode(DbContextMode.ReadOnly);
            var strategy = dbContext.Database.CreateExecutionStrategy();

            await strategy.ExecuteInTransactionAsync(dbContext,
                async (dbContext1, _) => await transaction.Invoke(dbContext1).ConfigureAwait(false),
                (dbContext1, _) => Task.FromResult(true),
                cancellationToken);
        }

        public virtual async Task ReadWriteTransactionAsync(
            ICommand command,
            Func<TDbContext, Task> transaction,
            CancellationToken cancellationToken = default)
        {
            var dbContext = DbContextFactory.CreateDbContext();
            await using var _ = dbContext.ConfigureAwait(false);
            dbContext.ConfigureMode(DbContextMode.ReadWrite);
            var strategy = dbContext.Database.CreateExecutionStrategy();

            string recordId = "";
            await strategy.ExecuteInTransactionAsync(dbContext,
                async (dbContext1, _) => {
                    var record = await AddCommandRecordAsync(dbContext1, command, cancellationToken).ConfigureAwait(false);
                    recordId = record.Id;
                    await transaction.Invoke(dbContext1).ConfigureAwait(false);
                },
                async (dbContext1, _) => {
                    var record = await FindCommandRecordAsync(dbContext1, recordId, cancellationToken).ConfigureAwait(false);
                    return record != null;
                },
                cancellationToken);
        }

        // Protected methods

        protected virtual async Task<CommandRecord> AddCommandRecordAsync(
            TDbContext dbContext, ICommand operation, CancellationToken cancellationToken)
        {
            var record = new CommandRecord(Clock.Now.ToDateTime(), operation);
            await dbContext.AddAsync((object) record, cancellationToken).ConfigureAwait(false);
            return record;
        }

        protected virtual Task<CommandRecord?> FindCommandRecordAsync(
            TDbContext dbContext, string id, CancellationToken cancellationToken)
            => dbContext.Set<CommandRecord>().SingleOrDefaultAsync(e => e.Id == id, cancellationToken)!;
    }
}
