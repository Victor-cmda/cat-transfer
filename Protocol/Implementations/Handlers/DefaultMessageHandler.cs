using Protocol.Contracts;
using Protocol.Definitions;
using Domain.ValueObjects;
using System.Collections.Concurrent;

namespace Protocol.Implementations.Handlers;

public class DefaultMessageHandler : IMessageHandler
{
    private readonly NodeId _localNodeId;
    private readonly ConcurrentDictionary<Type, IMessageProcessor> _processors = new();

    public event EventHandler<MessageProcessedEventArgs>? MessageProcessed;
    public event EventHandler<MessageHandlerErrorEventArgs>? HandlerError;

    public DefaultMessageHandler(NodeId localNodeId)
    {
        _localNodeId = localNodeId;
    }

    public async Task HandleMessageAsync(IProtocolMessage message, NodeId sourceNodeId, string connectionId, CancellationToken cancellationToken = default)
    {
        try
        {
            var messageType = message.GetType();
            if (_processors.TryGetValue(messageType, out var processor))
            {
                var context = new MessageProcessingContext
                {
                    Message = message,
                    SenderId = sourceNodeId,
                    LocalNodeId = _localNodeId,
                    ConnectionId = connectionId
                };
                
                await processor.ProcessAsync(context);
                MessageProcessed?.Invoke(this, new MessageProcessedEventArgs(message, TimeSpan.Zero, sourceNodeId));
            }
            else
            {
                HandlerError?.Invoke(this, new MessageHandlerErrorEventArgs(
                    new InvalidOperationException($"No processor found for message type: {messageType.Name}"), 
                    message, 
                    sourceNodeId));
            }
        }
        catch (Exception ex)
        {
            HandlerError?.Invoke(this, new MessageHandlerErrorEventArgs(ex, message, sourceNodeId));
        }
    }

    public bool CanHandle(Type messageType)
    {
        return _processors.ContainsKey(messageType);
    }

    public void RegisterProcessor<T>(IMessageProcessor processor) where T : IProtocolMessage
    {
        _processors[typeof(T)] = processor;
    }
}
