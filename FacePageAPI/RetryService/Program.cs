using RetryService.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddSingleton<KafkaProducerService>();
builder.Services.AddSingleton<RetryStateService>();
builder.Services.AddHostedService<SendFailedConsumerService>();

builder.Logging.ClearProviders();
builder.Logging.AddConsole();
builder.Logging.SetMinimumLevel(LogLevel.Information);

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.UseAuthorization();
app.MapControllers();

Console.WriteLine("Retry Service starting on port 3003...");
Console.WriteLine("Retry Service consumes send_failed and publishes send_retry/dead_letter.");
app.Run();
