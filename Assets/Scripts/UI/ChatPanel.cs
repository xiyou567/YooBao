using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class ChatPanel : MonoBehaviour
{
    private static readonly Color UserColor = new Color(0.16f, 0.44f, 0.78f);
    private static readonly Color BotColor = new Color(0.16f, 0.16f, 0.18f);
    private static readonly Color ErrorColor = new Color(0.80f, 0.22f, 0.22f);

    [SerializeField] private TMP_InputField inputField;
    [SerializeField] private Button sendButton;
    [SerializeField] private ScrollRect scrollRect;
    [SerializeField] private RectTransform content;

    private ChatManager _chatManager;

    private void Start()
    {
        _chatManager = new ChatManager(new LLMService(LLMConfig.ApiKey));
        _chatManager.ReplyReceived += OnReplyReceived;
        _chatManager.ErrorOccurred += OnErrorOccurred;

        sendButton.onClick.AddListener(OnSendClicked);
        inputField.onSubmit.AddListener(OnSubmitted);
        inputField.ActivateInputField();
    }

    private void OnSubmitted(string _)
    {
        OnSendClicked();
    }

    private async void OnSendClicked()
    {
        string message = inputField.text;
        if (string.IsNullOrWhiteSpace(message))
        {
            return;
        }

        inputField.text = string.Empty;
        AddBubble(message, UserColor, TextAlignmentOptions.Right);

        SetInteractable(false);
        await _chatManager.SendAsync(message);
        SetInteractable(true);

        inputField.ActivateInputField();
    }

    private void OnReplyReceived(string reply)
    {
        AddBubble(reply, BotColor, TextAlignmentOptions.Left);
    }

    private void OnErrorOccurred(string error)
    {
        AddBubble("（请求失败）" + error, ErrorColor, TextAlignmentOptions.Left);
    }

    private void SetInteractable(bool value)
    {
        sendButton.interactable = value;
        inputField.interactable = value;
    }

    private void AddBubble(string message, Color color, TextAlignmentOptions alignment)
    {
        GameObject bubble = new GameObject("Bubble", typeof(RectTransform));
        bubble.transform.SetParent(content, false);

        TextMeshProUGUI text = bubble.AddComponent<TextMeshProUGUI>();
        text.text = message;
        text.color = color;
        text.alignment = alignment;
        text.fontSize = 26;
        text.margin = new Vector4(16f, 10f, 16f, 10f);

        ScrollToBottom();
    }

    private void ScrollToBottom()
    {
        Canvas.ForceUpdateCanvases();
        scrollRect.verticalNormalizedPosition = 0f;
    }
}
