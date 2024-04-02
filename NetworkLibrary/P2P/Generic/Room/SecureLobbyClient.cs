﻿using NetworkLibrary.MessageProtocol;
using NetworkLibrary.P2P.Components;
using NetworkLibrary.P2P.Components.HolePunch;
using NetworkLibrary.P2P.Generic;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net.Security;
using System.Security.Cryptography.X509Certificates;
using System.Threading;
using System.Threading.Tasks;

namespace NetworkLibrary.P2P.Generic.Room
{
    public class SecureLobbyClient<S> where S : ISerializer, new()
    {
        public Guid SessionId => client.SessionId;
        public bool IsConnected => client.IsConnected;

        public Action<string, Guid> OnPeerJoinedRoom;
        public Action<string, Guid> OnPeerLeftRoom;
        public Action<Guid> OnPeerDisconnected;
        //public Action<string, MessageEnvelope> OnTcpRoomMesssageReceived;
        //public Action<string, MessageEnvelope> OnUdpRoomMesssageReceived;
        public Action<MessageEnvelope> OnTcpMessageReceived;
        public Action<MessageEnvelope> OnUdpMessageReceived;
        public Action OnDisconnected;
        public RemoteCertificateValidationCallback RemoteCertificateValidationCallback;
        private RelayClientBase<S> client;

        private ConcurrentDictionary<string, Room>
            rooms = new ConcurrentDictionary<string, Room>();

        // [peerId] => Collection<RoomName>
        private ConcurrentDictionary<Guid, ConcurrentDictionary<string, string>>
            peersInRooms = new ConcurrentDictionary<Guid, ConcurrentDictionary<string, string>>();

        public SecureLobbyClient(X509Certificate2 clientCert)
        {
            client = new RelayClientBase<S>(clientCert);
            client.OnMessageReceived += HandleMessage;
            client.OnUdpMessageReceived += HandleUdpMessage;
            client.OnDisconnected += HandleDisconnected;
            client.RemoteCertificateValidationCallback += CertificateValidation;
           
        }

        private bool CertificateValidation(object sender, X509Certificate certificate, X509Chain chain, SslPolicyErrors sslPolicyErrors)
        {
            if (RemoteCertificateValidationCallback == null)
                return true;
            return RemoteCertificateValidationCallback.Invoke(sender, certificate, chain, sslPolicyErrors);
        }

        public Task<List<ServerInfo>> TryFindRelayServer(int port)
        {
            return client.TryFindRelayServer(port);
        }

        public Task<bool> ConnectAsync(string ip, int port)
        {
            return client.ConnectAsync(ip, port);
        }

        public void Connect(string ip, int port)
        {
            client.Connect(ip, port);
        }

        public Task<bool> RequestHolePunchAsync(Guid destinationId, int timeot = 10000)
        {
            return client.RequestHolePunchAsync(destinationId, timeot);
        }

        public Task<bool> RequestTcpHolePunchAsync(Guid destinationId, int timeot = 10000)
        {
            return client.RequestTcpHolePunchAsync(destinationId);
        }

        public void CreateOrJoinRoom(string roomName)
        {
            _ = CreateOrJoinRoomAsync(roomName).Result;
        }

        public Task<bool> CreateOrJoinRoomAsync(string roomName)
        {
            var returnVal = new TaskCompletionSource<bool>();

            var message = new MessageEnvelope();
            message.IsInternal = true;
            message.Header = Constants.JoinRoom;
            message.KeyValuePairs = new Dictionary<string, string>()
            {
                {roomName ,null}
            };

            rooms.TryAdd(roomName, new Room(roomName));

            client.tcpMessageClient.SendMessageAndWaitResponse(message)
                .ContinueWith((response) =>
                {
                    if (response.Result.Header != MessageEnvelope.RequestTimeout)
                    {
                        returnVal.TrySetResult(true);
                    }
                    else
                    {
                        rooms.TryRemove(roomName, out _);
                        returnVal.TrySetResult(false);
                    }
                });
            return returnVal.Task;
        }

        public void LeaveRoom(string roomName)
        {
            rooms.TryRemove(roomName, out _);

            var message = new MessageEnvelope();
            message.IsInternal = true;
            message.Header = Constants.LeaveRoom;
            message.KeyValuePairs = new Dictionary<string, string>()
            {
                {roomName ,null}
            };
            client.tcpMessageClient.SendAsyncMessage(message);
        }

