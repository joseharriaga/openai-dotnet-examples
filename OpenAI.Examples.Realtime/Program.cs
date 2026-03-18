using OpenAI.Realtime;
using OpenAI.Examples.Realtime;

#nullable disable
#pragma warning disable SCME0001
#pragma warning disable OPENAI002

public class Program
{
    public static async Task Main()
    {
        List<RealtimeItem> conversationHistory =
        [
            RealtimeItem.CreateUserMessageItem("I want to know the weather in a few different locations."),
        ];

        RealtimeClient client = new(Environment.GetEnvironmentVariable("OPENAI_API_KEY"));

        RealtimeConversationSessionOptions sessionOptions = new()
        {
            Tools =
            {
                FunctionTools.GetCurrentWeatherTool,
            },
            AudioOptions = new()
            {
                InputAudioOptions = new()
                {
                    AudioFormat = new RealtimePcmAudioFormat(),
                    AudioTranscriptionOptions = new()
                    {
                        Model = "gpt-4o-transcribe",
                    },

                    // Using server VAD means that the server will determine when the user has finished speaking. This
                    // will automatically trigger a commit of the input audio buffer, as well as the generation of a
                    // response from the model.
                    TurnDetection = new RealtimeServerVadTurnDetection(),
                },
                OutputAudioOptions = new()
                {
                    AudioFormat = new RealtimePcmAudioFormat(),
                    Voice = RealtimeVoice.Alloy,
                },
            },
        };

        // Create a realtime conversation session using an ephemeral client secret.
        string clientSecret = await ClientSecret.GetClientSecret(sessionOptions);
        RealtimeSessionClientOptions sessionClientOptions = new() { ClientSecret = clientSecret };
        using RealtimeSessionClient sessionClient = await client.StartConversationSessionAsync("gpt-realtime", sessionClientOptions);

        // Used to manage audio output.
        int outputAudioLength = 0;
        using SpeakerOutput speakerOutput = new();

        // Used to manage app state.
        int done = 0;
        bool IsDone() => Volatile.Read(ref done) == 1;
        void MarkDone() => Interlocked.Exchange(ref done, 1);
        Task quitTask = null;
        Task audioTask = null;
        using CancellationTokenSource appCts = new();

        try
        {
            await foreach (RealtimeServerUpdate update in sessionClient.ReceiveUpdatesAsync(appCts.Token))
            {
                switch (update)
                {
                    case RealtimeServerUpdateSessionCreated sessionCreatedUpdate:
                        {
                            ConsoleHelper.WriteEvent(sessionCreatedUpdate);

                            // Add existing conversation history (if any) by creating items one by one.
                            // Note that adding a message will not automatically initiate a response from the model.
                            foreach (RealtimeItem item in conversationHistory)
                            {
                                RealtimeClientCommandConversationItemCreate conversationItemCreateCommand = new(item);
                                await sessionClient.SendCommandAsync(conversationItemCreateCommand);
                            }

                            // Start reading keys and quit when `Q` is received.
                            if (quitTask is null)
                            {
                                quitTask = Task.Run(() =>
                                {
                                    ConsoleHelper.WriteStatus($"Press Q to quit.");
                                    Console.WriteLine();

                                    while (!appCts.IsCancellationRequested)
                                    {
                                        ConsoleKeyInfo keyInfo;

                                        try
                                        {
                                            keyInfo = Console.ReadKey(intercept: true);
                                        }
                                        catch (InvalidOperationException)
                                        {
                                            break;
                                        }

                                        if (keyInfo.KeyChar is 'q' or 'Q')
                                        {
                                            ConsoleHelper.WriteAction($"Quitting...");
                                            Console.WriteLine();

                                            MarkDone();
                                            appCts.Cancel();

                                            break;
                                        }
                                    }
                                });
                            }

                            // Start capturing audio from the microphone and append it to the input audio buffer.
                            if (audioTask is null)
                            {
                                audioTask = Task.Run(async () =>
                                {
                                    ConsoleHelper.WriteStatus($"Interact with the model by speaking into the microphone.");
                                    Console.WriteLine();

                                    using MicrophoneAudioStream microphoneInput = MicrophoneAudioStream.Start();

                                    while (!appCts.IsCancellationRequested)
                                    {
                                        try
                                        {
                                            await sessionClient.SendInputAudioAsync(microphoneInput, appCts.Token);
                                        }
                                        catch (OperationCanceledException)
                                        {
                                            break;
                                        }
                                        catch (Exception ex)
                                        {
                                            ConsoleHelper.WriteInformation($"Input audio buffer error: {ex.Message}.");
                                            ConsoleHelper.WriteAction($"Restarting audio stream...");
                                            Console.WriteLine();
                                            await Task.Delay(500, appCts.Token).ConfigureAwait(false);
                                        }
                                    }
                                });
                            }

                            Console.WriteLine();
                            break;
                        }

                    case RealtimeServerUpdateConversationItemAdded converstionItemAddedUpdate:
                        {
                            ConsoleHelper.WriteEvent(converstionItemAddedUpdate);

                            Console.WriteLine();
                            break;
                        }
                    case RealtimeServerUpdateConversationItemCreated converstionItemCreatedUpdate:
                        {
                            ConsoleHelper.WriteEvent(converstionItemCreatedUpdate);

                            Console.WriteLine();
                            break;
                        }
                    case RealtimeServerUpdateConversationItemDeleted converstionItemDeletedUpdate:
                        {
                            ConsoleHelper.WriteEvent(converstionItemDeletedUpdate);

                            Console.WriteLine();
                            break;
                        }
                    case RealtimeServerUpdateConversationItemDone conversationItemDoneUpdate:
                        {
                            ConsoleHelper.WriteEvent(conversationItemDoneUpdate);
                            ConsoleHelper.WriteInformation($"Item type: {conversationItemDoneUpdate.Item.Patch.GetString("$.type"u8)}.");

                            switch (conversationItemDoneUpdate.Item)
                            {
                                case RealtimeMessageItem messageItem:
                                    {
                                        ConsoleHelper.WriteMessage(messageItem);
                                        break;
                                    }
                            }

                            Console.WriteLine();
                            break;
                        }
                    case RealtimeServerUpdateConversationItemRetrieved converstionItemRetrievedUpdate:
                        {
                            ConsoleHelper.WriteEvent(converstionItemRetrievedUpdate);

                            Console.WriteLine();
                            break;
                        }
                    case RealtimeServerUpdateConversationItemTruncated converstionItemTruncatedUpdate:
                        {
                            ConsoleHelper.WriteEvent(converstionItemTruncatedUpdate);

                            Console.WriteLine();
                            break;
                        }

                    case RealtimeServerUpdateConversationItemInputAudioTranscriptionCompleted conversationItemInputAudioTranscriptionCompletedUpdate:
                        {
                            ConsoleHelper.WriteEvent(conversationItemInputAudioTranscriptionCompletedUpdate);
                            ConsoleHelper.WriteMessage(RealtimeMessageRole.User, conversationItemInputAudioTranscriptionCompletedUpdate.Transcript);

                            Console.WriteLine();
                            break;
                        }
                    case RealtimeServerUpdateConversationItemInputAudioTranscriptionDelta conversationItemInputAudioTranscriptionDeltaUpdate:
                        {
                            ConsoleHelper.WriteEvent(conversationItemInputAudioTranscriptionDeltaUpdate);

                            Console.WriteLine();
                            break;
                        }
                    case RealtimeServerUpdateConversationItemInputAudioTranscriptionFailed conversationItemInputAudioTranscriptionFailedUpdate:
                        {
                            ConsoleHelper.WriteEvent(conversationItemInputAudioTranscriptionFailedUpdate);

                            Console.WriteLine();
                            break;
                        }
                    case RealtimeServerUpdateConversationItemInputAudioTranscriptionSegment conversationItemInputAudioTranscriptionSegmentUpdate:
                        {
                            ConsoleHelper.WriteEvent(conversationItemInputAudioTranscriptionSegmentUpdate);

                            Console.WriteLine();
                            break;
                        }

                    case RealtimeServerUpdateInputAudioBufferCleared inputAudioBufferClearedUpdate:
                        {
                            ConsoleHelper.WriteEvent(inputAudioBufferClearedUpdate);

                            Console.WriteLine();
                            break;
                        }
                    case RealtimeServerUpdateInputAudioBufferCommitted inputAudioBufferCommittedUpdate:
                        {
                            ConsoleHelper.WriteEvent(inputAudioBufferCommittedUpdate);

                            Console.WriteLine();
                            break;
                        }
                    case RealtimeServerUpdateInputAudioBufferTimeoutTriggered inputAudioBufferTimeoutTriggeredUpdate:
                        {
                            ConsoleHelper.WriteEvent(inputAudioBufferTimeoutTriggeredUpdate);

                            Console.WriteLine();
                            break;
                        }

                    case RealtimeServerUpdateInputAudioBufferSpeechStarted inputAudioBufferSpeechStartedUpdate:
                        {
                            ConsoleHelper.WriteEvent(inputAudioBufferSpeechStartedUpdate);
                            ConsoleHelper.WriteInformation($"Audio start time: {inputAudioBufferSpeechStartedUpdate.AudioStartTime}.");

                            // Use the cue that the user started speaking as a hint that the app should stop talking.
                            speakerOutput.ClearPlayback();

                            // TODO: Track the playback position and truncate the response item so that the model does
                            // not "remember things it didn't say".

                            Console.WriteLine();
                            break;
                        }
                    case RealtimeServerUpdateInputAudioBufferSpeechStopped inputAudioBufferSpeechStoppedUpdate:
                        {
                            ConsoleHelper.WriteEvent(inputAudioBufferSpeechStoppedUpdate);
                            ConsoleHelper.WriteInformation($"Audio end time: {inputAudioBufferSpeechStoppedUpdate.AudioEndTime}.");

                            Console.WriteLine();
                            break;
                        }

                    case RealtimeServerUpdateResponseCreated responseCreatedUpdate:
                        {
                            ConsoleHelper.WriteEvent(responseCreatedUpdate);
                            
                            Console.WriteLine(); 
                            break;
                        }
                    case RealtimeServerUpdateResponseDone responseDoneUpdate:
                        {
                            ConsoleHelper.WriteEvent(responseDoneUpdate);

                            IList<RealtimeFunctionCallOutputItem> functionCallOutputItems = await FunctionTools.CallFunctionsAsync(responseDoneUpdate.Response);

                            // The output of each tool call must be appended to the conversation as an item.
                            foreach (RealtimeFunctionCallOutputItem functionCallOutputItem in functionCallOutputItems)
                            {
                                ConsoleHelper.WriteAction($"Adding function call output item...");
                                RealtimeClientCommandConversationItemCreate conversationItemCreateCommand = new(functionCallOutputItem);
                                await sessionClient.SendCommandAsync(conversationItemCreateCommand);
                            }

                            // To follow up on the output of one or more tool calls, we must request another response.
                            if (functionCallOutputItems.Count > 0)
                            {
                                ConsoleHelper.WriteAction($"Requesting follow-up response...");
                                RealtimeClientCommandResponseCreate responseCreateCommand = new();
                                await sessionClient.SendCommandAsync(responseCreateCommand);
                            }

                            Console.WriteLine(); 
                            break;
                        }

                    case RealtimeServerUpdateResponseOutputAudioDelta responseOutputAudioDeltaUpdate:
                        {
                            ConsoleHelper.WriteEvent(responseOutputAudioDeltaUpdate);
                            ConsoleHelper.WriteInformation($"Bytes: {responseOutputAudioDeltaUpdate.Delta.Length}.");

                            speakerOutput.EnqueueForPlayback(responseOutputAudioDeltaUpdate.Delta.ToArray());
                            outputAudioLength += responseOutputAudioDeltaUpdate.Delta.Length;

                            Console.WriteLine(); 
                            break;
                        }
                    case RealtimeServerUpdateResponseOutputAudioDone responseOutputAudioDoneUpdate:
                        {
                            ConsoleHelper.WriteEvent(responseOutputAudioDoneUpdate);
                            ConsoleHelper.WriteInformation($"Bytes: {outputAudioLength}.");

                            Console.WriteLine(); 
                            break;
                        }
                    case RealtimeServerUpdateResponseOutputAudioTranscriptDone responseOutputAudioTranscriptionDoneUpdate:
                        {
                            ConsoleHelper.WriteEvent(responseOutputAudioTranscriptionDoneUpdate);
                            ConsoleHelper.WriteMessage(RealtimeMessageRole.Assistant, responseOutputAudioTranscriptionDoneUpdate.Transcript);
                            
                            Console.WriteLine();
                            break;
                        }

                    case RealtimeServerUpdateError errorUpdate:
                        {
                            ConsoleHelper.WriteEvent(errorUpdate);
                            ConsoleHelper.WriteInformation($"Error: {errorUpdate.Error.Message}");

                            MarkDone();

                            Console.WriteLine();
                            break;
                        }
                }

                if (IsDone())
                {
                    appCts.Cancel();
                    break;
                }
            }
        }
        catch (OperationCanceledException) when (appCts.IsCancellationRequested)
        {
        }
        finally
        {
            appCts.Cancel();

            if (audioTask is not null)
            {
                await audioTask;
            }

            if (quitTask is not null)
            {
                await quitTask;
            }
        }
    }
}

#pragma warning restore OPENAI002
#pragma warning restore SCME0001