using WireCabinet.Data;
using WireCabinet.Flows;
using WireCabinet.Hmi;
using WireCabinet.Hmi.Views;

namespace WireCabinet.Hmi.Services;

public sealed class FlowCoordinator
{
    private readonly AppBootstrap _app;
    private readonly FlowTraceHub? _traceHub;
    private FlowSessionRunner? _runner;
    private FlowDefinition? _flow;
    private int _publishedCount;
    private string? _sessionId;

    private static readonly HashSet<string> AutoAckUserInputNodes = new(StringComparer.OrdinalIgnoreCase)
    {
        "showOPName",
        "showWireReturnSuccessMessage",
        "showLoadWireAndCloseDoorMessage",
        "loadReturnedWire",
        "showWireIssueSuccessMessage",
        "showUnloadWireAndCloseDoorMessage",
        "unloadWire",
        "showRemainingQtyWrongHint"
    };

    private static readonly HashSet<string> OpNonRetryableTerminalNodes = new(StringComparer.OrdinalIgnoreCase)
    {
        "showReturnedWeightNotExists",
        "showNoMatchedWireMessage",
        "showWireReturnFailMessage",
        "showNoReturnSlotAvailableMessage",
        "showOpenReturnSlotFailedMessage",
        "showWireQuotaCheckAbnormalMessage",
        "showWireIssueFailMessage",
        "showSubmitWireIssueErrorMessage",
        "showCheckSubmitWireIssueFailMessage",
        "showQueryWireByLotNoErrorMessage",
        "showQueryProductByLotNoErrorMessage",
        "showCheckProductInfoNotExistsMessage",
        "showWireInfoNotExists",
        "showOpenIssueSlotErrorMessage",
        "showUpdateIssueSlotAfterUnloadErrorMessage",
        "showOPIdMultipleRecordsMessage",
        "showEqpNoMultipleRecordsMessage",
        "showWireByLotNoMultipleRecordsMessage"
    };

    public const string ManualReturnGuidanceSuffix = "到人工窗口归还";

    private string? _lastFailTerminalNodeId;

    public FlowCoordinator(AppBootstrap app, FlowTraceHub? traceHub = null)
    {
        _app = app;
        _traceHub = traceHub;
    }

    public bool TryBeginFlow(string flowId, out string message)
    {
        if (!_app.WireSession.TryBegin(flowId, out message))
            return false;

        _flow = _app.Flows.FirstOrDefault(f => f.FlowId == flowId);
        if (_flow is null)
        {
            message = $"未找到流程 {flowId}";
            _app.WireSession.End();
            return false;
        }

        _runner = new FlowSessionRunner(_app.EngineServices);
        _runner.Begin(_flow);
        _publishedCount = 0;
        _sessionId = _traceHub?.BeginSession(_flow.FlowId, _flow.Title);
        _lastFailTerminalNodeId = null;
        message = "";
        return true;
    }

    public void EndFlow()
    {
        FlushNewTraceEntries();
        if (_traceHub is not null && _flow is not null && _sessionId is not null)
            _traceHub.EndSession(_flow.FlowId, _flow.Title, _sessionId);

        _runner = null;
        _flow = null;
        _sessionId = null;
        _publishedCount = 0;
        _app.WireSession.End();
    }

    /// <summary>存料流程 runner 已结束但 WireSession 仍挂着 load flowId 时清理。</summary>
    public void ClearStaleLoadSession()
    {
        if (IsFlowActive || !_app.WireSession.HasActiveSession)
            return;

        if (!string.Equals(_app.WireSession.ActiveFlowId, MhStationAccess.LoadFlowId, StringComparison.OrdinalIgnoreCase))
            return;

        _app.WireSession.End();
    }

    public bool IsFlowActive => _runner is not null;

