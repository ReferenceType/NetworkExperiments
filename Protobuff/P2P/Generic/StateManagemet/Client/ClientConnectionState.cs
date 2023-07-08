﻿using NetworkLibrary;
using NetworkLibrary.Components;
using NetworkLibrary.Utils;
using Protobuff.P2P.Generic.HolePunch;
using System;
using System.Collections.Generic;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Protobuff.P2P.Generic.StateManagemet.Client
{
    internal class ClientConnectionState : IState
    {
        public StateStatus Status { get; private set; }

        public Guid StateId { get; internal set; }

        public event Action<IState> Completed;

        internal ConcurrentAesAlgorithm udpEncriptor;
        internal List<EndpointData> localEndpoints;
        internal IPEndPoint serverEndpoint;
        internal Guid SessionId;

        private ConcurrentAesAlgorithm tempEncryptor;
        private StateManager stateManager;

        public ClientConnectionState(StateManager stateManager)
        {
            this.stateManager = stateManager;
            StartLifetimeCounter();
        }

        async void StartLifetimeCounter()
        {
            await Task.Delay(20000).ConfigureAwait(false);

            if (Status != StateStatus.Completed)
            {
                Release(false);
            }

        }

        public void HandleMessage(IPEndPoint remoteEndpoint, MessageEnvelope message) { }

        public void HandleMessage(MessageEnvelope message)
        {
            message.LockBytes();

            switch (message.Header)
            {
                case Constants.ServerRegisterAck:
                    RegisterUdp(message);
                    return;

                case Constants.ServerFinalizationCmd:
                    HandleFinalization(message);
                    return;
            }
        }

        private void RegisterUdp(MessageEnvelope message)
        {
            tempEncryptor = new ConcurrentAesAlgorithm(message.Payload, message.Payload);
            SessionId = message.To;

            MessageEnvelope udpRegistrationMsg = new MessageEnvelope()
            {
                Header = Constants.UdpInit,
                MessageId = message.MessageId,
            };

            EndpointTransferMessage endpoints = new EndpointTransferMessage() { LocalEndpoints = localEndpoints };
            StartUdpMessageRoutine(udpRegistrationMsg, endpoints);
        }

        private async void StartUdpMessageRoutine(MessageEnvelope udpRegistrationMsg, EndpointTransferMessage endpoints)
        {
            try
            {
                SendUdpRegistrationMessage(udpRegistrationMsg, endpoints);
                int retry = 3;
                while (--retry > 0)
                {
                    await Task.Delay(3000).ConfigureAwait(false);
                    if (Status != StateStatus.Completed)
                        SendUdpRegistrationMessage(udpRegistrationMsg, endpoints);
                }
            }
            catch { }
        }

        private void SendUdpRegistrationMessage(MessageEnvelope udpRegistrationMsg, EndpointTransferMessage endpoints)
        {
            stateManager.SendAsync(serverEndpoint, udpRegistrationMsg,
               (stream) => KnownTypeSerializer.SerializeEndpointTransferMessage(stream, endpoints),
                tempEncryptor);
        }

        private void HandleFinalization(MessageEnvelope finalMSg)
        {
            udpEncriptor = new ConcurrentAesAlgorithm(finalMSg.Payload, finalMSg.Payload);
            Release(true);
        }

        private int isReleased = 0;
        public void Release(bool isCompletedSuccessfully)
        {
            if (Interlocked.CompareExchange(ref isReleased, 1, 0) == 0)
            {
                if (isCompletedSuccessfully)
                    Status = StateStatus.Completed;
                else
                    Status = StateStatus.Failed;
                Completed?.Invoke(this);
                Completed = null;
            }
        }
    }
}
