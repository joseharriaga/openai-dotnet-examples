using OpenAI.Realtime;
using System.Text.Json;

namespace OpenAI.Examples.Realtime;

#nullable disable
#pragma warning disable OPENAI002

public class FunctionTools
{
    private static string GetCurrentWeather(string location, string unit = "celsius")
    {
        // Call the weather API here.
        if (unit == "celsius")
        {
            return $"25 celsius";
        }
        else
        {
            return $"75 fahrenheit";
        }
    }

    public static readonly RealtimeFunctionTool GetCurrentWeatherTool = new(functionName: nameof(GetCurrentWeather))
    {
        FunctionDescription = "gets the weather for a location",
        FunctionParameters = BinaryData.FromString("""
        {
            "type": "object",
            "properties": {
                "location": {
                    "type": "string",
                    "description": "The city and state, e.g. Boston, MA"
                },
                "unit": {
                    "type": "string",
                    "enum": [ "celsius", "fahrenheit" ],
                    "description": "The temperature unit to use. Infer this from the specified location."
                }
            },
            "required": [ "location" ]
        }
        """)
    };

    public static async Task<IList<RealtimeFunctionCallOutputItem>> CallFunctionsAsync(RealtimeResponse response)
    {
        List<RealtimeFunctionCallOutputItem> functionCallOutputItems = [];

        List<RealtimeFunctionCallItem> functionCallItems = response.OutputItems
            .OfType<RealtimeFunctionCallItem>()
            .ToList();

        foreach (RealtimeFunctionCallItem functionCallItem in functionCallItems)
        {
            string functionOutput = string.Empty;

            Console.WriteLine($">> Calling {functionCallItem.FunctionName} function...");

            switch (functionCallItem.FunctionName)
            {
                case nameof(GetCurrentWeather):
                    {
                        // The arguments that the model wants to use to call the function are specified as a
                        // stringified JSON object based on the schema defined in the tool definition. Note that
                        // the model may hallucinate arguments too. Consequently, it is important to do the
                        // appropriate parsing and validation before calling the function.
                        using JsonDocument argumentsJson = JsonDocument.Parse(functionCallItem.FunctionArguments);
                        bool hasLocation = argumentsJson.RootElement.TryGetProperty("location", out JsonElement location);
                        bool hasUnit = argumentsJson.RootElement.TryGetProperty("unit", out JsonElement unit);

                        if (!hasLocation)
                        {
                            throw new ArgumentNullException(nameof(location), "The location argument is required.");
                        }

                        functionOutput = hasUnit
                            ? GetCurrentWeather(location.GetString(), unit.GetString())
                            : GetCurrentWeather(location.GetString());

                        break;
                    }
            }

           functionCallOutputItems.Add(
               RealtimeItem.CreateFunctionCallOutputItem(
                callId: functionCallItem.CallId,
                functionOutput: functionOutput));
        }

        return functionCallOutputItems;
    }
}

#pragma warning restore OPENAI002