    public (bool Ok, string Message, FlowPauseReason Reason) AdvanceUntilPause(
        IDictionary<string, string>? userInput = null,
        string? pauseBeforeNodeId = null,
        bool pauseBeforeDoorClose = false,
        bool endFlowOnTerminal = true)
    {
        if (_runner is null)
            return (false, "流程未开始", FlowPauseReason.Error);

        if (userInput is not null && _runner.Engine.CurrentNodeId is not null)
            _runner.SetUserInput(_runner.Engine.CurrentNodeId, userInput);

        var entry = _runner.RunUntilPause(pauseBeforeNodeId, pauseBeforeDoorClose);
        FlushNewTraceEntries();
        if (_runner.PauseReason == FlowPauseReason.Error)
        {
            _lastFailTerminalNodeId = null;
            return (false, PickFlowErrorMessage(_runner.PauseMessage, entry?.Result, "流程错误"), FlowPauseReason.Error);
        }

        if (_runner.PauseReason == FlowPauseReason.Terminal)
        {
            var lastNode = _runner.Engine.Trace.LastOrDefault();
            var isFail = string.Equals(lastNode?.Outcome, "fail", StringComparison.OrdinalIgnoreCase)
                || string.Equals(entry?.Outcome, "fail", StringComparison.OrdinalIgnoreCase);
            var msg = isFail
                ? MapTerminalFailMessage(lastNode?.NodeId)
                : (_runner.PauseMessage ?? entry?.Result ?? "流程结束");
            if (isFail)
                _lastFailTerminalNodeId = lastNode?.NodeId;
            else
                _lastFailTerminalNodeId = null;
            if (endFlowOnTerminal)
                EndFlow();
            if (isFail)
                return (false, msg, FlowPauseReason.Error);
            return (true, msg, FlowPauseReason.Terminal);
        }

        _lastFailTerminalNodeId = null;
        return (true, _runner.PauseMessage ?? "等待输入", _runner.PauseReason);
    }

    public string? CurrentNodeId => _runner?.Engine.CurrentNodeId;

    public object? GetField(string nodeFieldRef)
    {
        if (_runner?.Engine.Context is null) return null;
        return _runner.Engine.Context.GetRef(nodeFieldRef);
    }

    public void FillOpStep0(string opId, string workGroup, string shift) =>
        _runner?.SetUserInput("inputOperatorWorkInfo", new Dictionary<string, string>
        {
            ["operator_id"] = opId,
            ["work_group"] = workGroup,
            ["shift"] = shift
        });

    public void FillOpReturnedWireLot(string lotNo) =>
        _runner?.SetUserInput("inputReturnedWireLotNo", new Dictionary<string, string>
        {
            ["returned_wire_lot_no"] = lotNo
        });

    public void FillOpEqpNo(string eqpNo) =>
        _runner?.SetUserInput("inputEqpNo", new Dictionary<string, string>
        {
            ["eqp_no"] = eqpNo
        });

    public void FillOpRemainingQty(string qty) =>
        _runner?.SetUserInput("inputRemainingQty", new Dictionary<string, string>
        {
            ["remaining_qty"] = qty
        });

    /// <summary>领用批次号：操作员手动输入，不与步骤③归还批次号联动/预填。</summary>
    public void FillOpIssueProductLotNo(string lotNo) =>
        _runner?.SetUserInput("inputIssueProductLotNo", new Dictionary<string, string>
        {
            ["issue_product_lot_no"] = lotNo
        });

    /// <summary>步骤①：校验操作员后暂停在归还批号输入。</summary>
    public (bool Ok, string Message, FlowPauseReason Reason) AdvanceOpOperatorValidate()
    {
        var result = AdvanceUntilPauseAutoAck(
            pauseBeforeNodeId: "inputReturnedWireLotNo",
            endFlowOnTerminal: false);
        if (!result.Ok)
            return result;
        if ((result.Reason == FlowPauseReason.UserInput || result.Reason == FlowPauseReason.AwaitingAction)
            && string.Equals(CurrentNodeId, "inputReturnedWireLotNo", StringComparison.OrdinalIgnoreCase))
            return (true, "操作员校验通过。", result.Reason);
        EndFlow();
        return (false, MapOpFlowMessage(), FlowPauseReason.Error);
    }

