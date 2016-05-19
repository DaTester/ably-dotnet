using IO.Ably.Rest;
using MsgPack;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.Linq;
using System.Runtime.CompilerServices;
using IO.Ably.Realtime;
using IO.Ably.Transport;
using IO.Ably.Types;

namespace IO.Ably.MessageEncoders
{
    internal class MessageHandler
    {
        private static readonly Type[] UnsupportedTypes = new[]
            {
                typeof(short), typeof(int), typeof(double), typeof(float), typeof(decimal), typeof(DateTime), typeof(DateTimeOffset), typeof(byte), typeof(bool),
                typeof(long), typeof(uint), typeof(ulong), typeof(ushort), typeof(sbyte)
            };
        private readonly Protocol _protocol;
        public readonly List<MessageEncoder> Encoders = new List<MessageEncoder>();

        public MessageHandler()
            : this(Protocol.MsgPack)
        {

        }

        public MessageHandler(Protocol protocol)
        {
            _protocol = protocol;

            InitializeMessageEncoders(protocol);
        }

        private void InitializeMessageEncoders(Protocol protocol)
        {
            Encoders.Add(new JsonEncoder(protocol));
            Encoders.Add(new Utf8Encoder(protocol));
            Encoders.Add(new CipherEncoder(protocol));
            Encoders.Add(new Base64Encoder(protocol));

            Logger.Debug(string.Format("Initializing message encodings. {0} initialized", string.Join(",", Encoders.Select(x => x.EncodingName))));
        }

        public T ParseMessagesResponse<T>(AblyResponse response) where T : class
        {
            if (response.Type == ResponseType.Json)
                return JsonConvert.DeserializeObject<T>(response.TextResponse);
            return default(T);
        }

        public IEnumerable<PresenceMessage> ParsePresenceMessages(AblyResponse response, ChannelOptions options)
        {
            if (response.Type == ResponseType.Json)
            {
                var messages = JsonConvert.DeserializeObject<List<PresenceMessage>>(response.TextResponse, Config.GetJsonSettings());
                ProcessMessages(messages, options);
                return messages;
            }

            var payloads = MsgPackHelper.DeSerialise(response.Body, typeof(List<PresenceMessage>)) as List<PresenceMessage>;
            ProcessMessages(payloads, options);
            return payloads;
        }

        public IEnumerable<Message> ParseMessagesResponse(AblyResponse response, ChannelOptions options)
        {
            Contract.Assert(options != null);

            if (response.Type == ResponseType.Json)
            {
                var messages = JsonConvert.DeserializeObject<List<Message>>(response.TextResponse, Config.GetJsonSettings());
                ProcessMessages(messages, options);
                return messages;
            }

            var payloads = MsgPackHelper.DeSerialise(response.Body, typeof(List<Message>)) as List<Message>;
            ProcessMessages(payloads, options);
            return payloads;
        }

        private void ProcessMessages<T>(IEnumerable<T> payloads, ChannelOptions options) where T : IEncodedMessage
        {
            DecodePayloads(options, payloads as IEnumerable<IEncodedMessage>);
        }

        public void SetRequestBody(AblyRequest request)
        {
            request.RequestBody = GetRequestBody(request);
        }

        public byte[] GetRequestBody(AblyRequest request)
        {
            if (request.PostData == null)
                return new byte[] { };

            if (request.PostData is IEnumerable<Message>)
                return GetMessagesRequestBody(request.PostData as IEnumerable<Message>,
                    request.ChannelOptions);

            if (_protocol == Protocol.Json)
                return JsonConvert.SerializeObject(request.PostData, Config.GetJsonSettings()).GetBytes();
            return MsgPackHelper.Serialise(request.PostData);
        }

        private byte[] GetMessagesRequestBody(IEnumerable<Message> payloads, ChannelOptions options)
        {
            EncodePayloads(options, payloads);

            if (_protocol == Protocol.MsgPack)
            {
                return MsgPackHelper.Serialise(payloads);
            }
            return JsonConvert.SerializeObject(payloads, Config.GetJsonSettings()).GetBytes();
        }

        internal void EncodePayloads(ChannelOptions options, IEnumerable<IEncodedMessage> payloads)
        {
            foreach (var payload in payloads)
                EncodePayload(payload, options);
        }

        internal void DecodePayloads(ChannelOptions options, IEnumerable<IEncodedMessage> payloads)
        {
            foreach (var payload in payloads)
                DecodePayload(payload, options);
        }

        private void EncodePayload(IEncodedMessage payload, ChannelOptions options)
        {
            ValidatePayloadDataType(payload);
            foreach (var encoder in Encoders)
            {
                encoder.Encode(payload, options);
            }
        }

        private void ValidatePayloadDataType(IEncodedMessage payload)
        {
            if (payload.data == null)
                return;

            var dataType = payload.data.GetType();
            var testType = GetNullableType(dataType) ?? dataType;
            if (UnsupportedTypes.Contains(testType))
            {
                throw new AblyException("Unsupported payload type. Only string, binarydata (byte[]) and objects convertable to json are supported being directly sent. This ensures that libraries in different languages work correctly. To send the requested value please create a DTO and pass the DTO as payload. For example if you are sending an '10' then create a class with one property; assign the value to the property and send it.");
            }
        }

