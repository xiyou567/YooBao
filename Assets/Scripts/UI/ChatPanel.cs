using TMPro;
using UnityEngine;
using UnityEngine.UI;

[ExecuteAlways]
public class ChatPanel : MonoBehaviour
{
    // ===== 配色 =====
    // 相机背景色是深蓝灰(0.19,0.30,0.47)，所以柚宝用浅底深字，跟用户气泡拉开对比
    private static readonly Color UserBubbleColor = new Color(0.18f, 0.48f, 0.92f);
    private static readonly Color UserTextColor = new Color(1f, 1f, 1f);
    private static readonly Color BotBubbleColor = new Color(0.95f, 0.96f, 0.98f);
    private static readonly Color BotTextColor = new Color(0.13f, 0.15f, 0.20f);
    private static readonly Color ErrorBubbleColor = new Color(0.72f, 0.24f, 0.24f);
    private static readonly Color ErrorTextColor = new Color(1f, 0.93f, 0.93f);

    // ===== 排版常量 =====
    private const float FontSize = 26f;
    private const float PaddingX = 22f;          // 气泡左右内边距
    private const float PaddingY = 14f;          // 气泡上下内边距
    private const float RowSpacing = 14f;        // 两条消息之间的间距
    private const int ContentPadding = 24;       // 消息列表四周留白
    private const float MaxBubbleWidthRatio = 0.75f;   // 气泡最宽占列表宽度的比例
    private const float FallbackContentWidth = 760f;   // 取不到宽度时的兜底值

    [SerializeField] private TMP_InputField inputField;
    [SerializeField] private Button sendButton;
    [SerializeField] private ScrollRect scrollRect;
    [SerializeField] private RectTransform content;

    private ChatManager _chatManager;
    private Sprite _bubbleSprite;

    /// <summary>
    /// 因为挂了 [ExecuteAlways]，这个方法在编辑器里也会执行，
    /// 所以 Scene 视图看到的排版和运行时是一致的。
    /// 这里只做「摆位置」，不碰任何联网络/读配置的逻辑。
    /// </summary>
    private void OnEnable()
    {
        // 编辑器里可能还没拖引用，先挡一下，不然会刷屏报错
        if (scrollRect == null || inputField == null || sendButton == null || content == null)
        {
            return;
        }

        SetupLayout();
        SetupContent();
    }

    private void Start()
    {
        // 编辑器里只摆布局，不启动聊天
        if (!Application.isPlaying)
        {
            return;
        }

        _bubbleSprite = CreateRoundedSprite();

        _chatManager = new ChatManager(new LLMService(LLMConfig.ApiKey));
        _chatManager.ReplyReceived += OnReplyReceived;
        _chatManager.ErrorOccurred += OnErrorOccurred;

        sendButton.onClick.AddListener(OnSendClicked);
        inputField.onSubmit.AddListener(OnSubmitted);
        inputField.ActivateInputField();
    }