    /// <summary>步骤②：批号查询后暂停在机台号输入。</summary>
    public (bool Ok, string Message, FlowPauseReason Reason) AdvanceOpWireLotQuery(string lotNo)
    {
        FillOpReturnedWireLot(lotNo);
        var result = RunOpWireLotQueryUntilPause();

        // 首次点击可能只提交了 user_input 暂停点，补推一次直至查询真正执行
        if (result.Reason == FlowPauseReason.UserInput
            && string.Equals(CurrentNodeId, "inputReturnedWireLotNo", StringComparison.OrdinalIgnoreCase))
        {
            FillOpReturnedWireLot(lotNo);
            result = RunOpWireLotQueryUntilPause();
        }

        if (!result.Ok)
        {
            RewindOpToWireLotInput();
            return result;
        }
        if (result.Reason == FlowPauseReason.AwaitingAction
            && string.Equals(CurrentNodeId, "inputEqpNo", StringComparison.OrdinalIgnoreCase))
        {
            var rowCount = _runner?.Engine.Context?.RowCounts.GetValueOrDefault("queryWireSpecByLotNo") ?? 0;
            if (rowCount <= 0)
            {
                RewindOpToWireLotInput();
                return (false, "焊丝批号无效或不存在", FlowPauseReason.Error);
            }
            return (true, "焊丝批号查询通过。", result.Reason);
        }
        RewindOpToWireLotInput();
        return (false, MapOpFlowMessage(), FlowPauseReason.Error);
    }

    private (bool Ok, string Message, FlowPauseReason Reason) RunOpWireLotQueryUntilPause() =>
        AdvanceUntilPause(pauseBeforeNodeId: "inputEqpNo", endFlowOnTerminal: false);

    private void RewindOpToWireLotInput() =>
        _runner?.RewindToNode("inputReturnedWireLotNo");

    private void RewindOpToEqpInput() =>
        _runner?.RewindToNode("inputEqpNo");

    private void RewindOpToRemainingQtyInput() =>
        _runner?.RewindToNode("inputRemainingQty");

    private void RewindOpToIssueLotInput() =>
        _runner?.RewindToNode("inputIssueProductLotNo");

    public static bool IsReturnQtyRejectMessage(string? message) =>
        FlowEngine.IsReturnQtyReject(message);

    public static bool IsQuotaRejectMessage(string? message) =>
        FlowEngine.IsQuotaReject(message);

    public static string AppendManualReturnGuidance(string message)
    {
        if (string.IsNullOrWhiteSpace(message))
            return ManualReturnGuidanceSuffix;
        var trimmed = message.Trim();
        if (trimmed.EndsWith(ManualReturnGuidanceSuffix, StringComparison.Ordinal))
            return trimmed;
        return trimmed + "，" + ManualReturnGuidanceSuffix;
    }

    public bool ShouldAppendManualReturnGuidance(string? message)
    {
        if (IsReturnQtyRejectMessage(message) || IsQuotaRejectMessage(message))
            return false;
        return _lastFailTerminalNodeId is not null
               && OpNonRetryableTerminalNodes.Contains(_lastFailTerminalNodeId);
    }

    /// <summary>步骤③：机台校验后暂停在剩余芯片输入。</summary>
    public (bool Ok, string Message, FlowPauseReason Reason) AdvanceOpEqpValidate(string eqpNo)
    {
        FillOpEqpNo(eqpNo);
        var result = AdvanceUntilPause(pauseBeforeNodeId: "inputRemainingQty", endFlowOnTerminal: false);
        if (!result.Ok)
        {
            RewindOpToEqpInput();
            return result;
        }
        if (result.Reason == FlowPauseReason.AwaitingAction
            && string.Equals(CurrentNodeId, "inputRemainingQty", StringComparison.OrdinalIgnoreCase))
            return (true, "机台校验通过。", result.Reason);
        EndFlow();
        return (false, MapOpFlowMessage(), FlowPauseReason.Error);
    }

