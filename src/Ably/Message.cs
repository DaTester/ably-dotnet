using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Ably
{
    public class Message
    {
        public string Name { get; set; }
        public string ChannelId { get; set; }
        public object Data { get; set; }

        public bool IsBinaryMessage
        {
            get
            {
                return Data is byte[];
            }
        }

        public T Value<T>()
        {
            object value = Value(typeof(T));
            if (value == null)
                return default(T);
            return (T)value;
        }

        public object Value(Type type)
        {
            if (IsBinaryMessage)
            {
                if (type == typeof(byte[]))
                    return (byte[])Data;
                else
                    throw new InvalidOperationException(String.Format("Current message contains binary data which cannot be converted to {0}", type));
            }

            if (Data == null)
                return null;
            return JsonConvert.DeserializeObject(Data.ToString(), type);
        }

        public DateTimeOffset TimeStamp { get; set; }
    }
}