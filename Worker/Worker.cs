using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using Shared;
using System.Text;
using System.Text.Json;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Processing;


namespace Worker;

public class ImageWorker : BackgroundService
{
    private readonly ILogger<ImageWorker> _logger;
    private IConnection? _connection;
    private IModel? _channel;
    private readonly IConfiguration _config;

    public ImageWorker(ILogger<ImageWorker> logger, IConfiguration config)
    {
        _logger = logger;
        _config = config;
        InitRabbitMQ();
    }

    private void InitRabbitMQ()
    {
        var retryCount = 5;
        var factory = new ConnectionFactory() { HostName = _config["RabbitMQ:HostName"] ?? "localhost" };

        for (int i = 0; i < retryCount; i++)
        {
            try
            {
                _connection = factory.CreateConnection();
                _channel = _connection.CreateModel();
                _channel.QueueDeclare(queue: "image_tasks", durable: true, exclusive: false, autoDelete: false, arguments: null);

                _logger.LogInformation("RabbitMQ baðlantýsý kuruldu.");
                return;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"RabbitMQ baðlantý hatasý (deneme {i + 1}/{retryCount})");
                Thread.Sleep(5000); // 5 saniye bekle
            }
        }

        throw new Exception("RabbitMQ baðlantýsý kurulamadý. Maksimum deneme hakký aþýldý.");
    }


    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var consumer = new EventingBasicConsumer(_channel);
        consumer.Received += async (model, ea) =>
        {
            var body = ea.Body.ToArray();
            var json = Encoding.UTF8.GetString(body);
            var message = JsonSerializer.Deserialize<ImageTaskMessage>(json);

            if (message != null)
            {
                _logger.LogInformation($"Mesaj alýndý: {message.FileName}, {message.Operation}, {message.Width}x{message.Height}");

                await ProcessImageAsync(message);
            }

            _channel.BasicAck(deliveryTag: ea.DeliveryTag, multiple: false);
        };

        _channel.BasicConsume(queue: "image_tasks", autoAck: false, consumer: consumer);

        return Task.CompletedTask;
    }

    private async Task ProcessImageAsync(ImageTaskMessage msg)
    {
        var uploadsDir = Path.Combine(Directory.GetCurrentDirectory(), "uploads");
        var processedDir = Path.Combine(Directory.GetCurrentDirectory(), "uploads", "processed");


        Directory.CreateDirectory(processedDir);

        var sourcePath = Path.Combine(uploadsDir, msg.FileName); 
        var targetPath = Path.Combine(processedDir, msg.FileName);


        if (!File.Exists(sourcePath))
        {
            _logger.LogWarning($"Görsel bulunamadý: {sourcePath}");
            return;
        }

        try
        {
            using var image = Image.Load(sourcePath); // ImageSharp
            image.Mutate(x => x.Resize(msg.Width, msg.Height));
            await image.SaveAsync(targetPath);
            _logger.LogInformation($"Görsel iþlendi ve kaydedildi: {targetPath}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Görsel iþleme sýrasýnda hata oluþtu");
        }
    }

    public override void Dispose()
    {
        _channel?.Close();
        _connection?.Close();
        base.Dispose();
    }
}
