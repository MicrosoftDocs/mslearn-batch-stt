using System;
using System.Net.Http;
using System.Net.Http.Formatting;
using System.Threading.Tasks;
using BatchClient;
using BatchSpeechToTextDemo.Models;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using Polly;
using Polly.Retry;

namespace BatchSpeechToTextDemo
{
    public class BatchClient
    {
        private const string SpeechToTextBasePath = "speechtotext/v3.0/";
        private const int MaxNumberOfRetries = 5;

        private readonly HttpClient _client;

        private static readonly AsyncRetryPolicy<HttpResponseMessage> TransientFailureRetryingPolicy = Policy
            .Handle<HttpRequestException>()
            .Or<TaskCanceledException>()
            .OrResult<HttpResponseMessage>(x => !x.IsSuccessStatusCode && (int)x.StatusCode == 429)
            .WaitAndRetryAsync(MaxNumberOfRetries, retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)), (result, timeSpan, retryCount, context) =>
            {
                Console.WriteLine($"Request failed with {result.Exception?.ToString() ?? result.Result?.StatusCode.ToString()}. Waiting {timeSpan} before next retry. Retry attempt {retryCount}");
            });

        public BatchClient(IHttpClientFactory httpClientFactory, IOptions<SpeechServiceOptions> options)
        {
            this._client = httpClientFactory.CreateClient();
            SetupApiV3Client(options.Value.ApiKey, options.Value.Region);
        }
        
        public Task<PaginatedTranscriptions> GetTranscriptionsAsync()
        {
            var path = $"{SpeechToTextBasePath}transcriptions";
            return this.GetAsync<PaginatedTranscriptions>(path);
        }
        
        public Task<PaginatedTranscriptions> GetTranscriptionsAsync(Uri location)
        {
            if (location == null)
            {
                return this.GetTranscriptionsAsync();
            }
            
            return this.GetAsync<PaginatedTranscriptions>(location.PathAndQuery);
        }

        public Task<PaginatedFiles> GetTranscriptionFilesAsync(Uri location)
        {
            if (location == null)
            {
                throw new ArgumentNullException(nameof(location));
            }

            return this.GetAsync<PaginatedFiles>(location.PathAndQuery);
        }
        
        public async Task<RecognitionResults> GetTranscriptionResultAsync(Uri location)
        {
            if (location == null)
            {
                throw new ArgumentNullException(nameof(location));
            }

            var response = await TransientFailureRetryingPolicy
                .ExecuteAsync(async () => await this._client.GetAsync(location).ConfigureAwait(false))
                .ConfigureAwait(false);

            if (response.IsSuccessStatusCode)
            {
                var json = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                return JsonConvert.DeserializeObject<RecognitionResults>(json, SpeechJsonContractResolver.ReaderSettings);
            }

            throw await FailedHttpClientRequestException.CreateExceptionAsync(response);
        }

        public Task<Transcription> CreateTranscriptionAsync(Transcription transcription)
        {
            if (transcription == null)
            {
                throw new ArgumentNullException(nameof(transcription));
            }

            var path = $"{SpeechToTextBasePath}transcriptions/";

            return this.PostAsJsonAsync<Transcription, Transcription>(path, transcription);
        }

        public Task DeleteTranscriptionAsync(Uri location)
        {
            if (location == null)
            {
                throw new ArgumentNullException(nameof(location));
            }

            return TransientFailureRetryingPolicy
                .ExecuteAsync(() => this._client.DeleteAsync(location.PathAndQuery));
        }

        private void SetupApiV3Client(string key, string region)
        {
            var hostName = $"{region}.api.cognitive.microsoft.com";
            this._client.Timeout = TimeSpan.FromMinutes(25);
            this._client.BaseAddress = new UriBuilder(Uri.UriSchemeHttps, hostName).Uri;
            this._client.DefaultRequestHeaders.Add("Ocp-Apim-Subscription-Key", key);
        }

        private async Task<TResponse> PostAsJsonAsync<TPayload, TResponse>(string path, TPayload payload)
        {
            string json = JsonConvert.SerializeObject(payload, SpeechJsonContractResolver.WriterSettings);
            StringContent content = new StringContent(json);
            content.Headers.ContentType = JsonMediaTypeFormatter.DefaultMediaType;

            var response = await TransientFailureRetryingPolicy
                .ExecuteAsync(() => this._client.PostAsync(path, content))
                .ConfigureAwait(false);

            if (response.IsSuccessStatusCode)
            {
                return await response.Content.ReadAsAsync<TResponse>(
                    new[]
                    {
                        new JsonMediaTypeFormatter
                        {
                            SerializerSettings = SpeechJsonContractResolver.ReaderSettings
                        }
                    }).ConfigureAwait(false);
            }

            throw await FailedHttpClientRequestException.CreateExceptionAsync(response).ConfigureAwait(false);
        }

        private async Task<TResponse> GetAsync<TResponse>(string path)
        {
            var response = await TransientFailureRetryingPolicy
                .ExecuteAsync(async () => await this._client.GetAsync(path).ConfigureAwait(false))
                .ConfigureAwait(false);

            if (response.IsSuccessStatusCode)
            {
                var result = await response.Content.ReadAsAsync<TResponse>().ConfigureAwait(false);

                return result;
            }

            throw await FailedHttpClientRequestException.CreateExceptionAsync(response);
        }
    }
}