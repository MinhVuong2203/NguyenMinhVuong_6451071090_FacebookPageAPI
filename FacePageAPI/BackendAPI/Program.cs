using BackendAPI.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddHttpClient<FacebookGraphApiClient>();
builder.Services.AddSingleton<IdempotencyStore>();
builder.Services.AddSingleton<KafkaProducerService>();
builder.Services.AddHostedService<ReplyCommandConsumerService>();

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

Console.WriteLine("Backend API starting on port 3000...");
Console.WriteLine("Backend API consumes reply_commands/send_retry and publishes send_failed.");
app.Run();
