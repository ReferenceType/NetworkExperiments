﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace CustomNetworkLib
{
    internal class ByteMessageSessionLite : TcpSession
    {
        struct ByteMessage
        {
            public List<ArraySegment<byte>> data;

            public ByteMessage(List<ArraySegment<byte>> data)
            {
               this.data = data;
               data.Add(new ArraySegment<byte>(new byte[4]));
               data.Add(new ArraySegment<byte>(new byte[0]));
            }
        }
        ByteMessage SendMessage;

        public ByteMessageSessionLite(SocketAsyncEventArgs acceptedArg,Guid sessionId) : base(acceptedArg,sessionId)
        {
            
        }
        protected override void ConfigureSocket()
        {
            sessionSocket.ReceiveBufferSize = 128000;
            sessionSocket.SendBufferSize = 128000;
        }
        protected override void ConfigureRecieveArgs(SocketAsyncEventArgs acceptedArg)
        {
            var recieveArg = new SocketAsyncEventArgs();
            recieveArg.Completed += RecievedHeader;

            recieveArg.UserToken = new UserToken();
            recieveBuffer = new byte[4];

            ClientRecieveEventArg = recieveArg;
            ClientRecieveEventArg.SetBuffer(recieveBuffer, 0, recieveBuffer.Length);       
        }

        protected override void ConfigureSendArgs(SocketAsyncEventArgs acceptedArg)
        {
            var sendArg = new SocketAsyncEventArgs();
            sendArg.Completed += Sent;

            var token = new UserToken();
            token.Guid = Guid.NewGuid();

            sendArg.UserToken = token;
            
            ClientSendEventArg = sendArg;
            SendMessage = new ByteMessage( new List<ArraySegment<byte>>(2));
        }


        #region Recieve
        private void RecievedHeader(object sender, SocketAsyncEventArgs e)
        {

            if (e.SocketError != SocketError.Success)
            {
                HandleError(e, "while recieving header from ");
                return;
            }
            else if (e.BytesTransferred == 0)
            {
                DisconnectClient(e);
                return;
            }
            else if (e.BytesTransferred + e.Offset < 4)
            {
                e.SetBuffer(e.BytesTransferred, 4 - e.BytesTransferred);
                if (!sessionSocket.ReceiveAsync(e))
                {
                    RecievedHeader(null, e);
                }
                return;
            }

            int expectedLen = BufferManager.ReadByteFrame(e.Buffer, 0);
            recieveBuffer = new byte[expectedLen];

            e.SetBuffer(recieveBuffer, 0,expectedLen);

            e.Completed -= RecievedHeader;
            e.Completed += RecievedBody;

            if (!sessionSocket.ReceiveAsync(e))
            {
                RecievedBody(null, e);
            }

        }

        private void RecievedBody(object sender, SocketAsyncEventArgs e)
        {

            if (e.SocketError != SocketError.Success)
            {
                HandleError(e, "while recieving message body from ");
                return;
            }
            else if (e.BytesTransferred == 0)
            {
                DisconnectClient(e);
                return;
            }
            else if (e.BytesTransferred < e.Count-e.Offset)
            {
                // count decreasing
                e.SetBuffer(e.Offset + e.BytesTransferred, e.Count - e.BytesTransferred);
                if (!sessionSocket.ReceiveAsync(e))
                {
                    Task.Run(()=>RecievedBody(null, e));
                }
                return;
            }
            
           
            HandleRecieveComplete(e.Buffer, 0, e.Count);

            e.SetBuffer(0, 4);

            e.Completed -= RecievedBody;
            e.Completed += RecievedHeader;
            if (!sessionSocket.ReceiveAsync(e))
            {
                RecievedHeader(null, e);
            }
        }
        #endregion
        public override void SendAsync(byte[] bytes)
        {
            var token = (UserToken)ClientSendEventArg.UserToken;
            token.WaitOperationCompletion();

            BufferManager.WriteInt32AsBytes(SendMessage.data[0].Array, 0, bytes.Length);
            SendMessage.data[1] = new ArraySegment<byte>(bytes);

            ClientSendEventArg.BufferList = SendMessage.data;
            if (!sessionSocket.SendAsync(ClientSendEventArg))
            {
                Sent(null, ClientSendEventArg);
            }
        }

        protected override void Sent(object sender, SocketAsyncEventArgs e)
        {
            if (e.SocketError != SocketError.Success)
            {
                HandleError(e, "while sending the client");

                return;
            }

            else if (e.BytesTransferred < e.BufferList.Sum(x=>x.Count))
            {
                // shouldnt happen in theory
            }

            ((UserToken)e.UserToken).OperationCompleted();
        }
        
    }
}

