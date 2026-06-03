using System.IO;

namespace WireFlowLab.Infrastructure;

/// <summary>
/// 解析 wire-flow-lab 根目录及其子资源（data-access-sql、db）的路径。
/// 从程序运行目录向上查找包含 data-access-sql 与 db 的 wire-flow-lab 目录。
/// </summary>
public static class LabPaths
{
    private static string? _root;

    public static string Root => _root ??= ResolveRoot();

    public static string DataAccessSql => Path.Combine(Root, "data-access-sql");
    public static string FlowMapIndex => Path.Combine(DataAccessSql, "flow-sql-map", "index.yaml");
    public static string SqlCatalogDir => Path.Combine(DataAccessSql, "sql-catalog");
    public static string DbDir => Path.Combine(Root, "db");
    public static string SchemaScript => Path.Combine(DbDir, "schema.sqlite.sql");
    public static string SeedScript => Path.Combine(DbDir, "seed.sqlite.sql");
    public static string AppDbPath => Path.Combine(DbDir, "app.db");
    public static string FindingsPath => Path.Combine(Root, "findings.md");

    private static string ResolveRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            var candidate = dir.FullName;
            if (Directory.Exists(Path.Combine(candidate, "data-access-sql")) &&
                Directory.Exists(Path.Combine(candidate, "db")))
            {
                return candidate;
            }
            // 也可能正处于 src/ 之下，向上找名为 wire-flow-lab 的目录
            if (string.Equals(dir.Name, "wire-flow-lab", StringComparison.OrdinalIgnoreCase))
            {
                return candidate;
            }
            dir = dir.Parent;
        }

        // 回退：基于源码相对位置（开发期 dotnet run）
        var guess = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));
        return guess;
    }
}
