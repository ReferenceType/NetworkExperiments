﻿using MessageProtocol;
using Protobuff.Components.Serialiser;
using System;
using System.Net.Sockets;

namespace Protobuff.Components.ProtoTcp
{
    internal class ProtoSessionInternal : MessageSession<MessageEnvelope, ProtoSerializer>
    {
        public ProtoSessionInternal(SocketAsyncEventArgs acceptedArg, Guid sessionId) : base(acceptedArg, sessionId)
        {
        }

      
       

        //protected override ProtoMessageQueue GetMesssageQueue()
        //{
        //    return new ProtoMessageQueue(MaxIndexedMemory, true);
        //}

        //ProtoMessageQueue mq;
        //private ByteMessageReader reader;
        //public ProtoSessionInternal(SocketAsyncEventArgs acceptedArg, Guid sessionId) : base(acceptedArg, sessionId)
        //{
        //    reader = new ByteMessageReader(sessionId);
        //    reader.OnMessageReady += HandleMessage;
        //    UseQueue = false;
        //}

        //protected override IMessageQueue CreateMessageQueue()
        //{
        //    mq = new ProtoMessageQueue(MaxIndexedMemory, true);
        //    return mq;
        //}
        //private void HandleMessage(byte[] arg1, int arg2, int arg3)
        //{
        //    base.HandleReceived(arg1, arg2, arg3);
        //}

        //protected override void HandleReceived(byte[] buffer, int offset, int count)
        //{
        //    reader.ParseBytes(buffer, offset, count);
        //}


        //public void SendAsync(MessageEnvelope message)
        //{
        //    if (IsSessionClosing())
        //        return;
        //    try
        //    {
        //        SendAsyncInternal(message);
        //    }
        //    catch { if (!IsSessionClosing()) throw; }
        //}

        //[MethodImpl(MethodImplOptions.AggressiveInlining)]
        //private void SendAsyncInternal(MessageEnvelope message)
        //{
        //    enqueueLock.Take();
        //    if (IsSessionClosing())
        //        ReleaseSendResourcesIdempotent();
        //    if (SendSemaphore.IsTaken() && mq.TryEnqueueMessage(message))
        //    {
        //        enqueueLock.Release();
        //        return;
        //    }
        //    enqueueLock.Release();

        //    if (DropOnCongestion && SendSemaphore.IsTaken())
        //        return;

        //    SendSemaphore.Take();
        //    if (IsSessionClosing())
        //    {
        //        ReleaseSendResourcesIdempotent();
        //        SendSemaphore.Release();
        //        return;
        //    }

        //    // you have to push it to queue because queue also does the processing.
        //    mq.TryEnqueueMessage(message);
        //    mq.TryFlushQueue(ref sendBuffer, 0, out int amountWritten);
        //    FlushSendBuffer(0,amountWritten);

        //}

        //public void SendAsync<T>(MessageEnvelope envelope, T message) where T : IProtoMessage
        //{
        //    if (IsSessionClosing())
        //        return;
        //    try
        //    {
        //        SendAsyncInternal(envelope,message);
        //    }
        //    catch { if (!IsSessionClosing()) throw; }
        //}

        //[MethodImpl(MethodImplOptions.AggressiveInlining)]
        //private void SendAsyncInternal<T>(MessageEnvelope envelope, T message) where T: IProtoMessage
        //{
        //    enqueueLock.Take();
        //    if (IsSessionClosing())
        //        ReleaseSendResourcesIdempotent();
        //    if (SendSemaphore.IsTaken() && mq.TryEnqueueMessage(envelope,message))
        //    {
        //        enqueueLock.Release();
        //        return;
        //    }
        //    enqueueLock.Release();

        //    if (DropOnCongestion && SendSemaphore.IsTaken())
        //        return;

        //    SendSemaphore.Take();
        //    if (IsSessionClosing())
        //    {
        //        ReleaseSendResourcesIdempotent();
        //        SendSemaphore.Release();
        //        return;
        //    }

        //    // you have to push it to queue because queue also does the processing.
        //    mq.TryEnqueueMessage(envelope, message);
        //    mq.TryFlushQueue(ref sendBuffer, 0, out int amountWritten);
        //    FlushSendBuffer(0, amountWritten);
        //}

        //protected override void ReleaseReceiveResources()
        //{
        //    base.ReleaseReceiveResources();
        //    reader.ReleaseResources();
        //    reader = null;
        //    mq = null;
        //}

    }
}
