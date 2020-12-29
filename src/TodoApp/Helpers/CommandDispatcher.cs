using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using TodoApp.Helpers.Internal;

namespace TodoApp.Helpers
{
    public interface ICommandDispatcher
    {
        Task InvokeAsync(ICommand command, CancellationToken cancellationToken = default);
    }

    public class CommandDispatcher : ICommandDispatcher
    {
        protected IServiceProvider Services { get; }
        protected ICommandHandlerResolver HandlerResolver { get; }
        protected ILogger Log { get; }

        public CommandDispatcher(
            IServiceProvider services,
            ICommandHandlerResolver handlerResolver,
            ILogger<CommandDispatcher>? log = null)
        {
            Log = log ??= NullLogger<CommandDispatcher>.Instance;
            Services = services;
            HandlerResolver = handlerResolver;
        }

        public Task InvokeAsync(ICommand command, CancellationToken cancellationToken = default)
        {
            var handlers = HandlerResolver.GetCommandHandlers(command.GetType());
            if (handlers.Count == 0) {
                Log.LogWarning($"No handler(s) found for {command}.");
                return Task.CompletedTask;
            }

            Func<Task> next = null!;
            var handlerIndex = 0;
            Task Next() {
                if (handlerIndex >= handlers!.Count)
                    return Task.CompletedTask;
                var handler = handlers[handlerIndex++];
                // ReSharper disable once AccessToModifiedClosure
                return handler.InvokeAsync(Services, command, next, cancellationToken);
            }
            next = Next;
            return next.Invoke();
        }
    }
}
