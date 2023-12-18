﻿using NetworkLibrary.Components;
using NetworkLibrary.TCP.SSL.Base;
using System;
using System.Net.Security;
using System.Runtime.CompilerServices;
using NetworkLibrary.Utils;
using System.Threading;

namespace NetworkLibrary.MessageProtocol
{
    internal class SecureMessageSession<S> : SslSession
        where S : ISerializer, new()
    {
        protected GenericMessageQueue<S> mq;
        private ByteMessageReader reader;
        public SecureMessageSession(Guid sessionId, SslStream sessionStream) : base(sessionId, sessionStream)
        {
            reader = new ByteMessageReader();
            reader.OnMessageReady += HandleMessage;
            UseQueue = false;
        }

        protected override IMessageQueue CreateMessageQueue()
        {
            mq = GetMesssageQueue();
            return mq;
        }

        protected virtual GenericMessageQueue<S> GetMesssageQueue()
        {
            return new GenericMessageQueue<S>(MaxIndexedMemory, true);
        }

        private void HandleMessage(byte[] arg1, int arg2, int arg3)
        {
            base.HandleReceived(arg1, arg2, arg3);
        }

        protected override void HandleReceived(byte[] buffer, int offset, int count)
        {
            if (IsSessionClosing()) return;
            reader.ParseBytes(buffer, offset, count);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void SendAsync(MessageEnvelope message)
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
        private void SendAsyncInternal(MessageEnvelope message)
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
            if (!mq.TryEnqueueMessage(message))
            {
                MiniLogger.Log(MiniLogger.LogLevel.Error, "Message is too large to fit on buffer");
                EndSession();
                return;
            }
            FlushAndSend();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void SendAsync<T>(MessageEnvelope envelope, T message)
        {
            if (IsSessionClosing())
                return;
            try
            {
                SendAsyncInternal(envelope, message);
            }
            catch { if (!IsSessionClosing()) throw; }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void SendAsyncInternal<T>(MessageEnvelope envelope, T message)
        {
            enqueueLock.Take();
            if (IsSessionClosing())
                ReleaseSendResourcesIdempotent();
            if (SendSemaphore.IsTaken() && mq.TryEnqueueMessage(envelope, message))
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
            if (!mq.TryEnqueueMessage(envelope,message))
            {
                MiniLogger.Log(MiniLogger.LogLevel.Error, "Message is too large to fit on buffer");
                EndSession();
                return;
            }

            //mq.TryFlushQueue(ref sendBuffer, 0, out int amountWritten);
            //WriteOnSessionStream(amountWritten);
            FlushAndSend();

        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void SendAsync(MessageEnvelope envelope, Action<PooledMemoryStream> serializationCallback)
        {
            if (IsSessionClosing())
                return;
            try
            {
                SendAsyncInternal(envelope, serializationCallback);
            }
            catch { if (!IsSessionClosing()) throw; }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void SendAsyncInternal(MessageEnvelope envelope, Action<PooledMemoryStream> serializationCallback)
        {
            enqueueLock.Take();
            if (IsSessionClosing())
                ReleaseSendResourcesIdempotent();
            if (SendSemaphore.IsTaken() && mq.TryEnqueueMessage(envelope, serializationCallback))
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
            if (!mq.TryEnqueueMessage(envelope,serializationCallback))
            {
                MiniLogger.Log(MiniLogger.LogLevel.Error, "Message is too large to fit on buffer");
                EndSession();
                return;
            }
            FlushAndSend();
            //mq.TryFlushQueue(ref sendBuffer, 0, out int amountWritten);
            //WriteOnSessionStream(amountWritten);

        }
        protected override void ReleaseSendResources()
        {
            base.ReleaseSendResources();
            Interlocked.Exchange(ref reader, null)?.ReleaseResources();
            Interlocked.Exchange(ref mq, null)?.Dispose();
        }
    }
}
