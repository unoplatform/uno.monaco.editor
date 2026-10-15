namespace Monaco.Helpers
{
    using System;
    using System.Threading.Tasks;
    using Microsoft.UI.Dispatching;

    /// <summary>
    /// Extension methods for <see cref="DispatcherQueue"/> to run async tasks.
    /// </summary>
    internal static class DispatcherTaskExtensions
    {
        internal static async Task<T> RunTaskAsync<T>(this DispatcherQueue queue,
            Func<Task<T>> func, DispatcherQueuePriority priority = DispatcherQueuePriority.Normal)
        {
            var taskCompletionSource = new TaskCompletionSource<T>();
            if (!queue.TryEnqueue(priority, async () =>
            {
                try
                {
                    taskCompletionSource.SetResult(await func());
                }
                catch (Exception ex)
                {
                    taskCompletionSource.SetException(ex);
                }
            }))
            {
                taskCompletionSource.SetException(
                    new InvalidOperationException("DispatcherQueue is unavailable or shutting down."));
            }
            return await taskCompletionSource.Task;
        }

        // There is no TaskCompletionSource<void> so we use a bool that we throw away.
        internal static async Task RunTaskAsync(this DispatcherQueue queue,
            Func<Task> func, DispatcherQueuePriority priority = DispatcherQueuePriority.Normal) =>
            await RunTaskAsync(queue, async () => { await func(); return false; }, priority);

        // The EnqueueAsync overloads replace the ones from Uno.CommunityToolkit.WinUI, which is built
        // against Uno 5 and cannot bind on Uno 7. They keep its semantics, which callers rely on:
        // on the queue's own thread the callback runs inline, synchronously, instead of being posted,
        // and a callback exception faults the returned task rather than being thrown.

        /// <summary>Runs <paramref name="action"/> on the queue's thread.</summary>
        internal static Task EnqueueAsync(this DispatcherQueue queue,
            Action action, DispatcherQueuePriority priority = DispatcherQueuePriority.Normal) =>
            queue.EnqueueAsync<bool>(() => { action(); return false; }, priority);

        /// <summary>Runs <paramref name="function"/> on the queue's thread and returns its result.</summary>
        internal static Task<T> EnqueueAsync<T>(this DispatcherQueue queue,
            Func<T> function, DispatcherQueuePriority priority = DispatcherQueuePriority.Normal)
        {
            if (queue.HasThreadAccess)
            {
                try
                {
                    return Task.FromResult(function());
                }
                catch (Exception ex)
                {
                    return Task.FromException<T>(ex);
                }
            }

            var taskCompletionSource = new TaskCompletionSource<T>();
            if (!queue.TryEnqueue(priority, () =>
            {
                try
                {
                    taskCompletionSource.SetResult(function());
                }
                catch (Exception ex)
                {
                    taskCompletionSource.SetException(ex);
                }
            }))
            {
                taskCompletionSource.SetException(
                    new InvalidOperationException("DispatcherQueue is unavailable or shutting down."));
            }
            return taskCompletionSource.Task;
        }

        /// <summary>Runs <paramref name="function"/> on the queue's thread and awaits the task it returns.</summary>
        internal static Task EnqueueAsync(this DispatcherQueue queue,
            Func<Task> function, DispatcherQueuePriority priority = DispatcherQueuePriority.Normal) =>
            queue.EnqueueAsync<bool>(async () => { await function().ConfigureAwait(false); return false; }, priority);

        /// <summary>Runs <paramref name="function"/> on the queue's thread and returns the result of the task it returns.</summary>
        internal static Task<T> EnqueueAsync<T>(this DispatcherQueue queue,
            Func<Task<T>> function, DispatcherQueuePriority priority = DispatcherQueuePriority.Normal)
        {
            if (queue.HasThreadAccess)
            {
                try
                {
                    return function();
                }
                catch (Exception ex)
                {
                    return Task.FromException<T>(ex);
                }
            }

            var taskCompletionSource = new TaskCompletionSource<T>();
            if (!queue.TryEnqueue(priority, async () =>
            {
                try
                {
                    taskCompletionSource.SetResult(await function().ConfigureAwait(false));
                }
                catch (Exception ex)
                {
                    taskCompletionSource.SetException(ex);
                }
            }))
            {
                taskCompletionSource.SetException(
                    new InvalidOperationException("DispatcherQueue is unavailable or shutting down."));
            }
            return taskCompletionSource.Task;
        }
    }
}
