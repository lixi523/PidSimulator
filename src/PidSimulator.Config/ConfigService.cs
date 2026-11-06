using System.Text.Json;

namespace PidSimulator.Config;

/// <summary>配置加载/保存（JSON）。</summary>
public sealed class ConfigService
{
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true
    };

    /// <summary>加载配置；文件不存在或解析失败时返回默认配置。</summary>
    public AppConfig Load(string path)
    {
        try
        {
            if (!File.Exists(path))
                return new AppConfig();

            string json = File.ReadAllText(path);
            return JsonSerializer.Deserialize<AppConfig>(json, Options) ?? new AppConfig();
        }
        catch
        {
            return new AppConfig();
        }
    }

    /// <summary>保存配置，自动创建目录。</summary>
    public void Save(AppConfig config, string path)
    {
        string? dir = Path.GetDirectoryName(Path.GetFullPath(path));
        if (!string.IsNullOrEmpty(dir))
            Directory.CreateDirectory(dir);

        string json = JsonSerializer.Serialize(config, Options);
        File.WriteAllText(path, json);
    }
}
