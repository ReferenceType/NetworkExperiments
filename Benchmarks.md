# Benchmarks

## Serialization Benchmarks
In this section I will publish my analysis and test results about the performance of all available serializers for .Net. 
Basically the idea is to echo a message between client and server. The message I'm using here is "MessageEnvelope", where I build my protocol around it.
The message structure looks as follows:
```c#
 public class MessageEnvelope : IMessageEnvelope
    {
        public bool IsInternal {get; set;}
        public DateTime TimeStamp { get; set; }
        public Guid MessageId { get; set; }
        public string Header { get; set; }
        public Guid From { get; set; }
        public Guid To { get; set; }
        public Dictionary<string, string> KeyValuePairs { get; set; }
 }
```
What I expect from a serialiser is to be fast, ignore the default fields/properies, and have mininmum possible serialization size.
During the tests following message is used:
``` c#
 clientMessage = new MessageEnvelope()
            {
                Header = "Test",
                Payload = new byte[32],
                From = Guid.NewGuid(),
                To = Guid.NewGuid(),
            };
```
Payload of bytes is independed of serialization, it is directly written by internal protocol right after the serialized message bytes.

<br>The byte structure looks as follows:
<br><img src="https://user-images.githubusercontent.com/109621184/235491942-422dfdc0-0bdd-4bf9-b49e-d831f1f53c56.png" width=60% height=60%>
<br>It is done this way, because i can directly write region of bytes ``` (byte[], offset, count)``` from a byte source direcly without making a new array.

Server/ Client model is identical for all serializer types, Entire logical layer is generic.
## Results
100 Client sends 1000 message to server initially, server replies same message to clients.
For each received reply clients send a new message. This gives us high load infinite echo
|Serializer|Serialized Message Size|Echo Throughput|
|-----------------|---------------|------------|
|Binary Serializer|814 bytes      |114k  Msg/s |
|Data Contract    |236 bytes      |470k  Msg/s |
|System.Text.Json |141 bytes      |2.7m  Msg/s |
|MessagePack      |172 bytes      |3.05m Msg/s |
|Protobuf-net     |86 bytes       |3.5m  Msg/s |
|NetSerializer    |91 bytes       |4.1m  Msg/s |
|CustomSerializer |72 bytes       |9.06m Msg/s |

- Binary and data contract serialisation is too slow and too large.
- Json considering its text based, shows more than expected performance.
- MessagePack does not skip serialization of default properties, Hence the size is large.
- NetSerializer Is the best among all, but usage is quite constrained.
- Protobuf offers the best of all worlds, reasonable performance with a lot of features and small message size.
- Custom Serializer is only meant to serialize/deserialize MessageEnvelope. This "Manual" serialization offers
 the best performance when the type is well known and not subject to change.

### Custom Serializer
- It is a simple serializer where I index each property with a variant during serialization.
- i.e. 8 bit index bit 0 represend wether the first property is serialized or skipped (default values are skipped) bit 1-> second property etc.
- When deserializing this index is read and related properties is deserialized from the bytes in order.
- All primitives are zigzag encoded variants. This allows significant size rediction on small numbers.
- Strings are Utf8 encoded and has a header(variant) to indicate their lenght.
- Guid is encoded as two fixed int64
If i will ever have time i will do full serializer maybe calling it YetAnotherSerializer

## Conclusion

In Protobuff assembly, I will use the custom serialization for the MessageEnvelope and for the rest of dynamic types using Protobuf-net.
