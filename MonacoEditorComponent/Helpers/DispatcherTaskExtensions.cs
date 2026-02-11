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
            queue.TryEnqueue(priority, async () =>
            {
                try
                {
                    taskCompletionSource.SetResult(await func());
                }
                catch (Exception ex)
                {
                    taskCompletionSource.SetException(ex);
                }
            });
            return await taskCompletionSource.Task;
        }

        // There is no TaskCompletionSource<void> so we use a bool that we throw away.
        internal static async Task RunTaskAsync(this DispatcherQueue queue,
            Func<Task> func, DispatcherQueuePriority priority = DispatcherQueuePriority.Normal) =>
            await RunTaskAsync(queue, async () => { await func(); return false; }, priority);
    }
}
