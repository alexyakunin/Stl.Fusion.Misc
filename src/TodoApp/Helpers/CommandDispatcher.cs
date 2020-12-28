using System.Threading;
using System.Threading.Tasks;

namespace TodoApp.Helpers
{
    public interface ICommandDispatcher
    {
        Task InvokeAsync(ICommand command, CancellationToken cancellationToken = default);
    }

    public class CommandDispatcher : ICommandDispatcher
    {
        public async Task InvokeAsync(ICommand command, CancellationToken cancellationToken = default)
        {
            throw new System.NotImplementedException();
        }
    }
}
