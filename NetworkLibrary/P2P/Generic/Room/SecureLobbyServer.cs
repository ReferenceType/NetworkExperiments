﻿using NetworkLibrary.Components;
using NetworkLibrary.MessageProtocol;
using NetworkLibrary.P2P.Components;
using NetworkLibrary.P2P.Components.HolePunch;
using NetworkLibrary.P2P.Generic;
using NetworkLibrary.Utils;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using System.Threading;
using System.Threading.Tasks;

namespace NetworkLibrary.P2P.Generic.Room
{
    public class SecureLobbyServer<S> : SecureRelayServerBase<S> where S : ISerializer, new()
    {
        private ConcurrentDictionary<string, Room<S>>
            rooms = new ConcurrentDictionary<string, Room<S>>();

        private ConcurrentDictionary<Guid, ConcurrentDictionary<string, string>>
            peerToRoomMap = new ConcurrentDictionary<Guid, ConcurrentDictionary<string, string>>();

        // representspeers that have contacted. used for broadasting disconnect message 
        private ConcurrentDictionary<Guid, ConcurrentDictionary<Guid, string>>
            peerRelationalMap = new ConcurrentDictionary<Guid, ConcurrentDictionary<Guid, string>>();

        private object roomLock = new object();
        public SecureLobbyServer(int port, X509Certificate2 cerificate) : base(port, cerificate)
        {
        }
        #region Overrides
        #region Override Disbale Behaviour
        protected override void NotifyCurrentPeerList(Guid clientId)
        {
            // skip, we dont publish peer registered anymore
        }

        protected override void PublishPeerRegistered(Guid clientId)
        {
            // this on base sets the task to publish peer list
            // we dont care
        }
        #endregion

        protected override void PublishPeerUnregistered(Guid clientId)
        {
            // here we care because it could be dc.
            //TODO Send a Special Disconnect Message.... to who ??
            HashSet<Guid> notificationList = new HashSet<Guid>();
            lock (roomLock)
            {
                if (!peerToRoomMap.TryRemove(clientId, out var roomList))
                {
                    return;
                }
                foreach (var item in roomList)
                {
                    var roomName = item.Key;
                    if (rooms.TryGetValue(roomName, out var room))
                    {
                        room.Remove(clientId);
                        var peerList = room.GetPeerIdList();
                        foreach (var id in peerList)
                        {
                            notificationList.Add(id);
                        }
                    }
                }
            }

            var msg = new MessageEnvelope()
            {
                Header = Constants.PeerDisconnected,
                From = clientId
            };
            foreach (var peerId in notificationList)
            {
                SendAsyncMessage(peerId, msg);
            }
        }

     
        protected override void HandleMessageReceivedInternal(Guid clientId, MessageEnvelope message)
        {
            if (message.Header == Constants.JoinRoom)
            {
                CreateOrJoinRoom(clientId, message);
            }
            else if (message.Header == Constants.LeaveRoom)
            {
                LeaveRoom(clientId, message);
            }
            else if (message.Header == Constants.GetAvailableRooms)
            {
                SendAvailableRooms(clientId, message);
            }
            else
            {
                base.HandleMessageReceivedInternal(clientId, message);
            }
        }

        private void SendAvailableRooms(Guid clientId, MessageEnvelope message)
        {
            message.KeyValuePairs = new Dictionary<string, string>();
            foreach (var item in rooms)
            {
                message.KeyValuePairs.Add(item.Key, null);
            }
            SendAsyncMessage(clientId, message);
        }
        #endregion

        private void CreateOrJoinRoom(Guid clientID, MessageEnvelope message)
        {
            if (message.KeyValuePairs != null)
            {
                var roomName = message.KeyValuePairs.Keys.First();
                Room<S> room = null;
                lock (roomLock)
                {
                    if (!rooms.TryGetValue(roomName, out room))
                    {
                        room = new Room<S>(roomName, this);
                        rooms.TryAdd(roomName, room);
                        //Console.WriteLine(roomName + " Added");
                        MiniLogger.Log(MiniLogger.LogLevel.Info, $"Room Created [{roomName}]");
                    }
                    room.Add(clientID);
                    // Console.WriteLine(roomName + " Joined");
                    MiniLogger.Log(MiniLogger.LogLevel.Info, $"{clientID} Joined the room [{roomName}]");

                    if (!peerToRoomMap.ContainsKey(clientID))
                        peerToRoomMap.TryAdd(clientID, new ConcurrentDictionary<string, string>());
                    peerToRoomMap[clientID].TryAdd(roomName, null);
                }
                SendAsyncMessage(clientID, new MessageEnvelope() { MessageId = message.MessageId });
            }
        }

