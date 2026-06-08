namespace WireCabinet.Data;

/// <summary>FUN_MAT_TRANS_NEW / mes.mat_trans.submit_return 的成功判定与识别。</summary>
public static class MatTransResult
{
    public const string SubmitResultField = "submit_result";

    public static bool IsMatTransItem(SqlCatalogItem item) =>
        item.Id.Contains("mat_trans.submit_return", StringComparison.OrdinalIgnoreCase);

    public static bool IsSuccess(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return true;
        return value.Equals("SUCCESS", StringComparison.OrdinalIgnoreCase);
    }
}
