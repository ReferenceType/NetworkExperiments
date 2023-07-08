﻿using NetworkLibrary.P2P.Generic.Room;
using Protobuff.Components.Serialiser;
using System.Security.Cryptography.X509Certificates;

namespace Protobuff.P2P
{
    public class SecureLobbyClient : SecureLobbyClient<ProtoSerializer>
    {
        public SecureLobbyClient(X509Certificate2 clientCert) : base(clientCert)
        {
        }
    }
}
