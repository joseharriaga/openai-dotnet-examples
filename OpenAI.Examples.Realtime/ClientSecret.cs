using OpenAI.Realtime;

namespace OpenAI.Examples.Realtime;

#pragma warning disable OPENAI002

public class ClientSecret
{
    public static async Task<string> GetClientSecret(RealtimeSessionOptions sessionOptions)
    {
        RealtimeClient client = new(Environment.GetEnvironmentVariable("OPENAI_API_KEY"));

        CreateClientSecretOptions createClientSecretOptions = new()
        {
            SessionOptions = sessionOptions,
        };

        CreateClientSecretResult createClientSecretResult = await client.CreateRealtimeClientSecretAsync(createClientSecretOptions);

        return createClientSecretResult.Value;
    }
}

#pragma warning restore OPENAI002
