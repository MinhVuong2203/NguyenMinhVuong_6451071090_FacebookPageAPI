
using FacePageAPI.Service;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Register Kafka Producer as Singleton
builder.Services.AddSingleton<KafkaProducerService>();

builder.Services.AddSingleton<GeminiAIService>();
builder.Services.AddSingleton<SpamDetectionService>();
builder.Services.AddSingleton<FacebookAPIService>();
builder.Services.AddSingleton<StateManagementService>();
builder.Services.AddSingleton<ActionExecutorService>();

// Register Kafka Consumer as Hosted Service (runs in background)
builder.Services.AddHostedService<KafkaConsumerService>();

// Add logging
builder.Logging.ClearProviders();
builder.Logging.AddConsole();
builder.Logging.SetMinimumLevel(LogLevel.Information);

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

Console.WriteLine("🚀 Core Service starting...");
Console.WriteLine("📨 Kafka Consumer will start processing events from raw_events topic");
app.Run();
