using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;

namespace Ably
{
    public class AblyRequest
    {
        public AblyRequest(string path, HttpMethod method)
        {
            Path = path;
            PostParameters = new Dictionary<string, string>();
            QueryParameters = new Dictionary<string, string>();
            Headers = new Dictionary<string, string>();
            Method = method;
        }

        public string Path { get; private set; }
        public HttpMethod Method { get; private set; }

        public Dictionary<string, string> Headers { get; private set; }
        public Dictionary<string, string> QueryParameters { get; private set; }
        public Dictionary<string, string> PostParameters { get; private set; }
    }
}
