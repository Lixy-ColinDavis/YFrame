namespace YFrame.Common;

/// <summary>
/// 腾讯对象存储（COS）
/// 更新地址的唯一默认来源。
/// 这里只保留一个 COS 根地址，Config、Pack、update 都从这里派生环境路径。
/// 项目为开源项目，COS 成本很低，无安全与经济风险，因此地址明文保存。
/// </summary>
public static class Cos_UpdateDefaults
{
    /// <summary>唯一的默认 COS 更新根地址。</summary>
    public const string BaseUrl =
        "https://yfcos-1256976024.cos.ap-beijing.myqcloud.com/YFrame/update";

    /// <summary>缺省环境。</summary>
    public const string DefaultEnv = "uat";

    /// <summary>归一化环境名：只允许 uat/pdt，其他值回退 uat。</summary>
    public static string NormalizeEnv(string? env)
        => string.Equals(env?.Trim(), "pdt", StringComparison.OrdinalIgnoreCase)
            ? "pdt"
            : DefaultEnv;

    /// <summary>版本清单地址。</summary>
    public static string ManifestUrl(string env)
        => $"{BaseUrl.TrimEnd('/')}/{NormalizeEnv(env)}/YF_Version.json";

    /// <summary>完整包地址。</summary>
    public static string CurrentUrl(string env)
        => $"{BaseUrl.TrimEnd('/')}/{NormalizeEnv(env)}/current.zip";

    /// <summary>增量包地址。</summary>
    public static string IncrementUrl(string env)
        => $"{BaseUrl.TrimEnd('/')}/{NormalizeEnv(env)}/increment.zip";
}
