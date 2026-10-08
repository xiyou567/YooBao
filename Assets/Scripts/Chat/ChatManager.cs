using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;

public class ChatManager
{
    private const string SystemPrompt = "你叫柚宝，是玩家的游戏伙伴。说话温柔活泼，每次回复不超过两句话。";

    private readonly LLMService _llmService;
    private readonly List<ChatMessage> _history = new List<ChatMessage>();

    public event Action<string> ReplyReceived;
    public event Action<string> ErrorOccurred;

    public IReadOnlyList<ChatMessage> History
    {
        get { return _history; }
    }

    public ChatManager(LLMService llmService)
    {
        _llmService = llmService;

        _history.Add(new ChatMessage { role = "system", content = SystemPrompt });
    }

    public async Task SendAsync(string userInput)
    {
        if (string.IsNullOrWhiteSpace(userInput))
        {
            return;
        }

        ChatMessage userMessage = new ChatMessage { role = "user", content = userInput };
        _history.Add(userMessage);

        try
        {
            string reply = await _llmService.SendAsync(_history.ToArray());

            _history.Add(new ChatMessage { role = "assistant", content = reply });
            ReplyReceived?.Invoke(reply);
        }
        catch (Exception e)
        {
            _history.Remove(userMessage);
            ErrorOccurred?.Invoke(e.Message);
        }
    }
}
