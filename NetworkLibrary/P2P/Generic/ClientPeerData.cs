using NetworkLibrary.P2P.Components.Modules;
using System;
using System.Collections.Generic;
using System.Text;
using NetworkLibrary.UDP.Jumbo;
using System.Collections.Concurrent;
using NetworkLibrary.MessageProtocol;

namespace NetworkLibrary.P2P.Generic
{
    public class ClientPeerData<S> where S : ISerializer, new()
    {
        internal PeerInformation PeerInfo;
        internal List<ReliableUdpModule> RUdpModules =  new List<ReliableUdpModule>();
        internal JumboModule JumboUdpModule;
        internal EndpointGroup punchedEndpoints;
        internal  AesTcpModule<S> punchedTcpModule;

        internal void Clear()
        {
            foreach (var item in RUdpModules)
            {
                item?.Release();
            }
            JumboUdpModule.Release();

            punchedTcpModule.Dispose();
        }
    }
}
