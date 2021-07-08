using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using BatchClient;
using BatchSpeechToTextDemo.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;

namespace BatchSpeechToTextDemo.Services
{
    public class SpeechService
    {
        private const string Locale = "en-US";
        private const string DisplayName = "Simple transcription";
        
        private readonly ILogger<SpeechService> _logger;
        private readonly BatchClient _client;
        private readonly Uri _audioBlobContainer;
        
        public SpeechService(ILogger<SpeechService> logger, BatchClient batchClient, IOptions<SpeechServiceOptions> options)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _client = batchClient ?? throw new ArgumentNullException(nameof(batchClient));
            
            var containerUrl = options.Value?.AudioBlobContainer ?? throw new ArgumentNullException("AudioBlobContainer");
            _audioBlobContainer = new Uri(containerUrl);
        }
        
        public async Task TranscribeAsync()
        {
            await DeleteExistingCompletedTranscriptions();

            var newTranscription = await CreateTranscription();

            // get the transcription Id from the location URI
            var createdTranscriptions = new List<Uri> { newTranscription.Self };

            Console.WriteLine("Checking status.");
            await PollTranscriptionResults(createdTranscriptions);
        }

        private async Task<Transcription> CreateTranscription()
        {
            // <transcriptiondefinition>
            var newTranscription = new Transcription
            {
                DisplayName = DisplayName, 
                Locale = Locale, 
                ContentContainerUrl = _audioBlobContainer,
                Properties = new TranscriptionProperties
                {
                    IsWordLevelTimestampsEnabled = true,
                    TimeToLive = TimeSpan.FromDays(1)
                }
            };

            newTranscription = await _client.CreateTranscriptionAsync(newTranscription).ConfigureAwait(false);
            Console.WriteLine($"Created transcription {newTranscription.Self}");

            return newTranscription;
        }
        
        private async Task GetTranscriptionResults(Transcription transcription)
        {
            // if the transcription was successful, check the results
            if (transcription.Status == "Succeeded")
            {
                var paginatedFiles = await _client.GetTranscriptionFilesAsync(transcription.Links.Files).ConfigureAwait(false);

                var results = paginatedFiles.Values.Where(f => f.Kind == ArtifactKind.Transcription).ToList();
                Console.WriteLine($"Transcription succeeded. { results.Count() } Results: ");
                foreach (var resultFile in results)
                {
                    var result = await _client.GetTranscriptionResultAsync(new Uri(resultFile.Links.ContentUrl)).ConfigureAwait(false);
                    
                    Console.WriteLine($"==== File: {result.Source}. Combined recognized phrases:");
                    Console.WriteLine(JsonConvert.SerializeObject(result.CombinedRecognizedPhrases, SpeechJsonContractResolver.WriterSettings));
                }
            }
            else
            {
                Console.WriteLine("Transcription failed. Status: {0}", transcription.Properties.Error.Message);
            }
        }
        
        private async Task PollTranscriptionResults(List<Uri> createdTranscriptions)
        {
            // get the status of our transcriptions periodically and log results
            int completed = 0, running = 0, notStarted = 0;
            while (completed < 1)
            {
                completed = 0;
                running = 0;
                notStarted = 0;

                // get all transcriptions for the user
                PaginatedTranscriptions paginatedTranscriptions = null;
                do
                {
                    // <transcriptionstatus>
                    paginatedTranscriptions = await _client.GetTranscriptionsAsync(paginatedTranscriptions?.NextLink)
                        .ConfigureAwait(false);

                    // delete all pre-existing completed transcriptions. If transcriptions are still running or not started, they will not be deleted
                    foreach (var transcription in paginatedTranscriptions.Values)
                    {
                        switch (transcription.Status)
                        {
                            case "Succeeded":
                                // we check to see if it was one of the transcriptions we created from this _client.
                                if (!createdTranscriptions.Contains(transcription.Self))
                                {
                                    // not created form here, continue
                                    continue;
                                }

                                completed++;

                                // if the transcription was successful, check the results
                                await GetTranscriptionResults(transcription);

                                break;

                            case "Running":
                                running++;
                                break;

                            case "NotStarted":
                                notStarted++;
                                break;
                        }
                    }

                    // for each transcription in the list we check the status
                    Console.WriteLine(
                        $"Transcriptions status: {completed} completed, {running} running, {notStarted} not started yet");
                } while (paginatedTranscriptions.NextLink != null);

                // </transcriptionstatus>
                // check again after 1 minute
                Console.WriteLine("Waiting 1 minute for Transcription results...");
                await Task.Delay(TimeSpan.FromMinutes(1)).ConfigureAwait(false);
            }
        }

        private async Task DeleteExistingCompletedTranscriptions()
        {
            Console.WriteLine("Deleting all existing completed transcriptions.");

            // get all transcriptions for the subscription
            PaginatedTranscriptions paginatedTranscriptions = null;
            do
            {
                paginatedTranscriptions = await _client.GetTranscriptionsAsync(paginatedTranscriptions?.NextLink).ConfigureAwait(false);

                // delete all pre-existing completed transcriptions. If transcriptions are still running or not started, they will not be deleted
                foreach (var transcriptionToDelete in paginatedTranscriptions.Values)
                {
                    // delete a transcription
                    await _client.DeleteTranscriptionAsync(transcriptionToDelete.Self).ConfigureAwait(false);
                    Console.WriteLine($"Deleted transcription {transcriptionToDelete.Self}");
                }
            }
            while (paginatedTranscriptions.NextLink != null);
        }
    }
}