        public Task<List<string>> GetAvailableRooms()
        {
            var returnVal = new TaskCompletionSource<List<string>>();
            var message = new MessageEnvelope();
            message.Header = Constants.GetAvailableRooms;
            message.IsInternal = true;

            client.tcpMessageClient.SendMessageAndWaitResponse(message)
                .ContinueWith((response) =>
                {
                    if (response.Result.Header != MessageEnvelope.RequestTimeout)
                    {
                        returnVal.TrySetResult(response.Result.KeyValuePairs.Keys.ToList());
                    }
                    else
                    {
                        returnVal.TrySetResult(null);
                    }
                });
            return returnVal.Task;
        }

        #region Send
        private bool CanSend(string roomName)
        {
            if (rooms.TryGetValue(roomName, out var room))
            {
                if (room.PeerCount > 0)
                {
                    return true;
                }
            }
            return false;
        }

        private void PrepareEnvelopeBC(string roomName, ref MessageEnvelope messageEnvelope)
        {
            if (messageEnvelope.KeyValuePairs == null)
                messageEnvelope.KeyValuePairs = new Dictionary<string, string>();

            messageEnvelope.KeyValuePairs[Constants.RoomName] = roomName;
            messageEnvelope.To = Guid.Empty;
            messageEnvelope.From = client.SessionId;
        }

        private void PrepareEnvelopeDM(string roomName, ref MessageEnvelope messageEnvelope)
        {
            if (messageEnvelope.KeyValuePairs == null)
                messageEnvelope.KeyValuePairs = new Dictionary<string, string>();

            messageEnvelope.KeyValuePairs[Constants.RoomName] = roomName;
            messageEnvelope.From = client.SessionId;
        }

        #region Room Messages

        public void BroadcastMessageToRoom(string roomName, MessageEnvelope message)
        {
            if (CanSend(roomName))
            {
                PrepareEnvelopeBC(roomName, ref message);
                client.tcpMessageClient.SendAsyncMessage(message);
            }
        }

        public void BroadcastMessageToRoom<T>(string roomName, MessageEnvelope message, T innerMessage)
        {
            if (CanSend(roomName))
            {
                PrepareEnvelopeBC(roomName, ref message);
                client.tcpMessageClient.SendAsyncMessage(message, innerMessage);
            }
        }

        public void BroadcastUdpMessageToRoom(string roomName, MessageEnvelope message)
        {
            if (CanSend(roomName))
            {
                PrepareEnvelopeBC(roomName, ref message);
                if (rooms.TryGetValue(roomName, out var room))
                    client.MulticastUdpMessage(message, room.PeerIds);
            }
        }

        public void BroadcastUdpMessageToRoom<T>(string roomName, MessageEnvelope message, T innerMessage)
        {
            if (CanSend(roomName))
            {
                PrepareEnvelopeBC(roomName, ref message);
                if (rooms.TryGetValue(roomName, out var dict))
                    client.MulticastUdpMessage(message, dict.PeerIds, innerMessage);
            }
        }

        public void BroadcastRudpMessageToRoom(string roomName, MessageEnvelope message, RudpChannel channel = RudpChannel.Ch1)
        {
            if (CanSend(roomName))
            {
                PrepareEnvelopeBC(roomName, ref message);
                if (rooms.TryGetValue(roomName, out var roomDict))
                {
                    foreach (var peerId in roomDict.PeerIds)
                    {
                        if (peerId != SessionId)
                            client.SendRudpMessage(peerId, message, channel);
                    }
                }
            }
        }

        public void BroadcastRudpMessageToRoom<T>(string roomName, MessageEnvelope message, T innerMessage, RudpChannel channel = RudpChannel.Ch1)
        {
            if (CanSend(roomName))
            {
                PrepareEnvelopeBC(roomName, ref message);
                if (rooms.TryGetValue(roomName, out var roomDict))
                {
                    foreach (var peerId in roomDict.PeerIds)
                    {
                        if (peerId != SessionId)
                            client.SendRudpMessage(peerId, message, innerMessage, channel);
                    }
                }
            }
        }
        #endregion

