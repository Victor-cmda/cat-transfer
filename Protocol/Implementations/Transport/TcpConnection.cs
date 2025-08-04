using Protocol.Contracts;
using Protocol.Definitions;
using Protocol.Exceptions;
using System.Net.Sockets;

namespace Protocol.Implementations.Transport
{
    internal class TcpConnection : IDisposable
    {
        private readonly TcpClient _tcpClient;
        private readonly NetworkStream _stream;
        private readonly IMessageCodec _messageCodec;
        private readonly object _sendLock = new();
        private bool _disposed = false;

        public TcpConnection(
            TcpClient tcpClient, 
            string peerId, 
            IMessageCodec messageCodec,
            bool isIncoming)
        {
            _tcpClient = tcpClient ?? throw new ArgumentNullException(nameof(tcpClient));
            _messageCodec = messageCodec ?? throw new ArgumentNullException(nameof(messageCodec));
            PeerId = peerId ?? throw new ArgumentNullException(nameof(peerId));
            IsIncoming = isIncoming;
            
            _stream = _tcpClient.GetStream();
            ConnectedAt = DateTimeOffset.UtcNow;
        }

        public string PeerId { get; }
        public bool IsIncoming { get; }
        public DateTimeOffset ConnectedAt { get; }
        public bool IsConnected => _tcpClient?.Connected == true && !_disposed;

        public event EventHandler<MessageReceivedEventArgs>? MessageReceived;
        public event EventHandler<PeerDisconnectedEventArgs>? Disconnected;

        public async Task StartReceivingAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                var buffer = new byte[ProtocolConstants.MaxMessageSize];
                
                while (!cancellationToken.IsCancellationRequested && IsConnected)
                {
                    try
                    {
                        var lengthBytes = new byte[4];
                        var lengthBytesRead = await ReadExactAsync(lengthBytes, 0, 4, cancellationToken);
                        
                        if (lengthBytesRead == 0)
                        {
                            break;
                        }

                        var messageLength = BitConverter.ToInt32(lengthBytes, 0);
                        
                        if (messageLength <= 0 || messageLength > ProtocolConstants.MaxMessageSize)
                        {
                            throw new NetworkException("Read", $"Invalid message length: {messageLength}");
                        }

                        var messageData = new byte[messageLength];
                        var messageBytesRead = await ReadExactAsync(messageData, 0, messageLength, cancellationToken);
                        
                        if (messageBytesRead == 0)
                        {
                            break;
                        }

                        var message = _messageCodec.Decode(messageData);
                        
                        OnMessageReceived(new MessageReceivedEventArgs(PeerId, message));
                    }
                    catch (IOException)
                    {
                        break;
                    }
                    catch (SocketException)
                    {
                        break;
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"Error receiving message from {PeerId}: {ex.Message}");
                    }
                }
            }
            finally
            {
                await DisconnectAsync();
            }
        }

        public async Task<bool> SendMessageAsync(IProtocolMessage message, CancellationToken cancellationToken = default)
        {
            if (_disposed || !IsConnected)
                return false;

            try
            {
                var messageData = _messageCodec.Encode(message);
                
                if (messageData.Length > ProtocolConstants.MaxMessageSize)
                {
                    throw new NetworkException("Send", $"Message too large: {messageData.Length} bytes");
                }

                lock (_sendLock)
                {
                    if (_disposed || !IsConnected)
                        return false;

                    var lengthBytes = BitConverter.GetBytes(messageData.Length);
                    _stream.Write(lengthBytes, 0, 4);
                    
                    _stream.Write(messageData, 0, messageData.Length);
                    _stream.Flush();
                }

                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error sending message to {PeerId}: {ex.Message}");
                await DisconnectAsync();
                return false;
            }
        }

        public async Task DisconnectAsync()
        {
            if (_disposed)
                return;

            try
            {
                _stream?.Close();
                _tcpClient?.Close();
                
                OnDisconnected(new PeerDisconnectedEventArgs(PeerId, "Connection closed"));
            }
            catch
            {
            }
            finally
            {
                Dispose();
            }

            await Task.CompletedTask;
        }

        private async Task<int> ReadExactAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            var totalBytesRead = 0;
            
            while (totalBytesRead < count && !cancellationToken.IsCancellationRequested)
            {
                var bytesRead = await _stream.ReadAsync(
                    buffer, 
                    offset + totalBytesRead, 
                    count - totalBytesRead, 
                    cancellationToken);
                
                if (bytesRead == 0)
                {
                    break;
                }
                
                totalBytesRead += bytesRead;
            }
            
            return totalBytesRead;
        }

        private void OnMessageReceived(MessageReceivedEventArgs e)
        {
            MessageReceived?.Invoke(this, e);
        }

        private void OnDisconnected(PeerDisconnectedEventArgs e)
        {
            Disconnected?.Invoke(this, e);
        }

        public void Dispose()
        {
            if (_disposed)
                return;

            _disposed = true;

            try
            {
                _stream?.Dispose();
                _tcpClient?.Dispose();
            }
            catch
            {
            }
        }
    }

    public class MessageReceivedEventArgs : EventArgs
    {
        public MessageReceivedEventArgs(string peerId, IProtocolMessage message)
        {
            PeerId = peerId ?? throw new ArgumentNullException(nameof(peerId));
            Message = message ?? throw new ArgumentNullException(nameof(message));
            ReceivedAt = DateTimeOffset.UtcNow;
        }

        public string PeerId { get; }
        public IProtocolMessage Message { get; }
        public DateTimeOffset ReceivedAt { get; }
    }

    public class PeerConnectedEventArgs : EventArgs
    {
        public PeerConnectedEventArgs(string peerId, bool isIncoming)
        {
            PeerId = peerId ?? throw new ArgumentNullException(nameof(peerId));
            IsIncoming = isIncoming;
            ConnectedAt = DateTimeOffset.UtcNow;
        }

        public string PeerId { get; }
        public bool IsIncoming { get; }
        public DateTimeOffset ConnectedAt { get; }
    }

    public class PeerDisconnectedEventArgs : EventArgs
    {
        public PeerDisconnectedEventArgs(string peerId, string reason)
        {
            PeerId = peerId ?? throw new ArgumentNullException(nameof(peerId));
            Reason = reason ?? "Unknown";
            DisconnectedAt = DateTimeOffset.UtcNow;
        }

        public string PeerId { get; }
        public string Reason { get; }
        public DateTimeOffset DisconnectedAt { get; }
    }

    public class TransportErrorEventArgs : EventArgs
    {
        public TransportErrorEventArgs(Exception exception, string context)
        {
            Exception = exception ?? throw new ArgumentNullException(nameof(exception));
            Context = context ?? "Unknown";
            OccurredAt = DateTimeOffset.UtcNow;
        }

        public Exception Exception { get; }
        public string Context { get; }
        public DateTimeOffset OccurredAt { get; }
    }
}
