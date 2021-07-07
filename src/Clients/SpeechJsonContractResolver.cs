using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Serialization;

namespace BatchSpeechToTextDemo
{
    public class SpeechJsonContractResolver : CamelCasePropertyNamesContractResolver
    {
        public SpeechJsonContractResolver()
        {
            this.NamingStrategy = new CamelCaseNamingStrategy
            {
                ProcessDictionaryKeys = true
            };
        }

        public static readonly SpeechJsonContractResolver ContractResolver = new SpeechJsonContractResolver();

        public static JsonSerializerSettings WriterSettings { get; } = new JsonSerializerSettings
        {
            ContractResolver = ContractResolver,
            Converters = new List<JsonConverter> { new StringEnumConverter() { AllowIntegerValues = false } },
            DateFormatString = "yyyy-MM-ddTHH\\:mm\\:ssZ",
            NullValueHandling = NullValueHandling.Ignore,
            Formatting = Formatting.Indented
        };

        public static JsonSerializerSettings ReaderSettings { get; } = new JsonSerializerSettings
        {
            ContractResolver = ContractResolver,
            Converters = new List<JsonConverter> { new StringEnumConverter() { AllowIntegerValues = true } },
        };
    }
}