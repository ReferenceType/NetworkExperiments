﻿using NetworkLibrary.Components;

namespace MessageProtocol
{
    public interface ISerialisableMessageQueue<U> : IMessageQueue where U : IMessageEnvelope
    {
        bool TryEnqueueMessage<T>(U envelope, T message);
        bool TryEnqueueMessage(U envelope);
    }
}
