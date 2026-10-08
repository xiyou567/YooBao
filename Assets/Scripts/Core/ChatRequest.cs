using System;


[Serializable]
public class ChatRequest
{
    public string model;

    public ChatMessage[] messages;
}
