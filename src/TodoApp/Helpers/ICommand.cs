using System;

namespace TodoApp.Helpers
{
    public interface ICommand
    {
        Type ResultType { get; }
    }

    public interface ICommand<TResult> : ICommand
    {
        Type ICommand.ResultType => typeof(TResult);
    }
}
