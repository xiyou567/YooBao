using System;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

public class LLMService
{
    private const string ApiUrl = "https://api.deepseek.com/chat/completions";//往哪发
    
    private const string ModelName = "deepseek-v4-flash";//使用什么模型名字
    
    private static readonly HttpClient Http = new HttpClient //谁去发
    {
        Timeout = TimeSpan.FromSeconds(30)//发多久算超时
    };

    private readonly string _apiKey;       //readonly 只能在声明处或构造函数里赋值，之后不能再改指向//声明字段_apiKey

    public LLMService(string apiKey)//构造函数 ， 参数
    {
        _apiKey = apiKey;//给字段传参
    }
    
    public async Task<string> SendAsync(ChatMessage[] messages, CancellationToken cancellationToken = default)//异步方法，最终返回一个字符串
    {
        //对APIkey进行空判
        if (string.IsNullOrEmpty(_apiKey))
        {
            throw new InvalidOperationException("API Key 为空，请检查工程根目录的 config.json。");
        }
        //组装请求体
        ChatRequest payload = new ChatRequest
        {
            model = ModelName,
            messages = messages
        };
        //创建Http请求
        using (HttpRequestMessage httpRequest = new HttpRequestMessage(HttpMethod.Post, ApiUrl))
        {
            //加请求头和请求体
            httpRequest.Headers.Add("Authorization", "Bearer " + _apiKey);
            httpRequest.Content = new StringContent(
                JsonUtility.ToJson(payload),
                Encoding.UTF8,
                "application/json");

            // await 期间不阻塞主线程。
            // 结束后 Unity 的同步上下文会自动把接下来的代码切回主线程，
            // 所以这行之后可以直接操作 UI，不需要自己写 Dispatcher。
            HttpResponseMessage response = await Http.SendAsync(httpRequest, cancellationToken);
            string body = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                throw new HttpRequestException($"HTTP {(int)response.StatusCode} - {body}");
            }

            ChatResponse parsed = JsonUtility.FromJson<ChatResponse>(body);
            if (parsed == null || parsed.choices == null || parsed.choices.Length == 0)
            {
                throw new Exception("响应解析失败，原始内容：" + body);
            }

            return parsed.choices[0].message.content;
        }
    }
}
