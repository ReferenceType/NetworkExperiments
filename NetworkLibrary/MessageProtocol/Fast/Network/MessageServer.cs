﻿using NetworkLibrary.TCP.Base;
using System;
using System.Net;
using System.Net.Sockets;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;

namespace NetworkLibrary.MessageProtocol
{
    public class MessageServer<S> : AsyncTcpServer
        where S : ISerializer, new()
    {
        public Action<Guid, MessageEnvelope> OnMessageReceived;

        private GenericMessageSerializer<S> serializer;
        internal GenericMessageAwaiter<MessageEnvelope> Awaiter = new GenericMessageAwaiter<MessageEnvelope>();
        public MessageServer(int port) : base(port)
        {
            if (MessageEnvelope.Serializer == null)
            {
                MessageEnvelope.Serializer = new GenericMessageSerializer<S>();
            }
            MapReceivedBytes();
        }

        protected virtual GenericMessageSerializer<S> CreateMessageSerializer()
        {
            return new GenericMessageSerializer<S>();
        }
        protected virtual void MapReceivedBytes()
        {
            serializer = CreateMessageSerializer();
            OnBytesReceived = HandleBytes;
        }

        private void HandleBytes(Guid guid, byte[] bytes, int offset, int count)
        {
            MessageEnvelope message = serializer.DeserialiseEnvelopedMessage(bytes, offset, count);
            if (!CheckAwaiter(message))
            {
                OnMessageReceived?.Invoke(guid, message);
            }
        }

        private protected virtual MessageSession<S> MakeSession(SocketAsyncEventArgs e, Guid sessionId)
        {
            return new MessageSession<S>(e, sessionId);
        }
        private protected override IAsyncSession CreateSession(SocketAsyncEventArgs e, Guid sessionId)
        {
            var session = MakeSession(e, sessionId);//new GenericMessageSession(e, sessionId);
            session.SocketRecieveBufferSize = ClientReceiveBufsize;
            session.MaxIndexedMemory = MaxIndexedMemoryPerClient;
            session.DropOnCongestion = DropOnBackPressure;
            //session.OnSessionClosed += (id) => OnClientDisconnected?.Invoke(id);
            return session;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void SendAsyncMessage(Guid clientId, MessageEnvelope message)
        {
            if (Sessions.TryGetValue(clientId, out var session))
                ((MessageSession<S>)session).SendAsync(message);

        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void SendAsyncMessage<T>(Guid clientId, MessageEnvelope envelope, T message)
        {
            if (Sessions.TryGetValue(clientId, out var session))
                ((MessageSession<S>)session).SendAsync(envelope, message);

        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void SendAsyncMessage(Guid clientId, MessageEnvelope message, byte[] buffer, int offset, int count)
        {
            message.SetPayload(buffer, offset, count);
            SendAsyncMessage(clientId, message);
        }

        public Task<MessageEnvelope> SendMessageAndWaitResponse<T>(Guid clientId, MessageEnvelope message, byte[] buffer, int offset, int count, int timeoutMs = 10000)
        {
            if (message.MessageId == Guid.Empty)
                message.MessageId = Guid.NewGuid();

            var result = Awaiter.RegisterWait(message.MessageId, timeoutMs);
            SendAsyncMessage(clientId, message, buffer, offset, count);
            return result;
        }

        public Task<MessageEnvelope> SendMessageAndWaitResponse<T>(Guid clientId, MessageEnvelope message, T payload, int timeoutMs = 10000)
        {
            if (message.MessageId == Guid.Empty)
                message.MessageId = Guid.NewGuid();

            var task = Awaiter.RegisterWait(message.MessageId, timeoutMs);
            SendAsyncMessage(clientId, message, payload);
            return task;
        }

        public Task<MessageEnvelope> SendMessageAndWaitResponse(Guid clientId, MessageEnvelope message, int timeoutMs = 10000)
        {
            if (message.MessageId == Guid.Empty)
                message.MessageId = Guid.NewGuid();

            var task = Awaiter.RegisterWait(message.MessageId, timeoutMs);
            SendAsyncMessage(clientId, message);
            return task;
        }

        public IPEndPoint GetIPEndPoint(Guid cliendId)
        {
            return GetSessionEndpoint(cliendId);
        }

        protected bool CheckAwaiter(MessageEnvelope message)
        {
            if (Awaiter.IsWaiting(message.MessageId))
            {
                message.LockBytes();
                Awaiter.ResponseArrived(message);
                return true;
            }
            return false;
        }
    }
}
