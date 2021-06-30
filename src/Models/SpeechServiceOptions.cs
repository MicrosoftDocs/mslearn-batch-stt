using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace BatchSpeechToTextDemo.Models
{
    public class SpeechServiceOptions
    {
        public string Region { get; set; }

        public string ApiKey { get; set; }

        public string AudioBlobContainer { get; set; }
    }
}
