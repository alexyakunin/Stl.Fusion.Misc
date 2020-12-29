using System;
using System.Threading;
using System.Threading.Tasks;

namespace TodoApp.Helpers
{
    public interface ICommandHandler<in TCommand>
        where TCommand : class, ICommand
    {
        Task OnCommandAsync(TCommand command, Func<Task> next, CancellationToken cancellationToken);
    }
}
