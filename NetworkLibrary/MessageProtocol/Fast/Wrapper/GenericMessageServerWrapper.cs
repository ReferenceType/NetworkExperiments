﻿using NetworkLibrary.Components.Statistics;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Net;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace NetworkLibrary.MessageProtocol.Fast
{
    public class GenericMessageServerWrapper<S>
        where S:ISerializer,new()
    {
        public delegate void MessageReceived(Guid clientId, MessageEnvelope message);
        public MessageReceived OnMessageReceived;

        public Action<Guid> OnClientAccepted;
        public Action<Guid> OnClientDisconnected;

        internal readonly MessageServer<S> server;
        private GenericMessageSerializer<S> serialiser = new GenericMessageSerializer<S>();


        public GenericMessageServerWrapper(int port)
        {
            server = new MessageServer<S>(port);
            server.OnClientAccepted += HandleClientAccepted;
            server.OnMessageReceived  = (guid,message)=>OnMessageReceived?.Invoke(guid,message);
            server.OnClientDisconnected += HandleClientDisconnected;

            server.MaxIndexedMemoryPerClient = 128000000;
        }

        public void StartServer() => server.StartServer();


        public void GetStatistics(out TcpStatistics generalStats, out ConcurrentDictionary<Guid, TcpStatistics> sessionStats)
           => server.GetStatistics(out generalStats, out sessionStats);

        public IPEndPoint GetIPEndPoint(Guid cliendId)
            => server.GetSessionEndpoint(cliendId);

        #region Send
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void SendAsyncMessage(Guid clientId, MessageEnvelope message)
        {
            server.SendAsyncMessage(clientId, message);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void SendAsyncMessage(Guid clientId, MessageEnvelope message, byte[] buffer, int offset, int count)
        {
            server.SendAsyncMessage(clientId, message, buffer, offset, count);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void SendAsyncMessage<T>(Guid clientId, MessageEnvelope message, T payload)
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
