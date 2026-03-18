using OpenAI.Realtime;

namespace OpenAI.Examples.Realtime;

#pragma warning disable SCME0001
#pragma warning disable OPENAI002

public class ConsoleHelper
{
    public static void WriteAction(string action)
    {
        Console.WriteLine($">> {action}");
    }

    public static void WriteEvent(RealtimeServerUpdate update)
    {
        string? type = update.Patch.GetString("$.type"u8);
        Console.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] {type}");
    }

    public static void WriteInformation(string information)
    {
        Console.WriteLine($"!! {information}");
    }

    public static void WriteMessage(RealtimeMessageItem messageItem)
    {
        foreach (RealtimeMessageContentPart contentPart in messageItem.Content)
        {
            switch (contentPart)
            {
                case RealtimeInputTextMessageContentPart inputTextPart:
                    {
                        Console.WriteLine();
                        Console.WriteLine($"++ [{messageItem.Role.ToString().ToUpperInvariant()}]:");
                        Console.WriteLine($"{inputTextPart.Text}");
                        break;
                    }
                case RealtimeOutputTextMessageContentPart outputTextPart:
                    {
                        Console.WriteLine();
                        Console.WriteLine($"++ [{messageItem.Role.ToString().ToUpperInvariant()}]:");
                        Console.WriteLine($"{outputTextPart.Text}");
                        break;
                    }
            }
        }
    }

    public static void WriteMessage(RealtimeMessageRole role, string text)
    {
        Console.WriteLine();
        Console.WriteLine($"++ [{role.ToString().ToUpperInvariant()}]:");
        Console.WriteLine($"{text}");
    }

    public static void WriteStatus(string status)
    {
        Console.WriteLine($"## {status}");
    }
}

#pragma warning restore OPENAI002
#pragma warning restore SCME0001