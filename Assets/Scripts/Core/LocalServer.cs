using System;
using System.Net;
using System.Text;
using System.Threading;
using YooBao.SDK;

/// <summary>
/// YooBao 的本地 HTTP 服务端。Plugin 通过它把游戏里的观察送进来。
/// 纯 C# 类，不继承 MonoBehaviour —— 收网络跟 Unity 的生命周期无关。
/// </summary>
public class LocalServer
{
    private HttpListener _listener;   // 那扇门本身
    private Thread _thread;           // 站在门口的人（后台线程）
    private volatile bool _running;   // 门是不是还开着；volatile 见 TODO 1 之后的说明
    private int _port;
    private long _startedAtMs;

    public int Port
    {
        get { return _port; }
    }

    public bool IsRunning
    {
        get { return _running; }
    }

    public bool Start()
    {
        _port = Protocol.DefaultPort;
        _startedAtMs = Protocol.NowMs();
        _running = true;

        // TODO 1 —— 开这扇门（大约 5 行)
        //   a. new 一个 HttpListener，赋给 _listener
        _listener = new HttpListener();
        //   b. 往前缀表里加 "http://127.0.0.1:{端口}/"
        _listener.Prefixes.Add("http://127.0.0.1:" + _port + "/");
        //      （用 _port 拼字符串。结尾那个斜杠不能省，用 127.0.0.1 不要用 localhost）
        //   c. 让 _listener 开始监听
        _listener.Start();
        //   d. 开一个后台线程跑 ListenLoop()，记得把它标成后台线程
        //
        //   注意：d 必须在 a~c 之后。线程一起来就会立刻读 _running（上面已经置 true），
        //   顺序反了会开出一个转身就退出的空线程。
        _thread = new Thread(ListenLoop);
        _thread.IsBackground = true;
        _thread.Start();

        return _listener != null && _running;
    }

    public void Stop()
    {
        _running = false;

        if (_listener != null)
        {
            _listener.Stop();
            _listener.Close();
            _listener = null;
        }

        _thread = null;
    }

    private void ListenLoop()
    {
        while (_running)
        {
            HttpListenerContext context;

            try
            {
                context = _listener.GetContext();   // 没请求时卡在这里等
            }
            catch (Exception)
            {
                break;   // Stop() 会让上面这行抛异常，这是正常的收摊信号
            }

            Handle(context);
        }
    }

    private void Handle(HttpListenerContext context)
    {
        string path = context.Request.Url.AbsolutePath;

        if (path == Protocol.RouteHealth)
        {
            WriteJson(context, 200, new HealthResponse
            {
                app             = Protocol.AppName,
                protocolVersion = Protocol.Version,
                uptimeMs        = Protocol.NowMs() - _startedAtMs,
                plugins         = 0
            });
        }
        else
        {
            WriteJson(context, 404, new ErrorEnvelope(ErrorCodes.NotFound, "没有这个接口：" + path));
        }
    }

    private void WriteJson(HttpListenerContext context, int statusCode, object payload)
    {
        string json = YooBaoJson.Serialize(payload);
        byte[] bytes = Encoding.UTF8.GetBytes(json);

        context.Response.ContentType = "application/json; charset=utf-8";
        context.Response.ContentLength64 = bytes.Length;
        context.Response.StatusCode = statusCode;

        context.Response.OutputStream.Write(bytes, 0, bytes.Length);
        context.Response.Close();
    }
}