        #region Direct Messages
        //Tcp
        public void SendAsyncMessage(Guid peerId, MessageEnvelope message)
        {
            client.SendAsyncMessage(peerId, message);
        }
        public void SendAsyncMessage<T>(Guid peerId, T message, string messageHeader = null)
        {
            client.SendAsyncMessage(peerId, message, messageHeader);
        }
        public void SendAsyncMessage<T>(Guid peerId, MessageEnvelope message, T innerMessage)
        {
            client.SendAsyncMessage(peerId, message, innerMessage);
        }

        public Task<MessageEnvelope> SendRequestAndWaitResponse(Guid peerId, MessageEnvelope message, int timeoutMs = 10000)
        {
            return client.SendRequestAndWaitResponse(peerId, message, timeoutMs);
        }

        public Task<MessageEnvelope> SendRequestAndWaitResponse<T>(Guid peerId, MessageEnvelope message,T innerMessage, int timeoutMs = 10000)
        {
            return client.SendRequestAndWaitResponse(peerId, message, innerMessage, timeoutMs);
        }
        public Task<MessageEnvelope> SendRequestAndWaitResponse<T>(Guid peerId, T innerMessage, string messageHeader = null, int timeoutMs = 10000)
        {
            return client.SendRequestAndWaitResponse(peerId, innerMessage, messageHeader, timeoutMs);
        }
        //---
        // Udp
        public void SendUdpMessage(Guid peerId, MessageEnvelope message)
        {
            client.SendUdpMessage(peerId, message);
        }

        public void SendUdpMessage<T>(Guid peerId, MessageEnvelope message, T innerMessage)
        {
            client.SendUdpMessage(peerId, message, innerMessage);
        }

        //--
        // Rudp
        public void SendRudpMessage(Guid peerId, MessageEnvelope message, RudpChannel channel = RudpChannel.Ch1)
        {
            client.SendRudpMessage(peerId, message, channel);
        }
        public void SendRudpMessage<T>(Guid peerId, MessageEnvelope message, T innerMessage, RudpChannel channel = RudpChannel.Ch1)
        {
            client.SendRudpMessage(peerId, message, innerMessage,channel);
        }
        public Task<MessageEnvelope> SendRudpMessageAndWaitResponse(Guid peerId, MessageEnvelope message, int timeoutMs = 10000,RudpChannel channel =  RudpChannel.Ch1)
        {
            return client.SendRudpMessageAndWaitResponse(peerId, message, timeoutMs, channel);
        }
        public Task<MessageEnvelope> SendRudpMessageAndWaitResponse<T>(Guid peerId, MessageEnvelope message, T innerMessage, int timeoutMs = 10000, RudpChannel channel = RudpChannel.Ch1)
        {
            return client.SendRudpMessageAndWaitResponse(peerId, message, innerMessage, timeoutMs,channel);
        }
        //--
        #endregion

        #endregion

        private void HandleUdpMessage(MessageEnvelope message)
        {
            //if (message.KeyValuePairs != null && message.KeyValuePairs.TryGetValue(Constants.RoomName, out string roomName))
            //{
            //    OnUdpRoomMesssageReceived?.Invoke(roomName, message);
            //}elseif
            if(peersInRooms.ContainsKey(message.From))
            {
                OnUdpMessageReceived?.Invoke(message);
            }
        }
        private void HandleMessage(MessageEnvelope message)
        {
            if (message.Header == Constants.RoomUpdate)
            {
                UpdateRooms(message);
            }
            else if (message.Header == Constants.PeerDisconnected)
            {
                HandlePeerDisconnected(message);
            }
            else
                HandleTcpReceived(message);
        }

        private void HandleTcpReceived(MessageEnvelope message)
        {
            //if (message.KeyValuePairs != null && message.KeyValuePairs.TryGetValue(Constants.RoomName, out string roomName))
            //{
            //    OnTcpRoomMesssageReceived?.Invoke(roomName, message);
            //}elseif
            if(peersInRooms.ContainsKey(message.From))
            {
                OnTcpMessageReceived?.Invoke(message);
            }
        }