        private void LeaveRoom(Guid clientID, MessageEnvelope message)
        {
            if (message.KeyValuePairs != null)
            {
                var roomName = message.KeyValuePairs.Keys.First();
                lock (roomLock)
                {
                    if (rooms.TryGetValue(roomName, out var room))
                    {
                        room.Remove(clientID);
                    }
                    if (peerToRoomMap.ContainsKey(clientID))
                        peerToRoomMap[clientID].TryRemove(roomName, out _);
                }
            }
        }
        protected override void BroadcastMessage(Guid guid, byte[] bytes, int offset, int count)
        {
            var messageEnvelope = serialiser.DeserialiseEnvelopedMessage(bytes, offset, count);
            if (messageEnvelope.KeyValuePairs != null && messageEnvelope.KeyValuePairs.TryGetValue(Constants.RoomName, out string roomName))
            {
                rooms.TryGetValue(roomName, out var room);
                var snapshot = room.GetPeerIdList();
                foreach (var peerId in snapshot)
                {
                    if (peerId != guid)
                        SendBytesToClient(peerId, bytes, offset, count);
                }
            }
        }

        // we have to do something about this in future when we have reliable udp.
        protected override void BroadcastUdp(byte[] buffer, int lenght)
        {
            var messageEnvelope = serialiser.DeserialiseEnvelopedMessage(buffer, 0, lenght);
            peerReachabilityMatrix.TryGetValue(messageEnvelope.From, out var edgeMap);
            if (messageEnvelope.KeyValuePairs != null && messageEnvelope.KeyValuePairs.TryGetValue(Constants.RoomName, out string roomName))
            {
                // find the room
                rooms.TryGetValue(roomName, out var room);
                var peerIdList = room.GetPeerIdList();
                foreach (var peerId in peerIdList)
                {
                    if (edgeMap != null && edgeMap.TryGetValue(peerId, out _))
                    {
                        continue;
                    }

                    if (messageEnvelope.From != peerId)
                        RelayUdpMessage(peerId, buffer, 0, lenght);

                }
            }

        }
        internal class Room<S> where S : ISerializer, new()
        {
            public readonly string RoomName;
            private readonly SecureLobbyServer<S> server;
            private ConcurrentDictionary<Guid, string> roomMates = new ConcurrentDictionary<Guid, string>();
            private TaskCompletionSource<bool> PublishSignal = new TaskCompletionSource<bool>();
            public Room(string roomName, SecureLobbyServer<S> server)
            {
                RoomName = roomName;
                this.server = server;
                PublishRotune();
            }

            private async void PublishRotune()
            {
                while (true)
                {
                    try
                    {
                        await Task.Delay(1000).ConfigureAwait(false);
                        await PublishSignal.Task.ConfigureAwait(false);

                        Interlocked.Exchange(ref PublishSignal, new TaskCompletionSource<bool>());

                        RoomPeerList rpl = GenerateRoomPeerList();
                        MessageEnvelope message = PrepareMessage(rpl, out var toReturn);
                        lock (roomMates)
                        {
                            foreach (var peer in roomMates)
                            {
                                server.SendAsyncMessage(peer.Key, message);
                            }
                            SharerdMemoryStreamPool.ReturnStreamStatic(toReturn);
                        }
                    }
                    catch (Exception ex) { MiniLogger.Log(MiniLogger.LogLevel.Error, ex.Message); }

                }
            }

            private MessageEnvelope PrepareMessage(RoomPeerList rpl, out PooledMemoryStream returnAfterFinished)
            {
                var message = new MessageEnvelope()
                {
                    Header = Constants.RoomUpdate,
                };

                var stream = SharerdMemoryStreamPool.RentStreamStatic();
                KnownTypeSerializer.SerializeRoomPeerList(stream, rpl);
                message.SetPayload(stream.GetBuffer(), 0, stream.Position32);

                returnAfterFinished = stream;
                return message;
            }

            private RoomPeerList GenerateRoomPeerList()
            {
                RoomPeerList rpl = new RoomPeerList();
                rpl.RoomName = RoomName;
                rpl.Peers = new PeerList();
                rpl.Peers.PeerIds = new Dictionary<Guid, PeerInfo>();

                foreach (var item in roomMates)
                {
                    var ep = server.GetIPEndPoint(item.Key);
                    var peerInfo = new PeerInfo()
                    {
                        Address = ep.Address.GetAddressBytes(),
                        Port = (ushort)ep.Port
                    };
                    rpl.Peers.PeerIds[item.Key] = peerInfo;
                }
                return rpl;
            }

            public void Add(Guid peerId)
            {
                if (roomMates.TryAdd(peerId, null))
                    PublishSignal.TrySetResult(true);
            }

            public void Remove(Guid peerId)
            {
                if (roomMates.TryRemove(peerId, out _))
                    PublishSignal.TrySetResult(true);
            }

            internal ICollection<Guid> GetPeerIdList()
            {
                return roomMates.Keys;
            }
        }
    }

}
