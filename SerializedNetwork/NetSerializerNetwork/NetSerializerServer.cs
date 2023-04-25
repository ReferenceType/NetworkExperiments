﻿using MessageProtocol;
using NetSerializerNetwork.Components;
using NetSerializerNetwork.Internal;
using NetworkLibrary.Components.Statistics;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Net;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace NetSerializerNetwork
{
    public class NetSerializerServer
    {
        public delegate void MessageReceived(in Guid clientId, MessageEnvelope message);
        public MessageReceived OnMessageReceived;

        public Action<Guid> OnClientAccepted;
        public Action<Guid> OnClientDisconnected;

        internal readonly NetSerializerServerIntenal server;
        private GenericMessageSerializer<MessageEnvelope, NetSerializer_> serialiser
            = new GenericMessageSerializer<MessageEnvelope, NetSerializer_>();


        public NetSerializerServer(int port)
        {
            //awaiter = new MessageAwaiter();

            server = new NetSerializerServerIntenal(port);
            server.DeserializeMessages = false;
            server.OnClientAccepted += HandleClientAccepted;
            // server.OnMessageReceived += HandleMessageReceived;
            server.OnBytesReceived += OnBytesReceived;
            server.OnClientDisconnected += HandleClientDisconnected;

            server.MaxIndexedMemoryPerClient = 128000000;
            server.StartServer();

        }

        private void HandleMessageReceived(Guid id, MessageEnvelope message)
        {
            OnMessageReceived?.Invoke(id, message);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        protected virtual void OnBytesReceived(in Guid guid, byte[] bytes, int offset, int count)
        {
            MessageEnvelope message = serialiser.DeserialiseEnvelopedMessage(bytes, offset, count);
            if (!CheckAwaiter(message))
            {
                OnMessageReceived?.Invoke(in guid, message);
            }
        }

        protected bool CheckAwaiter(MessageEnvelope message)
        {
            if (server.Awaiter.IsWaiting(message.MessageId))
            {
                message.LockBytes();
                server.Awaiter.ResponseArrived(message);
                return true;
            }
            return false;
        }

        public void GetStatistics(out TcpStatistics generalStats, out ConcurrentDictionary<Guid, TcpStatistics> sessionStats)
           => server.GetStatistics(out generalStats, out sessionStats);

        public IPEndPoint GetIPEndPoint(Guid cliendId)
            => server.GetSessionEndpoint(cliendId);

        #region Send
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void SendAsyncMessage(in Guid clientId, MessageEnvelope message)
        {
            server.SendAsyncMessage(clientId, message);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void SendAsyncMessage(in Guid clientId, MessageEnvelope message, byte[] buffer, int offset, int count)
        {
            server.SendAsyncMessage(clientId, message, buffer, offset, count);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void SendAsyncMessage<T>(in Guid clientId, MessageEnvelope message, T payload)
        {
            server.SendAsyncMessage(clientId, message, payload);
        }
        #endregion

        #region SendAndWait
        public Task<MessageEnvelope> SendMessageAndWaitResponse<T>(Guid clientId, MessageEnvelope message, byte[] buffer, int offset, int count, int timeoutMs = 10000)
        {
            return server.SendMessageAndWaitResponse<MessageEnvelope>(clientId, message, buffer, offset, count);
        }

        public Task<MessageEnvelope> SendMessageAndWaitResponse<T>(Guid clientId, MessageEnvelope message, T payload, int timeoutMs = 10000)
        {
            return server.SendMessageAndWaitResponse(clientId, message, payload, timeoutMs);
        }

        public Task<MessageEnvelope> SendMessageAndWaitResponse(Guid clientId, MessageEnvelope message, int timeoutMs = 10000)
        {
            return server.SendMessageAndWaitResponse(clientId, message, timeoutMs);
        }
        #endregion

        protected virtual void HandleClientAccepted(Guid clientId)
            => OnClientAccepted?.Invoke(clientId);
        protected virtual void HandleClientDisconnected(Guid guid)
            => OnClientDisconnected?.Invoke(guid);
        public void Shutdown()
            => server.ShutdownServer();
    }
}
