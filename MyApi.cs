using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using System.Text.Json;
using Azure.Storage.Queues;
using Microsoft.Extensions.Configuration;
using System.Text;

namespace Company.Function;

public class Message
{
    public string Content {get; set;} = "";
}

public class MyApi
{
    private readonly ILogger<MyApi> _logger;
    private string? ConnectionString;
    private const string QueueName = "practice-queue";
    
   // Constructor to inject the logger
    public MyApi(ILogger<MyApi> logger, IConfiguration configuration)
    {
        _logger = logger;
        ConnectionString = configuration["AzureWebJobsStorage"] ?? "UseDevelopmentStorage=true";
    }

    [Function("Echo")]
    public IActionResult RunEcho([HttpTrigger(AuthorizationLevel.Function, "get", Route="echo/{message}")] HttpRequest req, string message)
    {
        _logger.LogInformation("Echo function processed a request.");

        // Make sure that the echo parameter is something that is actuaally provided, otherwise we will return a bad request response
        if (string.IsNullOrEmpty(message))
        {
            return new BadRequestObjectResult(new { error = "Message parameter is required" });
        }

        var response = new {input=message, status="success"};
        return new OkObjectResult(response);
    }

    [Function("Echo2")]
    public IActionResult RunEcho2([HttpTrigger(AuthorizationLevel.Function, "get", Route="echo2/{message}")] HttpRequest req, string message)
    {
        _logger.LogInformation("Echo function processed a request.");
        
        var response = new {input=message, status="success2"};
        return new OkObjectResult(response);
    }

    [Function("PushMessage")]
    public async Task<IActionResult> RunPush([HttpTrigger(AuthorizationLevel.Function, "post", Route="push")] HttpRequest req)
    {
        _logger.LogInformation("PushMessage function processed a request.");
        
        string requestBody = await new StreamReader(req.Body).ReadToEndAsync();
        
        //queue is going to act like a message queue
        if (string.IsNullOrEmpty(requestBody))
        {
            return new BadRequestObjectResult(new { error = "Request body is required" });
        } 

        Message message = new Message { Content = requestBody };
        
        //creates a queue
        QueueClient client = new QueueClient(ConnectionString, QueueName);
        await client.CreateIfNotExistsAsync();
        
        string jsonMessage = JsonSerializer.Serialize(message);
        BinaryData binaryMessage = new BinaryData(Encoding.UTF8.GetBytes(jsonMessage));
        await client.SendMessageAsync(binaryMessage);

        return new OkObjectResult(new { message = "Added to queue", data = requestBody });
    }


    [Function("PopMessage")]
    public async Task<IActionResult> RunPop([HttpTrigger(AuthorizationLevel.Function, "get", Route="pop")] HttpRequest req)
    {
        _logger.LogInformation("PopMessage function processed a request.");
        QueueClient client = new QueueClient(ConnectionString, QueueName);

        bool exists = await client.ExistsAsync();
        if (exists){
            var receivedMessage = await client.ReceiveMessageAsync();

            if (receivedMessage.Value != null)
            {
                var messageText = receivedMessage.Value.MessageText;
                await client.DeleteMessageAsync(receivedMessage.Value.MessageId, receivedMessage.Value.PopReceipt);
                var content = JsonSerializer.Deserialize<Message>(messageText)?.Content ?? "";

                return new OkObjectResult(new { message = "Popped from queue", data = content });
            }
            else
            {
                return new OkObjectResult(new { message = "Queue is empty" });
            }
        
        }
        else {
            return new OkObjectResult(new { message = "Queue does not exist" });
        }

    }
}