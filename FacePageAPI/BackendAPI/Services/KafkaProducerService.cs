using Confluent.Kafka;
using System.Text.Json;

namespace BackendAPI.Services
{
    public class KafkaProducerService : IDisposable
    {
        private readonly IProducer<string, string> _producer;
        private readonly ILogger<KafkaProducerService> _logger;
        private readonly JsonSerializerOptions _jsonOptions;

        public KafkaProducerService(IConfiguration configuration, ILogger<KafkaProducerService> logger)
        {
            _logger = logger;
            _jsonOptions = new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            };

            var config = new ProducerConfig
            {
                BootstrapServers = configuration["Kafka:BootstrapServers"] ?? "localhost:9092",
                ClientId = "backend-api"
            };

            _producer = new ProducerBuilder<string, string>(config).Build();
        }

        public async Task ProduceAsync(string topic, string key, object message)
        {
            var messageJson = JsonSerializer.Serialize(message, _jsonOptions);

            var result = await _producer.ProduceAsync(topic, new Message<string, string>
            {
                Key = key,
                Value = messageJson
            });

            _logger.LogInformation($"Message sent to topic {topic}, partition {result.Partition}, offset {result.Offset}");
        }

        public void Dispose()
        {
            _producer.Flush(TimeSpan.FromSeconds(10));
            _producer.Dispose();
        }
    }
}