    /// <summary>
    /// 整个界面的排布在代码里统一设定：上面聊天记录区，下面一条输入栏。
    /// 场景里这几个控件的锚点和尺寸是乱的（按钮高度算出来是负数、Viewport 是 0×0），
    /// 所以这里全部重设，不依赖场景里的值。
    /// </summary>
    private void SetupLayout()
    {
        const float margin = 32f;         // 屏幕四周留白
        const float inputHeight = 84f;    // 输入栏高度
        const float buttonWidth = 150f;   // 发送按钮宽度
        const float gap = 16f;            // 输入框与按钮的间距
        const float scrollbarWidth = 10f; // 右侧滚动条宽度

        // ⓪ ChatPanel 自己铺满整个 Canvas。场景里它被拖成了底部 500 高的一条，
        //    而输入栏是锚在它身上的，不重设的话定位会跟着它漂
        RectTransform self = (RectTransform)transform;
        self.anchorMin = Vector2.zero;
        self.anchorMax = Vector2.one;
        self.pivot = new Vector2(0.5f, 0.5f);
        self.offsetMin = Vector2.zero;
        self.offsetMax = Vector2.zero;

        // ① 聊天记录区：占满上方，底部给输入栏让位
        RectTransform scrollView = (RectTransform)scrollRect.transform;
        scrollView.anchorMin = Vector2.zero;
        scrollView.anchorMax = Vector2.one;
        scrollView.pivot = new Vector2(0.5f, 0.5f);
        scrollView.offsetMin = new Vector2(margin, margin + inputHeight + gap);
        scrollView.offsetMax = new Vector2(-margin, -margin);

        // ② Viewport：撑满 Scroll View，右侧留出滚动条的位置（原来是 0×0，所以什么都看不见）
        RectTransform viewport = scrollRect.viewport;
        if (viewport != null)
        {
            viewport.anchorMin = Vector2.zero;
            viewport.anchorMax = Vector2.one;
            viewport.pivot = new Vector2(0f, 1f);
            viewport.offsetMin = Vector2.zero;
            viewport.offsetMax = new Vector2(-scrollbarWidth, 0f);
        }

        // ③ 垂直滚动条：贴右边通高（原来高度是 0）
        if (scrollRect.verticalScrollbar != null)
        {
            RectTransform bar = (RectTransform)scrollRect.verticalScrollbar.transform;
            bar.anchorMin = new Vector2(1f, 0f);
            bar.anchorMax = new Vector2(1f, 1f);
            bar.pivot = new Vector2(1f, 0.5f);
            bar.offsetMin = new Vector2(-scrollbarWidth, 0f);
            bar.offsetMax = Vector2.zero;
        }

        // ④ 聊天不需要横向滚动
        scrollRect.horizontal = false;
        if (scrollRect.horizontalScrollbar != null)
        {
            scrollRect.horizontalScrollbar.gameObject.SetActive(false);
        }

        // ⑤ 输入框：贴左下
        RectTransform inputRect = (RectTransform)inputField.transform;
        inputRect.anchorMin = new Vector2(0f, 0f);
        inputRect.anchorMax = new Vector2(1f, 0f);
        inputRect.pivot = new Vector2(0.5f, 0f);
        inputRect.offsetMin = new Vector2(margin, margin);
        inputRect.offsetMax = new Vector2(-(margin + buttonWidth + gap), margin + inputHeight);

        // ⑥ 发送按钮：贴右下（原来高度是负数，所以按钮一直是不显示的）
        RectTransform buttonRect = (RectTransform)sendButton.transform;
        buttonRect.anchorMin = new Vector2(1f, 0f);
        buttonRect.anchorMax = new Vector2(1f, 0f);
        buttonRect.pivot = new Vector2(1f, 0f);
        buttonRect.anchoredPosition = new Vector2(-margin, margin);
        buttonRect.sizeDelta = new Vector2(buttonWidth, inputHeight);
    }

    /// <summary>
    /// 消息列表的排版规则全部在代码里设定，不改场景文件。
    /// </summary>
    private void SetupContent()
    {
        // 顶部对齐、宽度跟随 Viewport，高度由 ContentSizeFitter 撑开
        content.anchorMin = new Vector2(0f, 1f);
        content.anchorMax = new Vector2(1f, 1f);
        content.pivot = new Vector2(0.5f, 1f);
        content.anchoredPosition = Vector2.zero;
        content.sizeDelta = Vector2.zero;

        VerticalLayoutGroup layout = content.GetComponent<VerticalLayoutGroup>();
        if (layout == null)
        {
            layout = content.gameObject.AddComponent<VerticalLayoutGroup>();
        }
        layout.padding = new RectOffset(ContentPadding, ContentPadding, ContentPadding, ContentPadding);
        layout.spacing = RowSpacing;
        layout.childAlignment = TextAnchor.UpperLeft;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = true;    // 行容器撑满整行，气泡才能在行内靠左/靠右
        layout.childForceExpandHeight = false;

        ContentSizeFitter fitter = content.GetComponent<ContentSizeFitter>();
        if (fitter == null)
        {
            fitter = content.gameObject.AddComponent<ContentSizeFitter>();
        }
        fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;  // 宽度交给 Viewport
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;    // 高度随消息条数增长
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
        AddBubble(message, true, UserBubbleColor, UserTextColor);

        SetInteractable(false);
        await _chatManager.SendAsync(message);
        SetInteractable(true);

        inputField.ActivateInputField();
    }

    private void OnReplyReceived(string reply)
    {
        AddBubble(reply, false, BotBubbleColor, BotTextColor);
    }

    private void OnErrorOccurred(string error)
    {
        AddBubble("（请求失败）" + error, false, ErrorBubbleColor, ErrorTextColor);
    }

    private void SetInteractable(bool value)
    {
        sendButton.interactable = value;
        inputField.interactable = value;
    }

