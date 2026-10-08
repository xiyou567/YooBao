using System;
using System.IO;
using UnityEngine;

/// <summary>
/// config.json 的形状：{ "apiKey": "sk-..." }
/// </summary>
[Serializable]
public class LLMConfigData
{
    public string apiKey;
}

/// <summary>
/// API Key 的唯一读取入口。
/// 优先级：环境变量 > 工程根目录的 config.json
/// 两条路都刻意放在版本库之外，避免 Key 被提交到 GitHub。
/// </summary>
public static class LLMConfig
{
    private const string EnvironmentVariableName = "DEEPSEEK_API_KEY";
    private const string ConfigFileName = "config.json";

    private static string _cachedApiKey;

    public static string ApiKey
    {
        get
        {
            if (string.IsNullOrEmpty(_cachedApiKey))
            {
                _cachedApiKey = Load();
            }

            return _cachedApiKey;
        }
    }

    private static string Load()
    {
        // 优先级 1：系统环境变量
        string fromEnvironment = Environment.GetEnvironmentVariable(EnvironmentVariableName);
        if (!string.IsNullOrEmpty(fromEnvironment))
        {
            return fromEnvironment;
        }

        // 优先级 2：工程根目录的 config.json
        // Application.dataPath 指向 <工程>/Assets，往上一级正好是工程根。
        // config.json 故意放在 Assets 外面：Unity 不会导入它，打包也不会带上。
        string path = Path.GetFullPath(Path.Combine(Application.dataPath, "..", ConfigFileName));

        if (!File.Exists(path))
        {
            Debug.LogError($"找不到 API Key：{path} 不存在，环境变量 {EnvironmentVariableName} 也没设置。");
            return null;
        }

        LLMConfigData data = JsonUtility.FromJson<LLMConfigData>(File.ReadAllText(path));
        if (data == null || string.IsNullOrEmpty(data.apiKey))
        {
            Debug.LogError($"{ConfigFileName} 里的 apiKey 是空的。");
            return null;
        }

        return data.apiKey;
    }
}
