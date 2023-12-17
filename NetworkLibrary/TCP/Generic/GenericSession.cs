﻿using NetworkLibrary.Components;
using NetworkLibrary.MessageProtocol;
using NetworkLibrary.TCP.Base;
using System;
using System.Net.Sockets;
using System.Runtime.CompilerServices;
using System.Threading;

namespace NetworkLibrary.TCP.Generic
{
    internal class GenericSession<S> : TcpSession
       where S : ISerializer, new()
    {
        protected GenericBuffer<S> mq;
        private ByteMessageReader reader;
        private readonly bool writeMsgLenghtPrefix;
        public GenericSession(SocketAsyncEventArgs acceptedArg, Guid sessionId, bool writeMsgLenghtPrefix = true) : base(acceptedArg, sessionId)
        {
            this.writeMsgLenghtPrefix = writeMsgLenghtPrefix;
            reader = new ByteMessageReader();
            reader.OnMessageReady += HandleMessage;
            UseQueue = false;
        }

        private GenericBuffer<S> GetMesssageQueue()
        {
            return new GenericBuffer<S>(MaxIndexedMemory, writeMsgLenghtPrefix);
        }

        protected sealed override IMessageQueue CreateMessageQueue()
        {
            mq = GetMesssageQueue();
            return mq;
        }

        private void HandleMessage(byte[] arg1, int arg2, int arg3)
        {
            base.HandleReceived(arg1, arg2, arg3);
        }

        protected override void HandleReceived(byte[] buffer, int offset, int count)
        {
            if (writeMsgLenghtPrefix)
                reader.ParseBytes(buffer, offset, count);
            else
                base.HandleReceived(buffer, offset, count);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void SendAsync<T>(T message)
        {
            if (IsSessionClosing())
                return;
            try
            {
                SendAsyncInternal(message);
            }
            catch { if (!IsSessionClosing()) throw; }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void SendAsyncInternal<T>(T message)
        {
            enqueueLock.Take();
            if (IsSessionClosing())
                ReleaseSendResourcesIdempotent();
            if (SendSemaphore.IsTaken() && mq.TryEnqueueMessage(message))
            {
                enqueueLock.Release();
                return;
            }
            enqueueLock.Release();

            if (DropOnCongestion && SendSemaphore.IsTaken())
                return;

            SendSemaphore.Take();
            if (IsSessionClosing())
            {
                ReleaseSendResourcesIdempotent();
                SendSemaphore.Release();
                return;
            }

            // you have to push it to queue because queue also does the processing.
            mq.TryEnqueueMessage(message);
            mq.TryFlushQueue(ref sendBuffer, 0, out int amountWritten);
            FlushSendBuffer(0, amountWritten);

        }
        protected override void ReleaseReceiveResources()
        {
            base.ReleaseReceiveResources();
            Interlocked.Exchange(ref mq, null)?.Dispose();
            Interlocked.Exchange(ref reader, null)?.ReleaseResources();
        }
    }
}
