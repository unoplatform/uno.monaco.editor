using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System.ComponentModel;
using System.Diagnostics;

namespace Monaco.Helpers
{
    /// <summary>
    /// Base control that provides a mechanism to queue property changes until initialization is complete.
    /// This solves the issue where dependency properties set before full initialization don't take effect.
    /// 
    /// Note: CodeEditor currently implements this pattern directly rather than inheriting from this class
    /// because it requires a sealed class with static property callbacks. This class is provided as a
    /// reference implementation for future controls that may need similar functionality.
    /// </summary>
    public abstract class DelayedInitControl : Control, INotifyPropertyChanged
    {
        private readonly Queue<Func<Task>> _propertyChangeQueue = new();
        private readonly object _queueLock = new();
        private bool _isInitializationComplete;

        public event PropertyChangedEventHandler? PropertyChanged;

        /// <summary>
        /// Gets whether initialization is complete and property changes should be processed immediately.
        /// </summary>
        protected bool IsInitializationComplete
        {
            get
            {
                lock (_queueLock)
                {
                    return _isInitializationComplete;
                }
            }
        }

        /// <summary>
        /// Queues a property change action to be executed after initialization, or executes immediately if already initialized.
        /// </summary>
        /// <param name="action">The async action to execute</param>
        protected void QueueOrExecutePropertyChange(Func<Task> action)
        {
            lock (_queueLock)
            {
                if (_isInitializationComplete)
                {
                    // Already initialized, execute immediately (fire and forget)
                    _ = action();
                }
                else
                {
                    // Not yet initialized, queue for later
                    _propertyChangeQueue.Enqueue(action);
                }
            }
        }

        /// <summary>
        /// Marks initialization as complete and replays all queued property changes.
        /// This should be called when the control is fully initialized and ready to process changes.
        /// </summary>
        protected async void CompleteInitialization()
        {
            Queue<Func<Task>> actionsToReplay;

            lock (_queueLock)
            {
                if (_isInitializationComplete)
                {
                    return; // Already completed
                }

                _isInitializationComplete = true;

                if (_propertyChangeQueue.Count == 0)
                {
                    return;
                }

                // Copy the queue to process outside the lock
                actionsToReplay = new Queue<Func<Task>>(_propertyChangeQueue);
                _propertyChangeQueue.Clear();
            }

            // Execute all queued actions
            while (actionsToReplay.Count > 0)
            {
                var action = actionsToReplay.Dequeue();
                try
                {
                    await action();
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Error replaying property change: {ex.Message}");
                    OnPropertyChangeReplayError(ex);
                }
            }
        }

        /// <summary>
        /// Called when an error occurs during property change replay.
        /// Override this to provide custom error handling.
        /// </summary>
        /// <param name="exception">The exception that occurred</param>
        protected virtual void OnPropertyChangeReplayError(Exception exception)
        {
            // Default implementation just logs to debug output
        }

        /// <summary>
        /// Raises the PropertyChanged event.
        /// </summary>
        /// <param name="propertyName">The name of the property that changed</param>
        protected void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