    /// <summary>
    /// 一条消息 = 行容器(HorizontalLayoutGroup，负责左右对齐) + 气泡(Image 圆角背景 + TMP 文字)。
    /// </summary>
    private void AddBubble(string message, bool isUser, Color bubbleColor, Color textColor)
    {
        // ① 行容器：撑满整行，把气泡推到左边或右边
        GameObject row = new GameObject("Row", typeof(RectTransform));
        row.transform.SetParent(content, false);

        HorizontalLayoutGroup rowLayout = row.AddComponent<HorizontalLayoutGroup>();
        rowLayout.childAlignment = isUser ? TextAnchor.MiddleRight : TextAnchor.MiddleLeft;
        rowLayout.childControlWidth = true;
        rowLayout.childControlHeight = true;
        rowLayout.childForceExpandWidth = false;
        rowLayout.childForceExpandHeight = false;

        // ② 气泡本体：圆角 Image 当背景
        GameObject bubble = new GameObject("Bubble", typeof(RectTransform));
        bubble.transform.SetParent(row.transform, false);

        Image background = bubble.AddComponent<Image>();
        if (_bubbleSprite != null)
        {
            background.sprite = _bubbleSprite;
            background.type = Image.Type.Sliced;
        }
        background.color = bubbleColor;
        background.raycastTarget = false;

        // ③ 文字单独一个物体：Image 和 TMP 都是 Graphic，不能挂在同一个物体上
        GameObject textObject = new GameObject("Text", typeof(RectTransform));
        textObject.transform.SetParent(bubble.transform, false);

        // 文字铺满气泡，四边 offset 就是气泡的内边距
        RectTransform textRect = (RectTransform)textObject.transform;
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = new Vector2(PaddingX, PaddingY);
        textRect.offsetMax = new Vector2(-PaddingX, -PaddingY);

        TextMeshProUGUI text = textObject.AddComponent<TextMeshProUGUI>();
        text.text = message;
        text.color = textColor;
        text.fontSize = FontSize;
        text.alignment = TextAlignmentOptions.TopLeft;
        text.enableWordWrapping = true;
        text.raycastTarget = false;

        // ④ 量出气泡尺寸：长文本先按上限换行，再算出换行后的真实高度
        float contentWidth = content.rect.width;
        if (contentWidth < 100f)
        {
            contentWidth = FallbackContentWidth;
        }

        float usableWidth = Mathf.Max(160f, contentWidth - ContentPadding * 2f);
        float maxTextWidth = Mathf.Max(80f, usableWidth * MaxBubbleWidthRatio - PaddingX * 2f);

        Vector2 preferred = text.GetPreferredValues(message, maxTextWidth, 0f);
        float textWidth = Mathf.Min(preferred.x, maxTextWidth);
        float textHeight = text.GetPreferredValues(message, textWidth, 0f).y;

        LayoutElement element = bubble.AddComponent<LayoutElement>();
        element.preferredWidth = textWidth + PaddingX * 2f;
        element.preferredHeight = textHeight + PaddingY * 2f;

        ScrollToBottom();
    }

    private void ScrollToBottom()
    {
        Canvas.ForceUpdateCanvases();
        scrollRect.verticalNormalizedPosition = 0f;
    }

    /// <summary>
    /// 现画一个圆角矩形当气泡底图。
    /// 九宫格（Sliced）拉伸，所以气泡拉多宽多高，圆角都不会被拉变形。
    /// 用画的而不是加载内置资源，是因为团结引擎里没有 Unity 那个 UI/Skin/UISprite.psd。
    /// </summary>
    private static Sprite CreateRoundedSprite()
    {
        const int radius = 12;   // 圆角半径（像素）
        const int middle = 10;   // 中间可拉伸区域的厚度
        int size = (radius + middle) * 2;

        Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
        texture.wrapMode = TextureWrapMode.Clamp;
        texture.filterMode = FilterMode.Bilinear;

        Color32[] pixels = new Color32[size * size];

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                // 只算到「最近那个圆角圆心」的横向/纵向距离，中间区域两者都是 0
                float dx = 0f;
                float dy = 0f;

                if (x < radius)
                {
                    dx = radius - x - 0.5f;
                }
                else if (x >= size - radius)
                {
                    dx = x - (size - radius) + 0.5f;
                }

                if (y < radius)
                {
                    dy = radius - y - 0.5f;
                }
                else if (y >= size - radius)
                {
                    dy = y - (size - radius) + 0.5f;
                }

                // 按距离做一点渐变，圆角边缘才不会有锯齿
                float alpha = Mathf.Clamp01(radius - Mathf.Sqrt(dx * dx + dy * dy) + 0.5f);

                pixels[y * size + x] = new Color32(255, 255, 255, (byte)(alpha * 255f));
            }
        }

        texture.SetPixels32(pixels);
        texture.Apply();

        // border 的顺序是 左/下/右/上，四边都取 radius，中间那块就是可拉伸区域
        return Sprite.Create(
            texture,
            new Rect(0f, 0f, size, size),
            new Vector2(0.5f, 0.5f),
            100f,
            0,
            SpriteMeshType.FullRect,
            new Vector4(radius, radius, radius, radius));
    }
}
