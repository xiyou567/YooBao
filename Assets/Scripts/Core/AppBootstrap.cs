using UnityEngine;
using YooBao.SDK;

/// <summary>
/// YooBao 的启动器。挂在场景里一个空物体上，负责开服务、关服务。
/// 单独一个类是为了让 ChatPanel 只管界面，不碰服务端的生死。
/// </summary>
public class AppBootstrap : MonoBehaviour
{
    private LocalServer _server;

    private void Start()
    {
        _server = new LocalServer();

        if (_server.Start())
        {
            Debug.Log("YooBao 本地服务已启动：http://127.0.0.1:" + _server.Port + Protocol.RouteHealth);
        }
        else
        {
            Debug.LogError("YooBao 本地服务启动失败（端口 " + _server.Port + " 可能被占用，或者 LocalServer.Start 的 TODO 还没填）。");
        }
    }

    private void OnDestroy()
    {
        if (_server != null)
        {
            _server.Stop();
            _server = null;
        }
    }
}
