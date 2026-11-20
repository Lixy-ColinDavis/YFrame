namespace YFrame.Common;

/// <summary>
/// 更新清单签名信任锚。
/// 只保存公钥，不包含任何私钥；UAT 与 PDT 使用独立密钥。
/// 私钥保存在仓库外的安全位置，不能提交到 Git 或上传到 COS。
/// </summary>
public static class UpdateTrustKeys
{
    /// <summary>UAT 清单签名密钥 ID。</summary>
    public const string UatKeyId = "uat-2026-01";

    /// <summary>PDT 清单签名密钥 ID。</summary>
    public const string PdtKeyId = "pdt-2026-01";

    /// <summary>
    /// UAT 公钥（SubjectPublicKeyInfo Base64）。
    /// 指纹：b652e514d9e776bcd72a893e1f3da7d1fba0e6827c101aefef0e058bdd8a3a60
    /// </summary>
    public const string UatPublicKey =
        "MFkwEwYHKoZIzj0CAQYIKoZIzj0DAQcDQgAEZQjG+wyuHNtuTAMNChpjWWDBw+jsJBpwgDSERV0FhyEeHx7DB/iUGhk9ezSU3a+rBplNpYmdFBqoP++MBDax0A==";

    /// <summary>
    /// PDT 公钥（SubjectPublicKeyInfo Base64）。
    /// 指纹：93dc7f540626905efe83a0ca56845f6ef91eae386a924aa44a3ebf9fbed2a1ce
    /// </summary>
    public const string PdtPublicKey =
        "MFkwEwYHKoZIzj0CAQYIKoZIzj0DAQcDQgAEi+prpyJWZpT3l469/LC9+YKamB+2bJEHkhjJigKnk2zO9VTEJ9Pt27RdPcBDjkK4NhVPRI/y+tyPsIVzk5Mihw==";

    /// <summary>
    /// 按环境返回公钥 Base64。
    /// </summary>
    /// <param name="environment">uat 或 pdt</param>
    /// <returns>对应环境的 SubjectPublicKeyInfo Base64</returns>
    public static string GetPublicKey(string? environment)
        => Cos_UpdateDefaults.NormalizeEnv(environment) == "pdt"
            ? PdtPublicKey
            : UatPublicKey;

    /// <summary>
    /// 按环境返回密钥 ID，便于后续在清单中做 key_id 选择。
    /// </summary>
    /// <param name="environment">uat 或 pdt</param>
    /// <returns>对应环境的密钥 ID</returns>
    public static string GetKeyId(string? environment)
        => Cos_UpdateDefaults.NormalizeEnv(environment) == "pdt"
            ? PdtKeyId
            : UatKeyId;
}
