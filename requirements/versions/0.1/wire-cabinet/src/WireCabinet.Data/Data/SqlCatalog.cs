using System.IO;
using WireCabinet.Core;
using WireCabinet.Core.Infrastructure;

namespace WireCabinet.Data;

public enum SqlOperation { Read, Write, Function }

public sealed class SqlCatalogItem
{
    public string Id { get; init; } = "";
    public string Dialect { get; init; } = "sqlite";
    public string DataSource { get; init; } = "";
    public SqlOperation Operation { get; init; } = SqlOperation.Read;
    public string Sql { get; init; } = "";
    public string? OracleSql { get; init; }
    public string? MockKind { get; init; }
    public string? MockResultField { get; init; }
    public string BusinessMeaning { get; init; } = "";

    public bool IsMes => Id.StartsWith("mes", StringComparison.OrdinalIgnoreCase);
    public bool IsApp => Id.StartsWith("app", StringComparison.OrdinalIgnoreCase);
}

/// <summary>加载 sql-catalog/items/*.yaml 形成 id -> item 的索引。</summary>
public sealed class SqlCatalog
{
    private readonly Dictionary<string, SqlCatalogItem> _items = new(StringComparer.OrdinalIgnoreCase);

    public IReadOnlyDictionary<string, SqlCatalogItem> Items => _items;

    public SqlCatalogItem? Find(string id) => _items.TryGetValue(id, out var it) ? it : null;

    public static SqlCatalog Load()
    {
        var catalog = new SqlCatalog();
        foreach (var itemsDir in new[] { CabinetPaths.FormalSqlCatalogDir, CabinetPaths.LabSqlCatalogDir })
        {
            if (!Directory.Exists(itemsDir)) continue;
            LoadDirectory(catalog, itemsDir);
        }
        return catalog;
    }

    private static void LoadDirectory(SqlCatalog catalog, string itemsDir)
    {
        foreach (var file in Directory.GetFiles(itemsDir, "*.yaml"))
        {
            var node = Yaml.Parse(File.ReadAllText(file));
            var id = Yaml.Str(node, "id");
            if (string.IsNullOrWhiteSpace(id)) continue;

            var op = (Yaml.Str(node, "operation") ?? "read").ToLowerInvariant() switch
            {
                "write" => SqlOperation.Write,
                "function" => SqlOperation.Function,
                _ => SqlOperation.Read
            };

            var mockNode = Yaml.Get(node, "mock");

            catalog._items[id] = new SqlCatalogItem
            {
                Id = id,
                Dialect = Yaml.Str(node, "dialect") ?? "sqlite",
                DataSource = Yaml.Str(node, "datasource") ?? "",
                Operation = op,
                Sql = (Yaml.Str(node, "sql") ?? "").Trim(),
                OracleSql = Yaml.Str(node, "oracle_sql")?.Trim(),
                MockKind = Yaml.Str(mockNode, "kind"),
                MockResultField = Yaml.Str(mockNode, "result_field"),
                BusinessMeaning = Yaml.Str(node, "business_meaning") ?? ""
            };
        }
    }
}
