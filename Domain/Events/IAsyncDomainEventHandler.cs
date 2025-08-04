using Domain.Events;

namespace Domain.Events
{
    public interface IAsyncDomainEventHandler
    {
        Task HandleAsync<T>(T @event, CancellationToken cancellationToken = default) where T : IDomainEvent;
    }
}
