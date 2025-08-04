using Domain.Events;
using System.Collections.Concurrent;

namespace Domain.Events
{
    public static class DomainEvents
    {
        private static readonly ConcurrentBag<IDomainEventHandler> _subscribers = new();
        private static readonly object _lock = new();

        public static void Register(IDomainEventHandler handler)
        {
            if (handler == null) return;
            
            lock (_lock)
            {
                _subscribers.Add(handler);
            }
        }

        public static void Unregister(IDomainEventHandler handler)
        {
            if (handler == null) return;
            
            lock (_lock)
            {
                var newSubscribers = _subscribers.Where(s => !ReferenceEquals(s, handler));
                _subscribers.Clear();
                foreach (var subscriber in newSubscribers)
                {
                    _subscribers.Add(subscriber);
                }
            }
        }

        public static void Raise<T>(T @event) where T : IDomainEvent
        {
            if (@event == null) return;

            var handlers = _subscribers.ToArray(); // Snapshot to avoid enumeration issues
            
            foreach (var handler in handlers)
            {
                try
                {
                    handler.Handle(@event);
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Domain event handler failed: {ex.Message}");
                }
            }
        }

        public static void ClearAllHandlers()
        {
            lock (_lock)
            {
                _subscribers.Clear();
            }
        }
    }

    public interface IDomainEventHandler
    {
        void Handle<T>(T @event) where T : IDomainEvent;
    }
}