    /// <summary>步骤④前半：用量校验后暂停在 MES 归还提交之前。</summary>
    public (bool Ok, string Message, FlowPauseReason Reason) AdvanceOpQuotaCheck(string remainingQty)
    {
        FillOpRemainingQty(remainingQty);
        var result = AdvanceUntilPause(pauseBeforeNodeId: "submitWireReturn", endFlowOnTerminal: false);

        // 按最近一次配额结果判定，避免首次 reject 后第二次正确输入仍被历史 trace 误判。
        var latestQuota = GetLatestTraceOutcome("queryWireQuotaCheck");
        if (string.Equals(latestQuota, "quota_reject", StringComparison.OrdinalIgnoreCase)
            || string.Equals(CurrentNodeId, "showRemainingQtyWrongHint", StringComparison.OrdinalIgnoreCase)
            || IsQuotaRejectMessage(result.Message)
            || IsQuotaRejectMessage(TryPickTraceSqlError("queryWireQuotaCheck")))
        {
            RewindOpToRemainingQtyInput();
            var quotaErr = TryPickTraceSqlError("queryWireQuotaCheck");
            return (false,
                quotaErr ?? "剩余待焊芯片数量不正确（剩余产量不能大于待完工产量），请重新输入。",
                FlowPauseReason.Error);
        }

        if (!result.Ok)
            return result;

        if (result.Reason == FlowPauseReason.AwaitingAction
            && string.Equals(CurrentNodeId, "submitWireReturn", StringComparison.OrdinalIgnoreCase))
            return (true, "用量校验通过。", result.Reason);

        EndFlow();
        return (false, MapOpFlowMessage(), FlowPauseReason.Error);
    }

    /// <summary>步骤④：MES 归还提交并打开归还格口，暂停在关门。</summary>
    public (bool Ok, string Message, FlowPauseReason Reason) AdvanceOpSubmitReturnToDoorClose()
    {
        var result = AdvanceUntilPauseAutoAck(
            pauseBeforeNodeId: "closeReturnSlotDoor",
            endFlowOnTerminal: false);
        if (TraceHasReturnQtyReject())
            return (false, PickReturnQtyRejectMessage(), FlowPauseReason.Error);
        if (!result.Ok)
            return result;
        if (IsOpReturnDoorWaitPause(result.Reason, CurrentNodeId))
            return (true, "请放入归还焊丝并关闭格口。", FlowPauseReason.AwaitingDoorClose);
        EndFlow();
        return (false, MapOpFlowMessage(), FlowPauseReason.Error);
    }

    /// <summary>归还格口已关：写库并推进到领用批次号手动输入前。</summary>
    public (bool Ok, string Message, FlowPauseReason Reason) AdvanceOpAfterReturnDoorClosed()
    {
        _runner?.SetUserInput("closeReturnSlotDoor", new Dictionary<string, string> { ["closed"] = "true" });
        var result = AdvanceUntilPauseAutoAck(pauseBeforeNodeId: "inputIssueProductLotNo", endFlowOnTerminal: false);
        if (!result.Ok)
            return result;
        if (result.Reason == FlowPauseReason.AwaitingAction
            && string.Equals(CurrentNodeId, "inputIssueProductLotNo", StringComparison.OrdinalIgnoreCase))
            return (true, "归还已提交。请手动输入领用工单批次号并点「校验」。", result.Reason);
        EndFlow();
        return (false, MapOpFlowMessage(), FlowPauseReason.Error);
    }

    /// <summary>领用批次号校验：与步骤③归还批次号相互独立，不自动预填/复用；通过后暂停在领用提交前。</summary>
    public (bool Ok, string Message, FlowPauseReason Reason) AdvanceOpIssueLotValidate(string issueProductLotNo)
    {
        FillOpIssueProductLotNo(issueProductLotNo);
        var result = AdvanceUntilPauseAutoAck(pauseBeforeNodeId: "submitWireIssue", endFlowOnTerminal: false);
        if (!result.Ok)
        {
            RewindOpToIssueLotInput();
            return result;
        }
        if (result.Reason == FlowPauseReason.AwaitingAction
            && string.Equals(CurrentNodeId, "submitWireIssue", StringComparison.OrdinalIgnoreCase))
            return (true, "领用批次校验通过。请点「提交领用焊丝」。", result.Reason);
        RewindOpToIssueLotInput();
        return (false, MapOpFlowMessage(), FlowPauseReason.Error);
    }

    /// <summary>步骤⑤：MES 领用提交并打开领用格口，暂停在关门。</summary>
    public (bool Ok, string Message, FlowPauseReason Reason) AdvanceOpSubmitIssueToDoorClose()
    {
        var result = AdvanceUntilPauseAutoAck(
            pauseBeforeNodeId: "closeIssueSlotDoor",
            endFlowOnTerminal: false);
        if (!result.Ok)
            return result;
        if (IsOpIssueDoorWaitPause(result.Reason, CurrentNodeId))
        {
            SaveOpInterruptedIssueSnapshot();
            return (true, "请取出可用焊丝并关闭格口。", FlowPauseReason.AwaitingDoorClose);
        }
        EndFlow();
        return (false, MapOpFlowMessage(), FlowPauseReason.Error);
    }

