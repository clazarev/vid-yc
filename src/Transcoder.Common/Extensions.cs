// ReSharper disable once CheckNamespace
namespace Amazon.SQS.Model;

public static class Extensions
{
    public const string ChunkStateMessageAttributeName = "State";

    public static bool IsCancellationRequestMessage(this Message message)
    {
        return message.MessageAttributes.ContainsKey(ChunkStateMessageAttributeName) &&
               message.MessageAttributes[ChunkStateMessageAttributeName].StringValue == "Cancelled";
    }

    public static string? GetStateAttribute(this SendMessageRequest messageRequest)
    {
        return messageRequest.MessageAttributes.TryGetValue(ChunkStateMessageAttributeName, out var attribute) ? attribute.StringValue : null;
    }

    public static void SetIsCancellationRequestMessage(this SendMessageRequest messageRequest)
    {
        messageRequest.MessageAttributes.TryAdd(ChunkStateMessageAttributeName, new MessageAttributeValue
        {
            StringValue = "Cancelled",
            DataType = "String"
        });
    }

    public static void SetIsContinueRequestMessage(this SendMessageRequest messageRequest)
    {
        messageRequest.MessageAttributes.TryAdd(ChunkStateMessageAttributeName, new MessageAttributeValue
        {
            StringValue = "Continue",
            DataType = "String"
        });
    }

    public static int GetMaxApproximateReceiveCount(this Message message)
    {
        if (message.Attributes.TryGetValue(MessageSystemAttributeName.ApproximateReceiveCount, out var attribute)
           )
        {
            return int.TryParse(attribute, out var value) ? value : 0;
        }

        return 0;
    }
}
