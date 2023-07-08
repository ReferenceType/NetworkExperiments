﻿using System;
using System.Collections.Concurrent;

/* Unmerged change from project 'NetworkLibrary (netstandard2.0)'
Before:
using System.Collections.Generic;
After:
using System.Collections.Generic;
using NetworkLibrary;
using NetworkLibrary.P2P;
using NetworkLibrary.P2P;
using NetworkLibrary.P2P.Components;
*/
using System.Collections.Generic;

namespace NetworkLibrary.P2P.Components
{
    class PingData
    {
        private object locker = new object();
        internal enum State
        {
            NotReady,
            PingDispatched,
            PongReceived
        }

        private State PingState = State.NotReady;
        private DateTime dispatchTimeStamp;
        private double latency;
        public void Update(DateTime timeStamp)
        {
            lock (locker)
            {
                PingState = State.PongReceived;
                latency = (DateTime.Now - dispatchTimeStamp).TotalMilliseconds;
            }

        }

        internal void PingDispatched(DateTime timeStamp)
        {
            lock (locker)
            {
                if (PingState != State.PingDispatched)
                {
                    dispatchTimeStamp = timeStamp;
                    PingState = State.PingDispatched;
                }
            }


        }

        public double GetLatency()
        {
            lock (locker)
            {
                switch (PingState)
                {
                    case State.NotReady:
                        return 0;

                    case State.PingDispatched:
                        return Math.Max((DateTime.Now - dispatchTimeStamp).TotalMilliseconds, latency);

                    case State.PongReceived:
                        return latency;

                    default: return 0;

                }

            }
        }
    }
    internal class PingHandler
    {

        private readonly ConcurrentDictionary<Guid, PingData> tcpPingDatas = new ConcurrentDictionary<Guid, PingData>();
        private readonly ConcurrentDictionary<Guid, PingData> udpPingDatas = new ConcurrentDictionary<Guid, PingData>();

        internal void HandleTcpPongMessage(MessageEnvelope message)
        {
            if (tcpPingDatas.TryGetValue(message.From, out var data))
            {
                data.Update(message.TimeStamp);
            }
        }

        internal void HandleUdpPongMessage(MessageEnvelope message)
        {
            if (udpPingDatas.TryGetValue(message.From, out var data))
            {
                data.Update(message.TimeStamp);
            }
        }

        internal void PeerRegistered(Guid peerId)
        {
            tcpPingDatas.TryAdd(peerId, new PingData());
            udpPingDatas.TryAdd(peerId, new PingData());
        }

        internal void PeerUnregistered(Guid peerId)
        {
            tcpPingDatas.TryRemove(peerId, out _);
            udpPingDatas.TryRemove(peerId, out _);

        }

        internal void NotifyTcpPingSent(Guid to, DateTime timeStamp)
        {
            if (tcpPingDatas.TryGetValue(to, out var data))
            {
                data.PingDispatched(timeStamp);
            }
        }
        internal void NotifyUdpPingSent(Guid to, DateTime timeStamp)
        {
            if (udpPingDatas.TryGetValue(to, out var data))
            {
                data.PingDispatched(timeStamp);
            }

        }



        internal Dictionary<Guid, double> GetTcpLatencies()
        {
            var ret = new Dictionary<Guid, double>();
            foreach (var item in tcpPingDatas)
            {
                ret[item.Key] = item.Value.GetLatency();
            }
            return ret;
        }

        internal Dictionary<Guid, double> GetUdpLatencies()
        {
            var ret = new Dictionary<Guid, double>();
            foreach (var item in udpPingDatas)
            {
                ret[item.Key] = item.Value.GetLatency();
            }
            return ret;
        }

    }
}
