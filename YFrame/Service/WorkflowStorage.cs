using System.Collections.ObjectModel;
using System.IO;
using System.Text;
using System.Text.Json;
using YFrame.Model;

namespace YFrame.Service;

/// <summary>
/// 工作任务持久化：保存到 Config\Workflows\workflows.json。
/// </summary>
public static class WorkflowStorage
{
    /// <summary>工作流配置文件目录。</summary>
    public static string DirectoryPath => Path.Combine(AppContext.BaseDirectory, "Config", "Workflows");

    /// <summary>工作流配置文件路径。</summary>
    public static string FilePath => Path.Combine(DirectoryPath, "workflows.json");

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true
    };

    /// <summary>
    /// 读取工作流文档；文件不存在时生成示例流程。
    /// </summary>
    /// <returns>工作流文档</returns>
    public static WorkflowDocument Load()
    {
        try
        {
            if (File.Exists(FilePath))
            {
                string json = File.ReadAllText(FilePath, Encoding.UTF8);
                var document = JsonSerializer.Deserialize<WorkflowDocument>(json, JsonOptions);
                if (document != null && document.Flows.Count > 0)
                {
                    Normalize(document);
                    return document;
                }
            }
        }
        catch
        {
            // 配置损坏时回退到示例流程，避免工作任务页无法打开
        }

        var fallback = CreateSeedDocument();
        Normalize(fallback);
        return fallback;
    }

    /// <summary>
    /// 原子保存工作流文档。
    /// </summary>
    /// <param name="document">工作流文档</param>
    public static void Save(WorkflowDocument document)
    {
        Directory.CreateDirectory(DirectoryPath);
        string json = JsonSerializer.Serialize(document, JsonOptions);
        string temporaryPath = FilePath + ".tmp";
        File.WriteAllText(temporaryPath, json, new UTF8Encoding(false));
        File.Move(temporaryPath, FilePath, overwrite: true);
    }

    /// <summary>补齐旧数据和空节点，避免绑定空引用。</summary>
    private static void Normalize(WorkflowDocument document)
    {
        foreach (WorkflowDefinition flow in document.Flows)
        {
            if (string.IsNullOrWhiteSpace(flow.Id))
                flow.Id = Guid.NewGuid().ToString("N");
            if (string.IsNullOrWhiteSpace(flow.Name))
                flow.Name = "未命名流程";
            flow.Nodes ??= new ObservableCollection<WorkflowNode>();
            flow.Edges ??= new ObservableCollection<WorkflowEdge>();

            foreach (WorkflowNode node in flow.Nodes)
            {
                if (string.IsNullOrWhiteSpace(node.Id))
                    node.Id = "n" + flow.NextNodeId++;
                node.Text ??= string.Empty;
                node.Note ??= string.Empty;
            }

            foreach (WorkflowEdge edge in flow.Edges)
            {
                edge.From ??= string.Empty;
                edge.To ??= string.Empty;
                edge.FromDir = string.IsNullOrWhiteSpace(edge.FromDir) ? "right" : edge.FromDir;
                edge.ToDir = string.IsNullOrWhiteSpace(edge.ToDir) ? "left" : edge.ToDir;
            }
        }
    }

    /// <summary>创建与 FlowBoard 类似的初始示例流程。</summary>
    private static WorkflowDocument CreateSeedDocument()
    {
        var releaseNodes = new[]
        {
            "拉取发布分支", "更新版本号", "构建全量包", "生成增量包",
            "生成版本清单", "签名清单", "上传 COS", "发布完成"
        };
        var hotfixNodes = new[]
        {
            "拉取 hotfix 分支", "修复缺陷", "本地自测", "合入主干", "热更发布"
        };

        var release = CreateChainFlow("发布流程", releaseNodes);
        var hotfix = CreateChainFlow("紧急修复", hotfixNodes);
        return new WorkflowDocument
        {
            ActiveId = release.Id,
            Flows = new ObservableCollection<WorkflowDefinition> { release, hotfix }
        };
    }

    /// <summary>创建一条链式流程。</summary>
    private static WorkflowDefinition CreateChainFlow(string name, string[] titles)
    {
        var flow = new WorkflowDefinition { Name = name };
        for (int i = 0; i < titles.Length; i++)
        {
            flow.Nodes.Add(new WorkflowNode
            {
                Id = "n" + (i + 1),
                Text = titles[i],
                X = 40 + (i % 4) * 256,
                Y = 40 + (i / 4) * 134
            });
        }

        flow.NextNodeId = titles.Length + 1;
        for (int i = 0; i < flow.Nodes.Count - 1; i++)
        {
            flow.Edges.Add(new WorkflowEdge
            {
                From = flow.Nodes[i].Id,
                To = flow.Nodes[i + 1].Id,
                FromDir = "right",
                ToDir = "left"
            });
        }

        return flow;
    }
}

/// <summary>工作流文件根对象。</summary>
public sealed class WorkflowDocument
{
    /// <summary>全部流程。</summary>
    public ObservableCollection<WorkflowDefinition> Flows { get; set; } = new();

    /// <summary>上次激活的流程 ID。</summary>
    public string ActiveId { get; set; } = string.Empty;
}
