using System;

namespace BatchSpeechToTextDemo.Models
{
    public class SpeechServiceOptions
    {
        public string Region { get; set; }

        public string ApiKey { get; set; }

        public Uri AudioBlobContainer { get; set; }

        public Uri CustomModel { get; set; }
    }
}
