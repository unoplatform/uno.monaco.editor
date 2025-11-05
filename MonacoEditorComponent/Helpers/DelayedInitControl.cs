using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System.Collections.Concurrent;
using System.ComponentModel;

namespace Monaco.Helpers
{
    /// <summary>
    /// Base control that queues property change events until initialization is complete.
    /// This solves the issue where dependency properties set before full initialization don't take effect.
    /// </summary>
    public abstract class DelayedInitControl : Control, INotifyPropertyChanged
    {
        private readonly ConcurrentQueue<QueuedPropertyChange> _propertyChangeQueue = new();
        private bool _isInitializationComplete;
        private readonly object _initLock = new();

        public event PropertyChangedEventHandler? PropertyChanged;

        /// <summary>
        /// Represents a queued property change event
        /// </summary>
        protected class QueuedPropertyChange
        {
            public required DependencyProperty Property { get; init; }
            public required object? OldValue { get; init; }
            public required object? NewValue { get; init; }
        }

        /// <summary>
        /// Gets whether initialization is complete and property changes should be processed immediately.
        /// </summary>
        protected bool IsInitializationComplete
        {
            get
            {
                lock (_initLock)
                {
                    return _isInitializationComplete;
                }
            }
        }

        /// <summary>
        /// Queues a property change if initialization is not complete, otherwise processes it immediately.
        /// </summary>
        /// <param name="property">The dependency property that changed</param>
        /// <param name="oldValue">The old value</param>
        /// <param name="newValue">The new value</param>
        /// <returns>True if the change was processed immediately, false if it was queued</returns>
        protected bool QueueOrProcessPropertyChange(DependencyProperty property, object? oldValue, object? newValue)
        {
            lock (_initLock)
            {
                if (_isInitializationComplete)
                {
                    // Process immediately
                    ProcessPropertyChange(property, oldValue, newValue);
                    return true;
                }
                else
                {
                    // Queue for later
                    _propertyChangeQueue.Enqueue(new QueuedPropertyChange
                    {
                        Property = property,
                        OldValue = oldValue,
                        NewValue = newValue
                    });
                    return false;
                }
            }
        }

        /// <summary>
        /// Marks initialization as complete and replays all queued property changes.
        /// This should be called when the control is fully initialized and ready to process changes.
        /// </summary>
        protected void CompleteInitialization()
        {
            lock (_initLock)
            {
                if (_isInitializationComplete)
                {
                    return; // Already completed
                }

                _isInitializationComplete = true;
            }

            // Replay queued property changes outside the lock to avoid potential deadlocks
            while (_propertyChangeQueue.TryDequeue(out var change))
            {
                ProcessPropertyChange(change.Property, change.OldValue, change.NewValue);
            }
        }

        /// <summary>
        /// Override this method to process property changes for your control.
        /// This will be called either immediately (if initialized) or during replay (after initialization).
        /// </summary>
        /// <param name="property">The dependency property that changed</param>
        /// <param name="oldValue">The old value</param>
        /// <param name="newValue">The new value</param>
        protected abstract void ProcessPropertyChange(DependencyProperty property, object? oldValue, object? newValue);

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