    /// <summary>领用格口已关：完成流程。</summary>
    public (bool Ok, string Message, FlowPauseReason Reason) AdvanceOpAfterIssueDoorClosed()
    {
        _runner?.SetUserInput("closeIssueSlotDoor", new Dictionary<string, string> { ["closed"] = "true" });
        var result = AdvanceUntilPauseAutoAck(endFlowOnTerminal: true);
        if (result.Reason == FlowPauseReason.Terminal && result.Ok)
        {
            _app.OpInterruptedIssue.Clear();
            return (true, "领用已提交，流程完成。", result.Reason);
        }

        if (!result.Ok)
            UpdateOpInterruptedProgressFromTrace();

        if (!result.Ok)
            return result;
        EndFlow();
        return (false, MapOpFlowMessage(), FlowPauseReason.Error);
    }

    private (bool Ok, string Message, FlowPauseReason Reason) AdvanceUntilPauseAutoAck(
        string? pauseBeforeNodeId = null,
        bool pauseBeforeDoorClose = false,
        bool endFlowOnTerminal = true)
    {
        for (var i = 0; i < 40; i++)
        {
            var result = AdvanceUntilPause(
                pauseBeforeNodeId: pauseBeforeNodeId,
                pauseBeforeDoorClose: pauseBeforeDoorClose,
                endFlowOnTerminal: endFlowOnTerminal);

            if (_runner?.PauseReason != FlowPauseReason.UserInput
                || CurrentNodeId is null
                || !AutoAckUserInputNodes.Contains(CurrentNodeId))
                return result;

            AcknowledgeUserInputNode(CurrentNodeId);
        }

        return (false, "流程步骤过多。", FlowPauseReason.Error);
    }

    private void AcknowledgeUserInputNode(string nodeId)
    {
        var fields = nodeId switch
        {
            "loadReturnedWire" => new Dictionary<string, string> { ["loaded"] = "true" },
            "unloadWire" => new Dictionary<string, string> { ["unloaded"] = "true" },
            _ => new Dictionary<string, string> { ["acknowledged"] = "true" }
        };
        _runner?.SetUserInput(nodeId, fields);
    }

    private static bool IsOpReturnDoorWaitPause(FlowPauseReason reason, string? currentNodeId) =>
        (reason == FlowPauseReason.AwaitingAction || reason == FlowPauseReason.UserInput)
        && string.Equals(currentNodeId, "closeReturnSlotDoor", StringComparison.OrdinalIgnoreCase);

    private static bool IsOpIssueDoorWaitPause(FlowPauseReason reason, string? currentNodeId) =>
        (reason == FlowPauseReason.AwaitingAction || reason == FlowPauseReason.UserInput)
        && string.Equals(currentNodeId, "closeIssueSlotDoor", StringComparison.OrdinalIgnoreCase);

    private string MapOpFlowMessage() =>
        MapTerminalFailMessage(_runner?.Engine.Trace.LastOrDefault()?.NodeId);

    public const string MhLoadFlowId = MhStationAccess.LoadFlowId;

    public void FillMhLotNo(string lotNo) =>
        _runner?.SetUserInput("inputAvailableWireLotNo", new Dictionary<string, string>
        {
            ["available_wire_lot_no"] = lotNo
        });

    /// <summary>批号校验：暂停在 openAvailableSlot 之前。</summary>
    public (bool Ok, string Message, FlowPauseReason Reason) AdvanceMhLoadWireQuery(string lotNo)
    {
        if (!TryBeginFlow(MhLoadFlowId, out var beginMsg))
            return (false, beginMsg, FlowPauseReason.Error);

        FillMhLotNo(lotNo);
        var result = AdvanceUntilPause(pauseBeforeNodeId: "openAvailableSlot", endFlowOnTerminal: false);
        if (result.Reason == FlowPauseReason.Terminal)
        {
            var msg = MapMhLoadTerminalMessage();
            EndFlow();
            return (false, msg, FlowPauseReason.Error);
        }
        if (!result.Ok)
            return (false, result.Message, FlowPauseReason.Error);
        if (result.Reason == FlowPauseReason.AwaitingAction && CurrentNodeId == "openAvailableSlot")
            return (true, "批号校验通过。", result.Reason);
        EndFlow();
        return (false, MapMhLoadTerminalMessage(), FlowPauseReason.Error);
    }

