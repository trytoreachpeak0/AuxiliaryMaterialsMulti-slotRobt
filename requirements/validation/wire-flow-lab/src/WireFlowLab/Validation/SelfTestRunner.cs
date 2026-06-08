using System.IO;
using System.Text;
using WireFlowLab.Data;
using WireFlowLab.Flows;
using WireFlowLab.Infrastructure;
using WireFlowLab.SlotControl;

namespace WireFlowLab.Validation;

/// <summary>
/// 无界面自测：对 6 个流程做静态校验 + 主路径自动执行（SQLite mock 模式），
/// 输出 trace 与 findings，便于在不启动 UI 的情况下验证引擎与 SQL。
/// </summary>
public static class SelfTestRunner
{
    public static string Run()
    {
        var sb = new StringBuilder();
        var catalog = SqlCatalog.Load();
        var db = new SqliteDb(LabPaths.AppDbPath);
        var settings = new MesFunctionSettings();
        var flows = FlowLoader.LoadAll();

        sb.AppendLine($"# WireFlowLab 自测 ({DateTime.Now:yyyy-MM-dd HH:mm:ss})");
        sb.AppendLine($"flows: {flows.Count}, catalog items: {catalog.Items.Count}");
        sb.AppendLine();

        var allFindings = new List<Finding>();

        foreach (var flow in flows)
        {
            sb.AppendLine(new string('=', 80));
            sb.AppendLine($"流程: {flow.FlowId} — {flow.Title}");
            sb.AppendLine($"节点数: {flow.Nodes.Count}, node_order: {flow.NodeOrder.Count}");

            var staticFindings = FlowValidator.Validate(flow, catalog);
            allFindings.AddRange(staticFindings);
            sb.AppendLine($"-- 静态校验: {staticFindings.Count} 条");
            foreach (var f in staticFindings)
                sb.AppendLine("   " + f);

            // 每个流程在干净的数据库上跑主路径
            db.Initialize(recreate: true);
            var slot = new MockSlotController(db);
            var svc = new EngineServices
            {
                Catalog = catalog,
                AppDb = new SqliteAppDb(db),
                Mes = new SqliteMockMesGateway(db, settings),
                Slot = slot,
                SystemAgvNo = "AGV-01",
                SystemMesWriter = "新厂前线物料多仓位2"
            };
            var engine = new FlowEngine(svc);
            engine.Begin(flow);

            // 用默认值填充所有 user_input 节点
            foreach (var (id, node) in flow.Nodes)
                if (node.Type == NodeType.UserInput)
                {
                    var map = new Dictionary<string, string>();
                    foreach (var o in node.Outputs) map[o.Name] = o.Default;
                    engine.Context!.UserInputs[id] = map;
                }

            sb.AppendLine("-- 主路径执行:");
            var guard = 0;
            while (!engine.Finished && guard++ < 300)
            {
                var t = engine.StepOnce();
                allFindings.AddRange(t.Findings);
                sb.AppendLine($"   [{t.Step:00}] {t.NodeId} ({t.NodeType}) => {t.Outcome} -> {t.NextNode ?? "END"}");
                if (!string.IsNullOrWhiteSpace(t.RenderedSql))
                    sb.AppendLine($"        SQL: {OneLine(t.RenderedSql)}");
                sb.AppendLine($"        结果: {t.Result}");
                foreach (var f in t.Findings) sb.AppendLine("        ! " + f);
            }
            if (guard >= 300) sb.AppendLine("   !! 超过步数上限，疑似死循环");
            sb.AppendLine();
        }

        sb.AppendLine(new string('=', 80));
        sb.AppendLine("# Findings 汇总");
        foreach (var grp in allFindings.GroupBy(f => f.Severity).OrderByDescending(g => g.Key))
        {
            sb.AppendLine($"## {grp.Key} ({grp.Count()})");
            foreach (var f in grp) sb.AppendLine("- " + f);
        }

        var outPath = Path.Combine(LabPaths.DbDir, "selftest-output.txt");
        File.WriteAllText(outPath, sb.ToString());
        return outPath;
    }

    private static string OneLine(string s) => s.Replace("\r", " ").Replace("\n", " ").Trim();
}
