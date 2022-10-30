﻿using NetworkLibrary.Utils;
using ProtoBuf;
using ProtoBuf.Meta;
using Protobuff;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;

namespace Protobuff
{
    public class ConcurrentProtoSerialiser
    {

        private ConcurrentBag<MemoryStream> streamPool = new ConcurrentBag<MemoryStream>();
        public ConcurrentProtoSerialiser()
        {
            streamPool.Add(new MemoryStream());

        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]

        public byte[] Serialize<T>(T record)
        {
            if (!streamPool.TryTake(out MemoryStream serialisationStream))
            {
                serialisationStream = new MemoryStream();
            }

            Serializer.Serialize(serialisationStream, record);
            var buffer = serialisationStream.GetBuffer();
            var ret = ByteCopy.ToArray(buffer, 0, (int)serialisationStream.Position);
            serialisationStream.Position = 0;

            streamPool.Add(serialisationStream);
                return ret;
            
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]

        public bool SerializeInto<T>(T record, ref byte[] buffer, int offset, out int count)
        {
            var serialisationStream = new MemoryStream(buffer, offset, buffer.Length, writable: true);
            Serializer.Serialize(serialisationStream, record);
           // buffer = serialisationStream.GetBuffer();
            count = (int)serialisationStream.Position;

            return true;
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]

        public T Deserialize<T>(byte[] data) where T : class
        {
            if (null == data) 
                return null;

            ReadOnlySpan<byte> seq = new ReadOnlySpan<byte>(data);
            return Serializer.Deserialize<T>(seq);                
            
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public T Deserialize<T>(byte[] data, int offset, int count) where T : class
        {
            if (null == data)
                return null;

            ReadOnlySpan<byte> seq = new ReadOnlySpan<byte>(data, offset, count);
            return Serializer.Deserialize<T>(seq);
        }

        //public MessageEnvelope EnvelopeMessage<T>(MessageEnvelope empyEnvelope,T payload)
        //{
        //    empyEnvelope.Payload = Serialize(payload);
        //    return empyEnvelope;
        //}
        


        public T UnpackEnvelopedMessage<T>(MessageEnvelope fullEnvelope) where T : class
        {
            return Deserialize<T>(fullEnvelope.Payload);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public MessageEnvelope DeserialiseEnvelopedMessage(byte[] buffer, int offset, int count)
        {
            ushort secondStart =BitConverter.ToUInt16(buffer, offset);
            
            if(secondStart == 0)
            {
                ReadOnlySpan<byte> seq0 = new ReadOnlySpan<byte>(buffer, offset+2, count-  2);
                return Serializer.Deserialize<MessageEnvelope>(seq0);

            }

            ReadOnlySpan<byte> seq = new ReadOnlySpan<byte>(buffer, offset + 2,  secondStart-2);
            var envelope = Serializer.Deserialize<MessageEnvelope>(seq);
            envelope.Payload = ByteCopy.ToArray(buffer, offset + secondStart,count- secondStart);

            return envelope;
        }


        [MethodImpl(MethodImplOptions.AggressiveInlining)]

        public MessageEnvelope DeserialiseOnlyEnvelope(byte[] buffer, int offset, int count)
        {
            ushort secondStart = BitConverter.ToUInt16(buffer, offset + 0);
            if (secondStart == 0)
            {
                ReadOnlySpan<byte> seq0 = new ReadOnlySpan<byte>(buffer, offset + 2, count -  2);
                return Serializer.Deserialize<MessageEnvelope>(seq0);

            }
            ReadOnlySpan<byte> seq = new ReadOnlySpan<byte>(buffer, offset + 2, secondStart-2);
            var envelope = Serializer.Deserialize<MessageEnvelope>(seq);
            return envelope;
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]

        public T DeserialiseOnlyPayload<T>(byte[] buffer, int offset, int count)
        {
            ushort secondStart = BitConverter.ToUInt16(buffer, offset);

            ReadOnlySpan<byte> seq = new ReadOnlySpan<byte>(buffer, offset + secondStart, count - secondStart);
            var payload = Serializer.Deserialize<T>(seq);
            return payload;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool GetPayloadSlice(byte[] buffer, ref int offset, ref int count)
        {
            ushort secondStart = BitConverter.ToUInt16(buffer, offset);

            offset += secondStart;
            count -=secondStart;
            if(secondStart == 0)
                return false;
            return true;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public byte[] SerialiseEnvelopedMessage(MessageEnvelope message)
        {
            if (message.Payload == null)
                return EnvelopeAndSerialiseMessage(message, null, 0, 0);
            else 
                return EnvelopeAndSerialiseMessage(message, message.Payload, 0, message.Payload.Length);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public byte[] EnvelopeAndSerialiseMessage<T>(MessageEnvelope empyEnvelope, T payload) where T : class
        {
            if (!streamPool.TryTake(out MemoryStream serialisationStream))
            {
                serialisationStream = new MemoryStream();
            }
            EnvelopeMessageWithInnerMessage(serialisationStream, empyEnvelope, payload);
            var ret = ByteCopy.ToArray(serialisationStream.GetBuffer(), 0, (int)serialisationStream.Position);

            streamPool.Add(serialisationStream);
            return ret;

        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void EnvelopeMessageWithInnerMessage<T>(MemoryStream serialisationStream, MessageEnvelope empyEnvelope, T payload) where T : class
        {
            serialisationStream.Position = 2;
            Serializer.Serialize(serialisationStream, empyEnvelope);
            ushort oldpos = (ushort)serialisationStream.Position;//msglen +2

            if (payload == null)
            {
                serialisationStream.Position = 0;
                serialisationStream.Write(new byte[2], 0, 2);
                serialisationStream.Position = oldpos;
                return;


            }

            var envLen = BitConverter.GetBytes(oldpos);
            serialisationStream.Position = 0;
            serialisationStream.Write(envLen, 0, 2);
            serialisationStream.Position = oldpos;

            Serializer.Serialize(serialisationStream, payload);

           
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public byte[] EnvelopeAndSerialiseMessage(MessageEnvelope empyEnvelope, byte[] payloadBuffer, int offset, int count)
        {
            if (!streamPool.TryTake(out MemoryStream serialisationStream))
            {
                serialisationStream = new MemoryStream();
            }
            EnvelopeMessageWithBytes( serialisationStream,  empyEnvelope, payloadBuffer, offset, count);
            var ret = ByteCopy.ToArray(serialisationStream.GetBuffer(), 0, (int)serialisationStream.Position);

            streamPool.Add(serialisationStream);
            return ret;

        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void EnvelopeMessageWithBytes(MemoryStream serialisationStream, MessageEnvelope empyEnvelope, byte[] payloadBuffer, int offset, int count)
        {
            
            //empyEnvelope.Payload = null;

            serialisationStream.Position = 2;
            Serializer.Serialize(serialisationStream, empyEnvelope);
            ushort oldpos = (ushort)serialisationStream.Position;//msglen +4

            if (payloadBuffer == null)
            {
                serialisationStream.Position = 0;
                serialisationStream.Write(new byte[2], 0, 2);
                serialisationStream.Position = oldpos;
                return;
            }

            var secondStartsAt = BitConverter.GetBytes(oldpos);
            serialisationStream.Position = 0;
            serialisationStream.Write(secondStartsAt, 0, 2);
            serialisationStream.Position = oldpos;

            serialisationStream.Write(payloadBuffer, offset, count);

           
        }
    }
}

