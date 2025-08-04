using Domain.Events;

namespace Domain.Events
{
    public static class AsyncDomainEvents
    {
        private static readonly List<IAsyncDomainEventHandler> _asyncSubscribers = [];
        private static readonly List<IDomainEventHandler> _syncSubscribers = [];

        public static void RegisterAsync(IAsyncDomainEventHandler handler)
        {
            _asyncSubscribers.Add(handler);
        }

        public static void RegisterSync(IDomainEventHandler handler)
        {
            _syncSubscribers.Add(handler);
        }

        public static async Task RaiseAsync<T>(T @event, CancellationToken cancellationToken = default) where T : IDomainEvent
        {
            var tasks = _asyncSubscribers.Select(s => s.HandleAsync(@event, cancellationToken));
            await Task.WhenAll(tasks);

            foreach (var s in _syncSubscribers)
            {
                s.Handle(@event);
            }
        }

        public static void RaiseSync<T>(T @event) where T : IDomainEvent
        {
            foreach (var s in _syncSubscribers)
            {
                s.Handle(@event);
            }

            Task.Run(async () =>
            {
                var tasks = _asyncSubscribers.Select(s => s.HandleAsync(@event));
                await Task.WhenAll(tasks);
            });
        }
    }
}
