using HiveMQtt.Client;
using HiveMQtt.Client.Events;
using HiveMQtt.MQTT5.ReasonCodes;
using HiveMQtt.MQTT5.Types;
using System.Text;

namespace SimpleMqtt;

public class SimpleMqttClient : IDisposable
{
    private static Encoding DefaultEncoding = Encoding.ASCII;
    private readonly HiveMQClient _client;
    public SimpleMqttClient(SimpleMqttClientConfiguration options)
    {
        this.ClientId = options.ClientId;

        _client = new HiveMQClient(new()
        {
            ClientId = options.ClientId,
            CleanStart = options.CleanStart,
            Port = options.Port,
            Host = options.Host!,
            ConnectTimeoutInMs = options.TimeoutInMs,
            UserName = options.UserName,
            Password = options.Password
        });

        _client.OnMessageReceived += OnHiveMQttMessageReceived;
    }

    public event EventHandler<SimpleMqttMessage>? OnMessageReceived;

    public string? ClientId { get; private set; }

    public async Task PublishMessage(SimpleMqttMessage message)
    {
        await this.OpenAndVerifyConnection();

        var mqttMessage = new MQTT5PublishMessage
        {
            Topic = message.Topic,
            Payload = DefaultEncoding.GetBytes(message.Message!),
            QoS = QualityOfService.ExactlyOnceDelivery,
        };

        var publishResult = await _client.PublishAsync(mqttMessage).ConfigureAwait(false);

        if (publishResult.QoS2ReasonCode != PubRecReasonCode.Success)
        {
            throw new InvalidOperationException($"Unable to publish message [reason code: {publishResult.QoS2ReasonCode.GetValueOrDefault(PubRecReasonCode.UnspecifiedError)}");
        }
    }

    public Task PublishMessage(string message, string topic) => PublishMessage(new() { Topic = topic, Message = message });

    public async Task SubscribeToTopic(string topic)
    {
        await this.OpenAndVerifyConnection();
        await _client.SubscribeAsync(topic, QualityOfService.ExactlyOnceDelivery).ConfigureAwait(false);
    }

    private void OnHiveMQttMessageReceived(object? sender, OnMessageReceivedEventArgs e)
    {
        var msg = new SimpleMqttMessage
        {
            Topic = e.PublishMessage.Topic!,
            Message = DefaultEncoding.GetString(e.PublishMessage.Payload!)
        };

        this.OnMessageReceived?.Invoke(this, msg);
    }

    private async Task OpenAndVerifyConnection()
    {
        if (!this._client.IsConnected())
        {
            var connectionResult = await _client.ConnectAsync().ConfigureAwait(false);

            if (connectionResult.ReasonCode != ConnAckReasonCode.Success)
            {
                throw new InvalidOperationException($"Failed to connect: {connectionResult.ReasonString}");
            }
        }
    }

    public void Dispose() => _client.Dispose();

    ~SimpleMqttClient() => _client.Dispose();

    public static SimpleMqttClient CreateSimpleMqttClientForHiveMQ(string clientId, IConfiguration configuration)
    {
        // Retrieve MQTT connection settings from configuration
        var mqttConfig = configuration.GetSection("MqttConnection").Get<MqttConfig>();

        var mqttWrapper = new SimpleMqttClient(new()
        {
            Host = mqttConfig!.Host,
            Port = mqttConfig.Port,
            CleanStart = false,
            ClientId = clientId,
            TimeoutInMs = mqttConfig.TimeoutInMs,
            UserName = mqttConfig.UserName,
            Password = mqttConfig.Password
        });

        return mqttWrapper;
    }
}

public class SimpleMqttClientConfiguration
{

    public string? Host { get; set; }
    public int Port { get; set; }
    public string? ClientId { get; set; }
    public int TimeoutInMs { get; set; }
    public bool CleanStart { get; set; }
    public string? UserName { get; set; }
    public string? Password { get; set; }
}
public class SimpleMqttMessage
{
    public string? Topic { get; set; }
    public string? Message { get; set; }
}