        private void UpdateRooms(MessageEnvelope message)
        {
            var roomUpdateMessage = KnownTypeSerializer.DeserializeRoomPeerList(message.Payload, message.PayloadOffset);

            Dictionary<Guid, PeerInfo> JoinedList = new Dictionary<Guid, PeerInfo>();
            List<Guid> LeftList = new List<Guid>();

            var remoteList = roomUpdateMessage.Peers.PeerIds;
            if (!rooms.TryGetValue(roomUpdateMessage.RoomName, out var localRoom))
            {
                return;
            }

            foreach (var remotePeer in remoteList)
            {
                if (!localRoom.ContainsPeer(remotePeer.Key))
                {
                    JoinedList.Add(remotePeer.Key, remotePeer.Value);
                }

            }

            foreach (var localPeerId in localRoom.PeerIds)
            {
                if (!remoteList.ContainsKey(localPeerId))
                {
                    LeftList.Add(localPeerId);
                }

            }

            foreach (var peerKV in JoinedList)
            {
                if (rooms.TryGetValue(roomUpdateMessage.RoomName, out var room))
                {
                    room.Add(peerKV.Key, peerKV.Value);
                }

                if(!peersInRooms.TryGetValue(peerKV.Key, out var roomList))
                {
                    peersInRooms.TryAdd(peerKV.Key, new ConcurrentDictionary<string, string>());
                }
                peersInRooms[peerKV.Key].TryAdd(roomUpdateMessage.RoomName, null);

                client.HandleRegistered(peerKV.Key, roomUpdateMessage.Peers.PeerIds);
                OnPeerJoinedRoom?.Invoke(roomUpdateMessage.RoomName, peerKV.Key);
            }

            foreach (var peerId in LeftList)
            {
                if (rooms.TryGetValue(roomUpdateMessage.RoomName, out var room))
                {
                    room.Remove(peerId);
                }

                if (peersInRooms.TryGetValue(peerId, out var roomList))
                {
                    roomList.TryRemove(roomUpdateMessage.RoomName, out _);
                    if(roomList.Count == 0)
                    {
                        peersInRooms.TryRemove(peerId, out _);
                        HandlePeerDisconnected_(peerId);
                    }
                }
               
                //client.HandleUnRegistered(peerId);
                OnPeerLeftRoom?.Invoke(roomUpdateMessage.RoomName, peerId);
            }
        }

        private void HandlePeerDisconnected(MessageEnvelope message)
        {
            HandlePeerDisconnected_(message.From);
        }
        private void HandlePeerDisconnected_(Guid peerId)
        {
            if (client.Peers.TryRemove(peerId, out _))
            {
                client.HandleUnRegistered(peerId);
                OnPeerDisconnected?.Invoke(peerId);
            }
        }
        private void HandleDisconnected()
        {
            foreach (var room in rooms)
            {
                foreach (var peerId in room.Value.PeerIds)
                {
                    OnPeerDisconnected?.Invoke(peerId);
                }
            }
            peersInRooms.Clear();
            rooms.Clear();
            OnDisconnected?.Invoke();
        }
        public bool TryGetRoommateInfo(string roomName, Guid id, out PeerInfo info)
        {
            info = null;
            if (roomName == null) return false;
            if (rooms.TryGetValue(roomName, out Room room))
            {
                if (room.TryGetPeerInfo(id, out info))
                    return true;
            }
            return false;
        }

        public PeerInformation GetPeerInfo(Guid clientId)
        {
            return client.GetPeerInfo(clientId);
        }
        public bool TryGetRoommateIds(string roomName, out ICollection<Guid> peerIds)
        {
            peerIds = null;
            if (roomName == null) return false;
            if (rooms.TryGetValue(roomName, out Room room))
            {
                peerIds = room.PeerIds;
                return true;
            }
            return false;
        }

        public void Disconnect()
        {
            client.Disconnect();
        }

        public void Dispose()
        {
            client.Dispose();
            peersInRooms.Clear();
            rooms.Clear();
        }

        class Room
        {
            public string roomName;
            private ConcurrentDictionary<Guid, PeerInfo> roomMates = new ConcurrentDictionary<Guid, PeerInfo>();

            public Room(string roomName)
            {
                this.roomName = roomName;
            }

            public int PeerCount => roomMates.Count;

            public ICollection<Guid> PeerIds => roomMates.Keys;
            internal bool ContainsPeer(Guid key) => roomMates.ContainsKey(key);


            public void Add(Guid peerId, PeerInfo info)
            {
                roomMates.TryAdd(peerId, info);
            }
            public void Remove(Guid peerId)
            {
                roomMates.TryRemove(peerId, out _);
            }

            internal bool TryGetPeerInfo(Guid id, out PeerInfo info)
            {
                return roomMates.TryGetValue(id, out info);
            }
        }
    }
}