    public (bool Ok, string Message, FlowPauseReason Reason) AdvanceMhLoadWireValidateAndOpen(string lotNo)
    {
        var query = AdvanceMhLoadWireQuery(lotNo);
        if (!query.Ok)
            return query;
        return AdvanceMhLoadWireOpen();
    }

    public (bool Ok, string Message, FlowPauseReason Reason) AdvanceMhLoadWireOpen()
    {
        if (App.MhDoors.HasActiveDoorOnlySession)
        {
            EndFlow();
            return (false, "格口开门会话进行中，请先关闭所有已开格口后再存料。", FlowPauseReason.Error);
        }

        ClearStaleLoadSession();
        var block = MhOperationLock.Evaluate(reloadDb: false);
        if (block.IsBlocked)
        {
            EndFlow();
            return (false, block.Message, FlowPauseReason.Error);
        }

        var result = AdvanceUntilPause(pauseBeforeDoorClose: true);
        if (result.Reason == FlowPauseReason.AwaitingDoorClose)
        {
            SaveInterruptedLoadSnapshot();
            return (true, "请放入焊丝并关闭格口。", result.Reason);
        }
        if (!result.Ok)
            return (false, result.Message, FlowPauseReason.Error);
        return (false, MapMhLoadTerminalMessage(), FlowPauseReason.Error);
    }

    public (bool Ok, string Message, FlowPauseReason Reason) AdvanceMhLoadWireAfterDoorClosed()
    {
        var result = AdvanceUntilPause(pauseBeforeDoorClose: false);
        if (result.Reason == FlowPauseReason.Terminal)
        {
            if (result.Ok)
                _app.InterruptedLoad.Clear();
            else
                UpdateMhInterruptedProgressFromTrace();
            return (true, result.Message, FlowPauseReason.Terminal);
        }
        if (!result.Ok)
        {
            UpdateMhInterruptedProgressFromTrace();
            return (false, result.Message, FlowPauseReason.Error);
        }
        return (false, result.Message, result.Reason);
    }

    private string MapTerminalFailMessage(string? nodeId)
    {
        var mesMsg = TryPickMesSyncFailMessage();
        if (!string.IsNullOrWhiteSpace(mesMsg))
            return mesMsg;

        return nodeId switch
    {
        "showOPIdNotExistsMessage" => "不存在该操作人员",
        "showOPIdMultipleRecordsMessage" => "操作员工号查询到多条记录，数据异常",
        "showWireLotNoInputInvalidOrNotExistsMessage" => "焊丝批号无效或不存在",
        "showWireSpecNotExistsMessage" => "焊丝规格不存在",
        "showNoMatchedWireMessage" => "柜内无匹配规格的可用焊丝",
        "showReturnedWeightNotExists" => "未配置归还重量，请转人工归还",
        "showEqpNoNotExistsMessage" => "机台号不存在",
        "showEqpNoMultipleRecordsMessage" => "机台号查询到多条记录，数据异常",
        "showLastProductLotNoNotExistsMessage" => "未查询到最近产品批号",
        "showWireReturnFailMessage" => ResolveSubmitFailMessage("submitWireReturn", "归还提交失败"),
        "showNoReturnSlotAvailableMessage" => "无空闲归还格口，请转人工归还",
        "showOpenReturnSlotFailedMessage" => "无法打开归还格口，请检查柜门或联系维护",
        "showWireIssueFailMessage" => ResolveSubmitFailMessage("submitWireIssue", "领用提交失败"),
        "showQueryWireByLotNoErrorMessage" => "查询可用焊丝信息失败，请稍后重试或联系维护",
        "showQueryProductByLotNoErrorMessage" => "查询产品批次信息失败，请稍后重试或联系维护",
        "showCheckProductInfoNotExistsMessage" => "未查询到有效产品信息，无法领用",
        "showSubmitWireIssueErrorMessage" => ResolveSubmitFailMessage("submitWireIssue", "领用提交调用失败，请稍后重试"),
        "showCheckSubmitWireIssueFailMessage" => ResolveSubmitFailMessage("submitWireIssue", "领用提交失败"),
        "showOpenIssueSlotErrorMessage" => "无法打开领用格口，请检查柜门或联系维护",
        "showUpdateIssueSlotAfterUnloadErrorMessage" => "领用格口库存更新失败，请联系维护",
        "showWireByLotNoMultipleRecordsMessage" => "焊丝批号查询到多条记录，数据异常",
        "showWireInfoNotExists" => "未查询到可用焊丝信息，无法领用",
        "showWireQuotaCheckAbnormalMessage" => TryPickTraceSqlError("queryWireQuotaCheck")
            ?? "查询实际与理论消耗差值异常，请转人工处理",
        "showNoAvailableSlotMessage" => "无空闲格口",
        "showWireLotNoAlreadyInCabinetMessage" => BuildAlreadyInCabinetMessage(),
        _ => _runner?.PauseMessage ?? "流程未通过。"
        };
    }

