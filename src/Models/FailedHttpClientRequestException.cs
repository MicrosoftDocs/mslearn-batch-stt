using System.Net.Http;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace BatchSpeechToTextDemo.Models
{
    using System;
    using System.Net;
    using System.Runtime.Serialization;

    [Serializable]
    public sealed class FailedHttpClientRequestException : Exception
    {
        public FailedHttpClientRequestException()
        {
            this.StatusCode = HttpStatusCode.Unused;
        }

        public FailedHttpClientRequestException(string message)
            : base(message)
        {
            this.StatusCode = HttpStatusCode.Unused;
        }

        public FailedHttpClientRequestException(string message, Exception exception)
            : base(message, exception)
        {
            this.StatusCode = HttpStatusCode.Unused;
        }

        public FailedHttpClientRequestException(HttpStatusCode status, string reasonPhrase)
            : base(reasonPhrase)
        {
            this.StatusCode = status;
        }

        private FailedHttpClientRequestException(SerializationInfo info, StreamingContext context)
            : base(info, context)
        {
            this.StatusCode = (HttpStatusCode)info.GetValue(nameof(this.StatusCode), typeof(HttpStatusCode));
        }

        public HttpStatusCode StatusCode { get; private set; }

        public string ReasonPhrase => this.Message;

        public override void GetObjectData(SerializationInfo info, StreamingContext context)
        {
            if (info != null)
            {
                info.AddValue(nameof(this.StatusCode), this.StatusCode);
                base.GetObjectData(info, context);
            }
        }
        
        public static async Task<FailedHttpClientRequestException> CreateExceptionAsync(HttpResponseMessage response)
        {
            switch (response.StatusCode)
            {
                case HttpStatusCode.Forbidden:
                    return new FailedHttpClientRequestException(response.StatusCode, "No permission to access this resource.");
                case HttpStatusCode.Unauthorized:
                    return new FailedHttpClientRequestException(response.StatusCode, "Not authorized to see the resource.");
                case HttpStatusCode.NotFound:
                    return new FailedHttpClientRequestException(response.StatusCode, "The resource could not be found.");
                case HttpStatusCode.UnsupportedMediaType:
                    return new FailedHttpClientRequestException(response.StatusCode, "The file type isn't supported.");
                case HttpStatusCode.BadRequest:
                {
                    var content = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                    var shape = new { Message = string.Empty };
                    var result = JsonConvert.DeserializeAnonymousType(content, shape);
                    if (result != null && !string.IsNullOrEmpty(result.Message))
                    {
                        return new FailedHttpClientRequestException(response.StatusCode, result.Message);
                    }

                    return new FailedHttpClientRequestException(response.StatusCode, response.ReasonPhrase);
                }

                default:
                    return new FailedHttpClientRequestException(response.StatusCode, response.ReasonPhrase);
            }
        }
    }
}