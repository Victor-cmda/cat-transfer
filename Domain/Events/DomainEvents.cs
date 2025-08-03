using Domain.Events;

namespace Domain.Events
{
    public static class DomainEvents
    {
        private static readonly List<IDomainEventHandler> _subscribers = [];

        public static void Register(IDomainEventHandler handler)
        {
            _subscribers.Add(handler);
        }

        public static void Raise<T>(T @event) where T : IDomainEvent
        {
            foreach (var s in _subscribers)
            {
                s.Handle(@event);
            }
        }
    }

    public interface IDomainEventHandler
    {
        void Handle<T>(T @event) where T : IDomainEvent;
    }
}