    private string? TryPickMesSyncFailMessage()
    {
        var entry = _runner?.Engine.Trace.LastOrDefault(t =>
            t.Findings.Any(f => string.Equals(f.Category, WireMesDiscoMessages.FindingCategory, StringComparison.Ordinal)));
        return entry?.Findings
            .FirstOrDefault(f => string.Equals(f.Category, WireMesDiscoMessages.FindingCategory, StringComparison.Ordinal))
            ?.Message;
    }

    private void SaveOpInterruptedIssueSnapshot()
    {
        var slotId = ToLong(GetField("queryMatchedAvailableWire.slot_id"));
        var slotNo = GetField("queryMatchedAvailableWire.slot_no")?.ToString()?.Trim() ?? "";
        var lot = GetField("queryMatchedAvailableWire.matched_available_wire_lot_no")?.ToString()?.Trim() ?? "";
        if (slotId <= 0 || string.IsNullOrEmpty(slotNo) || string.IsNullOrEmpty(lot))
            return;

        _app.OpInterruptedIssue.Save(slotId, slotNo, lot, submitIssueDone: true);
    }

    private void UpdateOpInterruptedProgressFromTrace()
    {
        var pickupDone = TraceHasSuccessfulSql("app.slot.complete_issue_pickup");
        var mesFail = TryPickMesSyncFailMessage() is not null;
        var mesDone = pickupDone && !mesFail;
        if (pickupDone || mesDone)
            _app.OpInterruptedIssue.UpdateProgress(pickupDone: pickupDone, mesDiscoDone: mesDone);
    }

    private void UpdateMhInterruptedProgressFromTrace()
    {
        var bindDone = TraceHasSuccessfulSql("app.slot.bind_wire");
        var mesFail = TryPickMesSyncFailMessage() is not null;
        var mesDone = bindDone && !mesFail;
        if (bindDone || mesDone)
            _app.InterruptedLoad.UpdateProgress(bindDone: bindDone, mesDiscoDone: mesDone);
    }

    private bool TraceHasSuccessfulSql(string sqlId) =>
        _runner?.Engine.Trace.Any(t =>
            string.Equals(t.SqlId, sqlId, StringComparison.OrdinalIgnoreCase)
            && string.Equals(t.Outcome, "success", StringComparison.OrdinalIgnoreCase)) == true;

    private bool TraceHasOutcome(string nodeId, string outcome) =>
        _runner?.Engine.Trace.Any(t =>
            string.Equals(t.NodeId, nodeId, StringComparison.OrdinalIgnoreCase)
            && string.Equals(t.Outcome, outcome, StringComparison.OrdinalIgnoreCase)) == true;

    private string? GetLatestTraceOutcome(string nodeId) =>
        _runner?.Engine.Trace.LastOrDefault(t =>
            string.Equals(t.NodeId, nodeId, StringComparison.OrdinalIgnoreCase))?.Outcome;

    private bool TraceHasReturnQtyReject() =>
        string.Equals(GetLatestTraceOutcome("submitWireReturn"), "return_qty_reject", StringComparison.OrdinalIgnoreCase)
        || string.Equals(GetLatestTraceOutcome("checkSubmitWireReturnSuccess"), "return_qty_reject", StringComparison.OrdinalIgnoreCase);