        static Type GetNullableType(Type type)
        {
            if (type.IsValueType == false) return null; // ref-type
            return Nullable.GetUnderlyingType(type);
        }

        private void DecodePayload(IEncodedMessage payload, ChannelOptions options)
        {
            foreach (var encoder in (Encoders as IEnumerable<MessageEncoder>).Reverse())
            {
                encoder.Decode(payload, options);
            }
        }

        /// <summary>Parse paginated response using specified parser function.</summary>
        /// <typeparam name="T">Item type</typeparam>
        /// <param name="request"></param>
        /// <param name="response"></param>
        /// <param name="funcParse">Function to parse HTTP response into a sequence of items.</param>
        /// <returns></returns>
        internal static PaginatedResult<T> Paginated<T>(AblyRequest request, AblyResponse response, Func<AblyResponse, ChannelOptions, IEnumerable<T>> funcParse)
        {
            PaginatedResult<T> res = new PaginatedResult<T>(response.Headers, GetLimit(request));
            res.AddRange(funcParse(response, request.ChannelOptions));
            return res;
        }

        public T ParseResponse<T>(AblyRequest request, AblyResponse response) where T : class
        {
            LogResponse(response);
            if (typeof(T) == typeof(PaginatedResult<Message>))
                return Paginated(request, response, ParseMessagesResponse) as T;

            if (typeof(T) == typeof(PaginatedResult<Stats>))
                return Paginated(request, response, ParseStatsResponse) as T;

            if (typeof(T) == typeof(PaginatedResult<PresenceMessage>))
                return Paginated(request, response, ParsePresenceMessages) as T;

            var responseText = response.TextResponse;
            if (_protocol == Protocol.MsgPack)
            {
                // A bit of a hack. Message pack serializer does not like capability objects
                return (T)MsgPackHelper.DeSerialise(response.Body, typeof(T));
            }
            return (T)JsonConvert.DeserializeObject(responseText, typeof(T), Config.GetJsonSettings());
        }

        private void LogResponse(AblyResponse response)
        {
            if (Logger.IsDebug)
            {
                Logger.Info("Protocol:" + _protocol);
                try
                {
                    var responseBody = response.TextResponse;
                    if (_protocol == Protocol.MsgPack && response.Body != null)
                    {
                        responseBody = MsgPackHelper.DeSerialise(response.Body, typeof (MessagePackObject)).ToString();
                    }
                    Logger.Debug("Response: " + responseBody);
                }
                catch (Exception ex)
                {
                    Logger.Error("Error while logging response body.", ex);
                }
            }
        }

        private IEnumerable<Stats> ParseStatsResponse(AblyResponse response, ChannelOptions options)
        {
            var body = response.TextResponse;
            if (_protocol == Protocol.MsgPack)
            {
                return (List<Stats>)MsgPackHelper.DeSerialise(response.Body, typeof(List<Stats>));
            }
            return JsonConvert.DeserializeObject<List<Stats>>(body, Config.GetJsonSettings());
        }

        private static int GetLimit(AblyRequest request)
        {
            if (request.QueryParameters.ContainsKey("limit"))
            {
                var limitQuery = request.QueryParameters["limit"];
                if (limitQuery.IsNotEmpty())
                    return int.Parse(limitQuery);
            }
            return Defaults.QueryLimit;
        }

        public ProtocolMessage ParseRealtimeData(RealtimeTransportData data)
        {
            ProtocolMessage protocolMessage;
            if (_protocol == Protocol.MsgPack)
            {
                protocolMessage = (ProtocolMessage)MsgPackHelper.DeSerialise(data.Data, typeof(ProtocolMessage));
            }
            else
            {
                protocolMessage = JsonConvert.DeserializeObject<ProtocolMessage>(data.Text, Config.GetJsonSettings());
            }

            return protocolMessage;
        }

        public void EncodeProtocolMessage(ProtocolMessage protocolMessage, ChannelOptions channelOptions)
        {
            foreach (var message in protocolMessage.messages)
            {
                EncodePayload(message, channelOptions);
            }

            foreach (var presence in protocolMessage.presence)
            {
                EncodePayload(presence, channelOptions);
            }
        }

        public void DecodeProtocolMessage(ProtocolMessage protocolMessage, ChannelOptions channelOptions)
        {
            foreach (var message in protocolMessage.messages ?? new Message[] { })
            {
                DecodePayload(message, channelOptions);
            }
            foreach (var presence in protocolMessage.presence ?? new PresenceMessage[] { })
            {
                DecodePayload(presence, channelOptions);
            }
        }

        public RealtimeTransportData GetTransportData(ProtocolMessage protocolMessage)
        {
            RealtimeTransportData data;
            if (_protocol == Protocol.MsgPack)
            {
                var bytes= MsgPackHelper.Serialise(protocolMessage);
                data = new RealtimeTransportData(bytes) { Original = protocolMessage };
            }
            else
            {
                var text = JsonConvert.SerializeObject(protocolMessage, Config.GetJsonSettings());
                data = new RealtimeTransportData(text) { Original = protocolMessage };
            }

            return data;
        }
    }
}
