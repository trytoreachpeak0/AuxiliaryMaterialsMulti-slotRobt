using WireCabinet.Data;
using WireCabinet.Flows;
using WireCabinet.Hmi;
using WireCabinet.Hmi.Views;

namespace WireCabinet.Hmi.Services;

public sealed class FlowCoordinator
{
    private readonly AppBootstrap _app;
    private FlowSessionRunner? _runner;
    private FlowDefinition? _flow;

    private static readonly HashSet<string> AutoAckUserInputNodes = new(StringComparer.OrdinalIgnoreCase)
    {
        "showWireReturnSuccessMessage",
        "showLoadWireAndCloseDoorMessage",
        "loadReturnedWire",
        "showWireIssueSuccessMessage",
        "showUnloadWireAndCloseDoorMessage",
        "unloadWire"
    };

    public FlowCoordinator(AppBootstrap app) => _app = app;

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
        message = "";
        return true;
    }

    public void EndFlow()
    {
        _runner = null;
        _flow = null;
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
        if (_runner.PauseReason == FlowPauseReason.Error)
            return (false, PickFlowErrorMessage(_runner.PauseMessage, entry?.Result, "流程错误"), FlowPauseReason.Error);

        if (_runner.PauseReason == FlowPauseReason.Terminal)
        {
            var lastNode = _runner.Engine.Trace.LastOrDefault();
            var isFail = string.Equals(lastNode?.Outcome, "fail", StringComparison.OrdinalIgnoreCase)
                || string.Equals(entry?.Outcome, "fail", StringComparison.OrdinalIgnoreCase);
            var msg = isFail
                ? MapTerminalFailMessage(lastNode?.NodeId)
                : (_runner.PauseMessage ?? entry?.Result ?? "流程结束");
            if (endFlowOnTerminal)
                EndFlow();
            if (isFail)
                return (false, msg, FlowPauseReason.Error);
            return (true, msg, FlowPauseReason.Terminal);
        }

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

    /// <summary>步骤①：校验操作员后暂停在归还批号输入。</summary>
    public (bool Ok, string Message, FlowPauseReason Reason) AdvanceOpOperatorValidate()
    {
        var result = AdvanceUntilPause(endFlowOnTerminal: false);
        if (!result.Ok)
            return result;
        if (result.Reason == FlowPauseReason.UserInput
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

    /// <summary>步骤③：机台校验后暂停在剩余芯片输入。</summary>
    public (bool Ok, string Message, FlowPauseReason Reason) AdvanceOpEqpValidate(string eqpNo)
    {
        FillOpEqpNo(eqpNo);
        var result = AdvanceUntilPause(pauseBeforeNodeId: "inputRemainingQty", endFlowOnTerminal: false);
        if (!result.Ok)
            return result;
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
        if (!result.Ok)
            return result;

        if (string.Equals(CurrentNodeId, "showRemainingQtyWrongHint", StringComparison.OrdinalIgnoreCase)
            || (result.Reason == FlowPauseReason.UserInput
                && string.Equals(CurrentNodeId, "inputRemainingQty", StringComparison.OrdinalIgnoreCase)))
            return (false, "剩余待焊芯片数量不正确（剩余产量不能大于待完工产量），请重新输入。", FlowPauseReason.Error);

        if (result.Reason == FlowPauseReason.AwaitingAction
            && string.Equals(CurrentNodeId, "submitWireReturn", StringComparison.OrdinalIgnoreCase))
            return (true, "用量校验通过。", result.Reason);

        EndFlow();
        return (false, MapOpFlowMessage(), FlowPauseReason.Error);
    }

    /// <summary>步骤④：MES 归还提交并打开归还格口，暂停在关门。</summary>
    public (bool Ok, string Message, FlowPauseReason Reason) AdvanceOpSubmitReturnToDoorClose()
    {
        var result = AdvanceUntilPauseAutoAck(pauseBeforeDoorClose: true, endFlowOnTerminal: false);
        if (!result.Ok)
            return result;
        if (result.Reason == FlowPauseReason.AwaitingDoorClose)
            return (true, "请放入归还焊丝并关闭格口。", result.Reason);
        EndFlow();
        return (false, MapOpFlowMessage(), FlowPauseReason.Error);
    }

    /// <summary>归还格口已关：写库并推进到领用提交前。</summary>
    public (bool Ok, string Message, FlowPauseReason Reason) AdvanceOpAfterReturnDoorClosed()
    {
        var result = AdvanceUntilPauseAutoAck(pauseBeforeNodeId: "submitWireIssue", endFlowOnTerminal: false);
        if (!result.Ok)
            return result;
        if (result.Reason == FlowPauseReason.AwaitingAction
            && string.Equals(CurrentNodeId, "submitWireIssue", StringComparison.OrdinalIgnoreCase))
            return (true, "归还已提交。请点「提交领用焊丝」。", result.Reason);
        EndFlow();
        return (false, MapOpFlowMessage(), FlowPauseReason.Error);
    }

    /// <summary>步骤⑤：MES 领用提交并打开领用格口，暂停在关门。</summary>
    public (bool Ok, string Message, FlowPauseReason Reason) AdvanceOpSubmitIssueToDoorClose()
    {
        var result = AdvanceUntilPauseAutoAck(pauseBeforeDoorClose: true, endFlowOnTerminal: false);
        if (!result.Ok)
            return result;
        if (result.Reason == FlowPauseReason.AwaitingDoorClose)
            return (true, "请取出可用焊丝并关闭格口。", result.Reason);
        EndFlow();
        return (false, MapOpFlowMessage(), FlowPauseReason.Error);
    }

    /// <summary>领用格口已关：完成流程。</summary>
    public (bool Ok, string Message, FlowPauseReason Reason) AdvanceOpAfterIssueDoorClosed()
    {
        var result = AdvanceUntilPauseAutoAck(endFlowOnTerminal: true);
        if (result.Reason == FlowPauseReason.Terminal && result.Ok)
            return (true, "领用已提交，流程完成。", result.Reason);
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
            return (true, result.Message, FlowPauseReason.Terminal);
        }
        if (!result.Ok)
            return (false, result.Message, FlowPauseReason.Error);
        return (false, result.Message, result.Reason);
    }

    private string MapTerminalFailMessage(string? nodeId) => nodeId switch
    {
        "showOPIdNotExistsMessage" => "不存在该操作人员",
        "showWireLotNoInputInvalidOrNotExistsMessage" => "焊丝批号无效或不存在",
        "showWireSpecNotExistsMessage" => "焊丝规格不存在",
        "showNoMatchedWireMessage" => "柜内无匹配规格的可用焊丝",
        "showReturnedWeightNotExists" => "未配置归还重量，请转人工归还",
        "showEqpNoNotExistsMessage" => "机台号不存在",
        "showLastProductLotNoNotExistsMessage" => "未查询到最近产品批号",
        "showWireReturnFailMessage" => ResolveSubmitFailMessage("submitWireReturn", "归还提交失败"),
        "showNoReturnSlotAvailableMessage" => "无空闲归还格口，请转人工归还",
        "showWireIssueFailMessage" => ResolveSubmitFailMessage("submitWireIssue", "领用提交失败"),
        "showNoAvailableSlotMessage" => "无空闲格口",
        "showWireLotNoAlreadyInCabinetMessage" => BuildAlreadyInCabinetMessage(),
        _ => _runner?.PauseMessage ?? "流程未通过。"
    };

    private string ResolveSubmitFailMessage(string submitNodeId, string fallback)
    {
        var pause = PickFlowErrorMessage(_runner?.PauseMessage, null, "");
        if (!string.IsNullOrWhiteSpace(pause))
            return pause;

        var submitEntry = _runner?.Engine.Trace.LastOrDefault(t =>
            string.Equals(t.NodeId, submitNodeId, StringComparison.OrdinalIgnoreCase));
        var fromTrace = PickFlowErrorMessage(submitEntry?.Result, null, "");
        if (!string.IsNullOrWhiteSpace(fromTrace))
            return fromTrace;

        var submitResult = GetField($"{submitNodeId}.{MatTransResult.SubmitResultField}")?.ToString();
        if (!string.IsNullOrWhiteSpace(submitResult) && !MatTransResult.IsSuccess(submitResult))
            return submitResult;

        return fallback;
    }

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

    private static long ToLong(object? value) => value switch
    {
        long l => l,
        int i => i,
        string s when long.TryParse(s, out var x) => x,
        _ => 0
    };
}