    private string PickReturnQtyRejectMessage()
    {
        var fromTrace = TryPickTraceSqlError("submitWireReturn");
        if (!string.IsNullOrWhiteSpace(fromTrace) && FlowEngine.IsReturnQtyReject(fromTrace))
            return fromTrace;

        var submitResult = GetField("submitWireReturn.result")?.ToString();
        if (!string.IsNullOrWhiteSpace(submitResult) && FlowEngine.IsReturnQtyReject(submitResult))
            return submitResult;

        return "归还数量不能大于产品待完工数量，请重新输入剩余芯片并校验。";
    }

    private string ResolveSubmitFailMessage(string submitNodeId, string fallback)
    {
        var fromTrace = TryPickTraceSqlError(submitNodeId);
        if (!string.IsNullOrWhiteSpace(fromTrace))
            return fromTrace;

        var submitResult = GetField($"{submitNodeId}.{MatTransResult.SubmitResultField}")?.ToString()
                           ?? GetField($"{submitNodeId}.result")?.ToString();
        if (!string.IsNullOrWhiteSpace(submitResult) && !MatTransResult.IsSuccess(submitResult))
            return submitResult;

        var pause = PickFlowErrorMessage(_runner?.PauseMessage, null, "");
        if (!string.IsNullOrWhiteSpace(pause) && !IsTerminalPlaceholderMessage(pause))
            return pause;

        return fallback;
    }

    private string? TryPickTraceSqlError(string sqlNodeId)
    {
        var entry = _runner?.Engine.Trace.LastOrDefault(t =>
            string.Equals(t.NodeId, sqlNodeId, StringComparison.OrdinalIgnoreCase));
        var msg = NormalizeFlowError(entry?.Result);
        return string.IsNullOrWhiteSpace(msg) ? null : msg;
    }

    private static bool IsTerminalPlaceholderMessage(string message) =>
        message.StartsWith("提示:", StringComparison.Ordinal);

    private static string PickFlowErrorMessage(string? primary, string? secondary, string fallback)
    {
        var msg = NormalizeFlowError(primary);
        if (!string.IsNullOrWhiteSpace(msg)) return msg;
        msg = NormalizeFlowError(secondary);
        if (!string.IsNullOrWhiteSpace(msg)) return msg;
        return fallback;
    }

    private static string NormalizeFlowError(string? message)
    {
        if (string.IsNullOrWhiteSpace(message)) return "";
        const string prefix = "错误: ";
        return message.StartsWith(prefix, StringComparison.Ordinal) ? message[prefix.Length..].Trim() : message.Trim();
    }

    private string BuildAlreadyInCabinetMessage()
    {
        var slotNo = GetField("queryAppWireLotNo.slot_no")?.ToString()?.Trim();
        return string.IsNullOrEmpty(slotNo)
            ? "此批号已在柜内"
            : $"此批号已在柜内（{MhOpenDoorGuard.FormatCabinetSlotLabel(slotNo)}）";
    }

    private string MapMhLoadTerminalMessage() =>
        MapTerminalFailMessage(_runner?.Engine.Trace.LastOrDefault()?.NodeId);

    private void SaveInterruptedLoadSnapshot()
    {
        var slotId = ToLong(GetField("checkAvailableSlot.available_slot_id"));
        var slotNo = GetField("checkAvailableSlot.slot_no")?.ToString()?.Trim() ?? "";
        var lot = GetField("inputAvailableWireLotNo.available_wire_lot_no")?.ToString()?.Trim() ?? "";
        var spec = GetField("queryWireSpecByLotNo.spec")?.ToString()?.Trim();
        if (slotId <= 0 || string.IsNullOrEmpty(slotNo) || string.IsNullOrEmpty(lot))
            return;

        _app.InterruptedLoad.Save(slotId, slotNo, lot, spec);
    }

    private void FlushNewTraceEntries()
    {
        if (_traceHub is null || _runner is null || _flow is null || _sessionId is null)
            return;

        _traceHub.PublishNewEntries(_runner.Engine, _flow.FlowId, _flow.Title, _sessionId, ref _publishedCount);
    }

    private static long ToLong(object? value) => value switch
    {
        long l => l,
        int i => i,
        string s when long.TryParse(s, out var x) => x,
        _ => 0
    };
}
