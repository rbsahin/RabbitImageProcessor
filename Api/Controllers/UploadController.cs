using Microsoft.AspNetCore.Connections;
using Microsoft.AspNetCore.Mvc;
using RabbitMQ.Client;
using Shared;
using System.Text;
using System.Text.Json;

namespace Api.Controllers;

[ApiController]
[Route("[controller]")]
public class UploadController : ControllerBase
{
    private readonly IWebHostEnvironment _env;
    private readonly IConfiguration _config;

    public UploadController(IWebHostEnvironment env, IConfiguration config)
    {
        _env = env;
        _config = config;
    }

    [HttpPost]
    public async Task<IActionResult> Upload(IFormFile file, int width, int height)
    {
        if (file == null || file.Length == 0)
            return BadRequest("Dosya yüklenmedi.");

        var uploadsPath = Path.Combine(_env.ContentRootPath, "uploads");
        Directory.CreateDirectory(uploadsPath);

        var fileName = $"{Guid.NewGuid()}_{file.FileName}";
        var filePath = Path.Combine(uploadsPath, fileName);

        using (var stream = new FileStream(filePath, FileMode.Create))
        {
            await file.CopyToAsync(stream);
        }

        var message = new ImageTaskMessage
        {
            FileName = fileName,
            Width = width,
            Height = height,
            Operation = "resize"
        };

        SendMessageToQueue(message);

        return Ok(new { message = "Yüklendi ve kuyruða eklendi", fileName });
    }

    private void SendMessageToQueue(ImageTaskMessage msg)
    {
        var factory = new RabbitMQ.Client.ConnectionFactory()
        {
            HostName = _config["RabbitMQ:HostName"] ?? "localhost"
        };

        using var connection = factory.CreateConnection();
        using var channel = connection.CreateModel();

        channel.QueueDeclare(queue: "image_tasks", durable: true, exclusive: false, autoDelete: false, arguments: null);

        var body = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(msg));

        channel.BasicPublish(exchange: "", routingKey: "image_tasks", basicProperties: null, body: body);
    }

    [HttpGet("processed/{fileName}")]
    public IActionResult GetProcessedImage(string fileName)
    {
        var processedPath = Path.Combine(_env.ContentRootPath, "uploads", "processed", fileName);

        if (!System.IO.File.Exists(processedPath))
        {
            return NotFound("Ýþlenmiþ görsel bulunamadý.");
        }

        var contentType = "image/" + Path.GetExtension(fileName).TrimStart('.');
        var fileBytes = System.IO.File.ReadAllBytes(processedPath);

        return File(fileBytes, contentType, fileName);
    }


}
