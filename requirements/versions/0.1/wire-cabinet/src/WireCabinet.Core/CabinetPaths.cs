namespace WireCabinet.Core;

/// <summary>解析仓库内 flow-sql-map、sql-catalog、0.1 脚本与配置路径。</summary>
public static class CabinetPaths
{
    private static string? _repoRoot;
    private static string? _versionRoot;

    public static string RepoRoot => _repoRoot ??= ResolveRepoRoot();
    /// <summary>0.1 需求与设计文档根目录（不含可运行工程）。</summary>
    public static string RequirementsVersionRoot =>
        Path.Combine(RepoRoot, "requirements", "versions", "0.1");

    /// <summary>WireCabinet 工程、现场配置与 SQLite 数据根目录。</summary>
    public static string VersionRoot =>
        _versionRoot ??= Path.Combine(RequirementsVersionRoot, "wire-cabinet");

    public static string FlowMapIndex =>
        Path.Combine(RepoRoot, "requirements", "flows", "flow-sql-map", "index.yaml");

    public static string FormalSqlCatalogDir =>
        Path.Combine(RepoRoot, "requirements", "data-access-sql", "sql-catalog", "items");

    public static string LabSqlCatalogDir =>
        Path.Combine(RepoRoot, "requirements", "validation", "wire-flow-lab", "data-access-sql", "sql-catalog", "items");

    public static string ScriptsDir => Path.Combine(VersionRoot, "scripts");
    public static string SchemaScript => Path.Combine(ScriptsDir, "schema.sqlite.sql");
    public static string SeedSlotScript => Path.Combine(ScriptsDir, "seed-app_slot.sqlite.sql");
    public static string CreateMaterialsScript => Path.Combine(ScriptsDir, "create-welding_wire_materials.sqlite.sql");
    public static string SeedMaterialsScript => Path.Combine(ScriptsDir, "seed-welding_wire_materials.sqlite.sql");

    public static string SlotConfigDir => Path.Combine(VersionRoot, "slot-config");
    public static string SlotIoMappingFile => Path.Combine(SlotConfigDir, "slot-io-mapping.generated.yaml");
    public static string IoModulesFile => Path.Combine(SlotConfigDir, "io-modules.yaml");
    public static string StationsFile => Path.Combine(VersionRoot, "station-config", "stations.yaml");

    public static string DefaultAppDbPath => Path.Combine(VersionRoot, "data", "app.db");

    private static string ResolveRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            var indexFile = Path.Combine(dir.FullName, "requirements", "flows", "flow-sql-map", "index.yaml");
            if (File.Exists(indexFile))
                return dir.FullName;
            if (string.Equals(dir.Name, "AuxiliaryMaterialsMulti-slotRobt", StringComparison.OrdinalIgnoreCase))
                return dir.FullName;
            dir = dir.Parent;
        }

        return Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "..", "..", ".."));
    }
}
