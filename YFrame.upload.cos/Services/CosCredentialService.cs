using System.IO;
using System.Text;
using YFrame.upload.cos.Models;

namespace YFrame.upload.cos.Services;

/// <summary>
/// 读取 COS 凭证 CSV。文件格式为：
/// SecretId,SecretKey
/// 值可以带双引号；密钥只保存在内存中，不写日志。
/// </summary>
public sealed class CosCredentialService
{
    /// <summary>从 CSV 文件读取凭证。</summary>
    /// <param name="filePath">SecretKey.csv 路径</param>
    /// <returns>COS 凭证</returns>
    public CosCredentials Load(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath))
            throw new InvalidOperationException("密钥文件路径为空。");
        if (!File.Exists(filePath))
            throw new FileNotFoundException("找不到密钥文件。", filePath);

        string[] lines;
        // Excel 等程序会独占打开 CSV，使用共享读写避免上传器直接读取失败
        using (var stream = new FileStream(
                   filePath,
                   FileMode.Open,
                   FileAccess.Read,
                   FileShare.ReadWrite | FileShare.Delete))
        using (var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true))
        {
            lines = reader.ReadToEnd()
                .Split(new[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries)
                .Where(line => !string.IsNullOrWhiteSpace(line))
                .ToArray();
        }

        if (lines.Length < 2)
            throw new InvalidDataException("密钥 CSV 至少需要表头和一行数据。");

        string[] headers = ParseCsvLine(lines[0]);
        int secretIdIndex = Array.FindIndex(headers,
            header => string.Equals(header.Trim(), "SecretId", StringComparison.OrdinalIgnoreCase));
        int secretKeyIndex = Array.FindIndex(headers,
            header => string.Equals(header.Trim(), "SecretKey", StringComparison.OrdinalIgnoreCase));

        // 兼容没有表头、直接两列数据的旧文件
        string[] values;
        if (secretIdIndex < 0 || secretKeyIndex < 0)
        {
            values = headers;
            secretIdIndex = 0;
            secretKeyIndex = 1;
        }
        else
        {
            values = ParseCsvLine(lines[1]);
        }

        if (values.Length <= Math.Max(secretIdIndex, secretKeyIndex))
            throw new InvalidDataException("密钥 CSV 字段数量不足。");

        string secretId = values[secretIdIndex].Trim();
        string secretKey = values[secretKeyIndex].Trim();
        if (string.IsNullOrWhiteSpace(secretId) || string.IsNullOrWhiteSpace(secretKey))
            throw new InvalidDataException("SecretId 或 SecretKey 为空。");

        return new CosCredentials
        {
            SecretId = secretId,
            SecretKey = secretKey
        };
    }

    /// <summary>解析一行 CSV，支持双引号和双引号转义。</summary>
    /// <param name="line">CSV 行</param>
    /// <returns>字段数组</returns>
    private static string[] ParseCsvLine(string line)
    {
        var fields = new List<string>();
        var current = new StringBuilder();
        bool inQuotes = false;

        for (int i = 0; i < line.Length; i++)
        {
            char ch = line[i];
            if (ch == '"')
            {
                if (inQuotes && i + 1 < line.Length && line[i + 1] == '"')
                {
                    current.Append('"');
                    i++;
                }
                else
                {
                    inQuotes = !inQuotes;
                }
            }
            else if (ch == ',' && !inQuotes)
            {
                fields.Add(current.ToString());
                current.Clear();
            }
            else
            {
                current.Append(ch);
            }
        }

        fields.Add(current.ToString());
        return fields.ToArray();
    }
}
