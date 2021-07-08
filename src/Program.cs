using System;
using System.Threading.Tasks;
using BatchSpeechToTextDemo.Models;
using BatchSpeechToTextDemo.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace BatchSpeechToTextDemo
{
    class Program
    {
        static async Task Main(string[] args)
        {
            using IHost host = CreateHostBuilder(args).Build();

            await ProcessBatchSTTRequests(host.Services);

            await host.RunAsync();
        }
        
        static async Task ProcessBatchSTTRequests(IServiceProvider services)
        {
            using IServiceScope serviceScope = services.CreateScope();
            IServiceProvider provider = serviceScope.ServiceProvider;
            
            var service = provider.GetRequiredService<SpeechService>();
            await service.TranscribeAsync();
            
            Console.WriteLine("Finished processing batch transcription.");
            Console.WriteLine("Press any key...");
            Console.ReadKey();
        }
        
        static IHostBuilder CreateHostBuilder(string[] args) =>
            Host.CreateDefaultBuilder(args)
                .ConfigureServices(ConfigureServices);

        static void ConfigureServices(HostBuilderContext hostContext, IServiceCollection services)
        {
            services.Configure<SpeechServiceOptions>(hostContext.Configuration.GetSection("SpeechService"));
            
            services.AddScoped<SpeechService>()
                .AddScoped<BatchClient>()
                .AddHttpClient();
        }
    }
}