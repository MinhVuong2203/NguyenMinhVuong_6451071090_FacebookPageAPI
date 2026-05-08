using Confluent.Kafka;
using Newtonsoft.Json;

namespace FacePageAPI.Service
{
    public class KafkaProducerService
    {
        private readonly IProducer<string, string> _producer;
        private readonly ILogger<KafkaProducerService> _logger;

        public KafkaProducerService(IConfiguration configuration, ILogger<KafkaProducerService> logger)
        {
            _logger = logger;

            var config = new ProducerConfig
            {
                BootstrapServers = configuration["Kafka:BootstrapServers"] ?? "localhost:9092",
                ClientId = "facebook-webhook-service"
            };

            _producer = new ProducerBuilder<string, string>(config).Build();
        }

        public async Task ProduceAsync(string topic, string key, object message)
        {
            try
            {
                var messageJson = JsonConvert.SerializeObject(message);

                var result = await _producer.ProduceAsync(topic, new Message<string, string>
                {
                    Key = key,
                    Value = messageJson
                });

                _logger.LogInformation($"Message sent to topic {topic}, partition {result.Partition}, offset {result.Offset}");
            }
            catch (ProduceException<string, string> ex)
            {
                _logger.LogError($"Failed to send message to Kafka: {ex.Error.Reason}");
                throw;
            }
        }

        public void Dispose()
        {
            _producer?.Flush(TimeSpan.FromSeconds(10));
            _producer?.Dispose();
        }
    }
}