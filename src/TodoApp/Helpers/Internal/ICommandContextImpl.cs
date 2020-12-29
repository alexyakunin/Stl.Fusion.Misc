using System;
using System.Threading;

namespace TodoApp.Helpers.Internal
{
    public interface ICommandContextImpl
    {
        void TrySetDefaultResult();
        void TrySetException(Exception exception);
        void TrySetCancelled(CancellationToken cancellationToken);
    }
}
