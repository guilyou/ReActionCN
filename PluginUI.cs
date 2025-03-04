using System;
using System.Linq;
using System.Numerics;
using Dalamud.Interface;
using Dalamud.Interface.Utility;
using FFXIVClientStructs.FFXIV.Client.UI.Misc;
using Hypostasis.Game.Structures;
using ImGuiNET;
using Lumina.Excel.Sheets;
using Action = Lumina.Excel.Sheets.Action;

namespace ReAction;

public static class PluginUI
{
    private static bool isVisible = false;
    private static int selectedStack = -1;
    private static int hotbar = 0;
    private static int hotbarSlot = 0;
    private static int commandType = 1;
    private static uint commandID = 0;

    public static bool IsVisible
    {
        get => isVisible;
        set => isVisible = value;
    }

    private static Configuration.ActionStack CurrentStack => 0 <= selectedStack && selectedStack < ReAction.Config.ActionStacks.Count ? ReAction.Config.ActionStacks[selectedStack] : null;

    public static void Draw()
    {
        if (!isVisible) return;

        ImGui.SetNextWindowSizeConstraints(new Vector2(700, 600) * ImGuiHelpers.GlobalScale, new Vector2(9999));
        ImGui.Begin("ReAction配置", ref isVisible);
        ImGuiEx.AddDonationHeader();

        if (ImGui.BeginTabBar("ReActionTabs"))
        {
            if (ImGui.BeginTabItem("预设"))
            {
                DrawStackList();
                ImGui.EndTabItem();
            }

            if (ImGui.BeginTabItem("更多设置"))
            {
                ImGui.BeginChild("OtherSettings");
                DrawOtherSettings();
                ImGui.EndChild();
                ImGui.EndTabItem();
            }

            if (ImGui.BeginTabItem("自定义占位符"))
            {
                DrawCustomPlaceholders();
                ImGui.EndTabItem();
            }

            if (ImGui.BeginTabItem("帮助"))
            {
                DrawStackHelp();
                ImGui.EndTabItem();
            }

            ImGui.EndTabBar();
        }

        ImGui.End();
    }

    private static void DrawStackList()
    {
        var currentStack = CurrentStack;
        var hasSelectedStack = currentStack != null;

        ImGui.PushFont(UiBuilder.IconFont);

        var buttonSize = ImGui.CalcTextSize(FontAwesomeIcon.SignOutAlt.ToIconString()) + ImGui.GetStyle().FramePadding * 2;

        if (ImGui.Button(FontAwesomeIcon.Plus.ToIconString(), buttonSize))
        {
            ReAction.Config.ActionStacks.Add(new() { Name = "新预设" });
            ReAction.Config.Save();
        }

        ImGui.SameLine();

        if (ImGui.Button(FontAwesomeIcon.SignOutAlt.ToIconString(), buttonSize) && hasSelectedStack)
            ImGui.SetClipboardText(Configuration.ExportActionStack(CurrentStack));
        ImGui.PopFont();
        ImGuiEx.SetItemTooltip("将预设导出到剪贴板");
        ImGui.PushFont(UiBuilder.IconFont);

        ImGui.SameLine();

        if (ImGui.Button(FontAwesomeIcon.SignInAlt.ToIconString(), buttonSize))
        {
            try
            {
                var stack = Configuration.ImportActionStack(ImGui.GetClipboardText());
                ReAction.Config.ActionStacks.Add(stack);
                ReAction.Config.Save();
            }
            catch (Exception e)
            {
                DalamudApi.PrintError($"从剪贴板导入预设失败!\n{e.Message}");
            }
        }
        ImGui.PopFont();
        ImGuiEx.SetItemTooltip("从剪贴板导入预设");
        ImGui.PushFont(UiBuilder.IconFont);

        ImGui.SameLine();

        if (ImGui.Button(FontAwesomeIcon.ArrowUp.ToIconString(), buttonSize) && hasSelectedStack)
        {
            var preset = CurrentStack;
            ReAction.Config.ActionStacks.RemoveAt(selectedStack);

            selectedStack = Math.Max(selectedStack - 1, 0);

            ReAction.Config.ActionStacks.Insert(selectedStack, preset);
            ReAction.Config.Save();
        }

        ImGui.SameLine();

        if (ImGui.Button(FontAwesomeIcon.ArrowDown.ToIconString(), buttonSize) && hasSelectedStack)
        {
            var preset = CurrentStack;
            ReAction.Config.ActionStacks.RemoveAt(selectedStack);

            selectedStack = Math.Min(selectedStack + 1, ReAction.Config.ActionStacks.Count);

            ReAction.Config.ActionStacks.Insert(selectedStack, preset);
            ReAction.Config.Save();
        }

        ImGui.PopFont();

        ImGui.SameLine();

        if (ImGuiEx.DeleteConfirmationButton(buttonSize) && hasSelectedStack)
        {
            ReAction.Config.ActionStacks.RemoveAt(selectedStack);
            selectedStack = Math.Min(selectedStack, ReAction.Config.ActionStacks.Count - 1);
            currentStack = CurrentStack;
            hasSelectedStack = currentStack != null;
            ReAction.Config.Save();
        }

        var firstColumnWidth = 250 * ImGuiHelpers.GlobalScale;
        ImGui.PushStyleColor(ImGuiCol.Border, ImGui.GetColorU32(ImGuiCol.TabActive));
        ImGui.BeginChild("ReActionPresetList", new Vector2(firstColumnWidth, ImGui.GetContentRegionAvail().Y / 2), true);
        ImGui.PopStyleColor();

        for (int i = 0; i < ReAction.Config.ActionStacks.Count; i++)
        {
            ImGui.PushID(i);

            var preset = ReAction.Config.ActionStacks[i];

            if (ImGui.Selectable(preset.Name, selectedStack == i))
                selectedStack = i;

            ImGui.PopID();
        }

        ImGui.EndChild();

        if (!hasSelectedStack) return;

        var lastCursorPos = ImGui.GetCursorPos();
        ImGui.SameLine();
        var nextLineCursorPos = ImGui.GetCursorPos();
        ImGui.SetCursorPos(lastCursorPos);

        ImGui.BeginChild("ReActionStackEditorMain", new Vector2(firstColumnWidth, ImGui.GetContentRegionAvail().Y), true);
        DrawStackEditorMain(currentStack);
        ImGui.EndChild();

        ImGui.SetCursorPos(nextLineCursorPos);
        ImGui.BeginChild("ReActionStackEditorLists", ImGui.GetContentRegionAvail(), false);
        DrawStackEditorLists(currentStack);
        ImGui.EndChild();
    }

    private static void DrawStackEditorMain(Configuration.ActionStack stack)
    {
        var save = false;

        save |= ImGui.InputText("名称", ref stack.Name, 64);
        save |= ImGui.CheckboxFlags("##Shift", ref stack.ModifierKeys, 1);
        ImGuiEx.SetItemTooltip("Shift");
        ImGui.SameLine();
        save |= ImGui.CheckboxFlags("##Ctrl", ref stack.ModifierKeys, 2);
        ImGuiEx.SetItemTooltip("Control");
        ImGui.SameLine();
        save |= ImGui.CheckboxFlags("##Alt", ref stack.ModifierKeys, 4);
        ImGuiEx.SetItemTooltip("Alt");
        ImGui.SameLine();
        save |= ImGui.CheckboxFlags("##Exact", ref stack.ModifierKeys, 8);
        ImGuiEx.SetItemTooltip("精确匹配这些修饰键位，例如:Shift + Control不会触发Shift + Control + Alt的");
        ImGui.SameLine();
        ImGui.TextUnformatted("修饰键位");
        save |= ImGui.Checkbox("如果重定向失败，阻止原技能", ref stack.BlockOriginal);
        save |= ImGui.Checkbox("如果超出范围,则判定为失败", ref stack.CheckRange);
        save |= ImGui.Checkbox("如果技能在冷却,则判定为失败", ref stack.CheckCooldown);
        ImGuiEx.SetItemTooltip("如果操作因冷却而无法排队，则判定为失败。包括:" +
            "\n冷却时间> 0.5s 或者距离上次用< 0.5s(充能 / GCD).");

        if (save)
            ReAction.Config.Save();
    }

    private static void DrawStackEditorLists(Configuration.ActionStack stack)
    {
        DrawActionEditor(stack);
        DrawItemEditor(stack);
    }

    private static string FormatActionRow(Action a) => a.RowId switch
    {
        0 => "所有技能",
        1 => "所有伤害技能",
        2 => "所有增益技能",
        _ => $"[#{a.RowId} {a.ClassJob.ValueNullable?.Abbreviation}{(a.IsPvP ? " PVP" : string.Empty)}] {a.Name}"
    };

    private static readonly ImGuiEx.ExcelSheetComboOptions<Action> actionComboOptions = new()
    {
        FormatRow = FormatActionRow,
        FilteredSheet = DalamudApi.DataManager.GetExcelSheet<Action>().Take(3).Concat(ReAction.actionSheet.Select(kv => kv.Value))
    };

    private static readonly ImGuiEx.ExcelSheetPopupOptions<Action> actionPopupOptions = new()
    {
        FormatRow = FormatActionRow,
        FilteredSheet = actionComboOptions.FilteredSheet
    };

    private static void DrawActionEditor(Configuration.ActionStack stack)
    {
        var contentRegion = ImGui.GetContentRegionAvail();
        ImGui.BeginChild("ReActionActionEditor", contentRegion with { Y = contentRegion.Y / 2 }, true);

        var buttonWidth = ImGui.GetContentRegionAvail().X / 2;
        var buttonIndent = 0f;
        for (int i = 0; i < stack.Actions.Count; i++)
        {
            using var _ = ImGuiEx.IDBlock.Begin(i);
            var action = stack.Actions[i];

            ImGui.Button("≡");
            if (ImGuiEx.IsItemDraggedDelta(action, ImGuiMouseButton.Left, ImGui.GetFrameHeightWithSpacing(), false, out var dt) && dt.Y != 0)
                stack.Actions.Shift(i, dt.Y);

            if (i == 0)
                buttonIndent = ImGui.GetItemRectSize().X + ImGui.GetStyle().ItemSpacing.X;

            ImGui.SameLine();

            ImGui.SetNextItemWidth(buttonWidth);
            if (ImGuiEx.ExcelSheetCombo("##Action", ref action.ID, actionComboOptions))
                ReAction.Config.Save();

            ImGui.SameLine();

            if (ImGui.Checkbox("调整技能ID", ref action.UseAdjustedID))
                ReAction.Config.Save();
            var detectedAdjustment = false;
            unsafe
            {
                if (!action.UseAdjustedID && (detectedAdjustment = Common.ActionManager->CS.GetAdjustedActionId(action.ID) != action.ID))
                    ImGui.GetWindowDrawList().AddRectFilled(ImGui.GetItemRectMin(), ImGui.GetItemRectMax(), 0x2000FF30, ImGui.GetStyle().FrameRounding);
            }
            ImGuiEx.SetItemTooltip("允许该技能与它转换为的任何其他技能相匹配。" +
                "\n例如疾风会匹配天辉,出卡会匹配所有的卡,诊断会匹配均衡诊断等." +
                "\n启用此项对会升级的技能好用,但如果你有XIVCombos等连击插件,要禁用这个." +
                (detectedAdjustment ? "\n\n这个技能目前因为职业特性/连击/其他插件而被转换了,推荐你勾选这个." : string.Empty));

            ImGui.SameLine();

            if (!ImGuiEx.DeleteConfirmationButton()) continue;
            stack.Actions.RemoveAt(i);
            ReAction.Config.Save();
        }

        using (ImGuiEx.IndentBlock.Begin(buttonIndent))
        {
            ImGuiEx.FontButton(FontAwesomeIcon.Plus.ToIconString(), UiBuilder.IconFont, new Vector2(buttonWidth, 0));
            if (ImGuiEx.ExcelSheetPopup("ReActionAddSkillsPopup", out var row, actionPopupOptions))
            {
                stack.Actions.Add(new() { ID = row });
                ReAction.Config.Save();
            }
        }

        ImGui.EndChild();
    }

    private static string FormatOverrideActionRow(Action a) => a.RowId switch
    {
        0 => "同个技能",
        _ => $"[#{a.RowId} {a.ClassJob.ValueNullable?.Abbreviation}{(a.IsPvP ? " PVP" : string.Empty)}] {a.Name}"
    };

    private static readonly ImGuiEx.ExcelSheetComboOptions<Action> actionOverrideComboOptions = new()
    {
        FormatRow = FormatOverrideActionRow,
        FilteredSheet = DalamudApi.DataManager.GetExcelSheet<Action>().Take(1).Concat(ReAction.actionSheet.Select(kv => kv.Value))
    };

    private static void DrawItemEditor(Configuration.ActionStack stack)
    {
        ImGui.BeginChild("ReActionItemEditor", ImGui.GetContentRegionAvail(), true);

        var buttonWidth = ImGui.GetContentRegionAvail().X / 3;
        var buttonIndent = 0f;
        for (int i = 0; i < stack.Items.Count; i++)
        {
            using var _ = ImGuiEx.IDBlock.Begin(i);
            var item = stack.Items[i];

            ImGui.Button("≡");
            if (ImGuiEx.IsItemDraggedDelta(item, ImGuiMouseButton.Left, ImGui.GetFrameHeightWithSpacing(), false, out var dt) && dt.Y != 0)
                stack.Items.Shift(i, dt.Y);

            if (i == 0)
                buttonIndent = ImGui.GetItemRectSize().X + ImGui.GetStyle().ItemSpacing.X;

            ImGui.SameLine();

            ImGui.SetNextItemWidth(buttonWidth);
            if (DrawTargetTypeCombo("##TargetType", ref item.TargetID))
                ReAction.Config.Save();

            ImGui.SameLine();

            ImGui.SetNextItemWidth(buttonWidth);
            if (ImGuiEx.ExcelSheetCombo("##ActionOverride", ref item.ID, actionOverrideComboOptions))
                ReAction.Config.Save();

            ImGui.SameLine();

            if (!ImGuiEx.DeleteConfirmationButton()) continue;
            stack.Items.RemoveAt(i);
            ReAction.Config.Save();
        }

        using (ImGuiEx.IndentBlock.Begin(buttonIndent))
        {
            if (ImGuiEx.FontButton(FontAwesomeIcon.Plus.ToIconString(), UiBuilder.IconFont, new Vector2(buttonWidth, 0)))
            {
                stack.Items.Add(new());
                ReAction.Config.Save();
            }
        }

        ImGui.EndChild();
    }

    private static bool DrawTargetTypeCombo(string label, ref uint currentSelection)
    {
        if (!ImGui.BeginCombo(label, PronounManager.GetPronounName(currentSelection))) return false;

        var ret = false;
        foreach (var id in PronounManager.OrderedIDs)
        {
            if (!ImGui.Selectable(PronounManager.GetPronounName(id), id == currentSelection)) continue;
            currentSelection = id;
            ret = true;
            break;
        }

        ImGui.EndCombo();
        return ret;
    }




    private static void DrawOtherSettings()
    {
        var save = false;

        if (ImGuiEx.BeginGroupBox("技能", 0.5f))
        {
            save |= ImGui.Checkbox("按住按键连发", ref ReAction.Config.EnableTurboHotbars);
            ImGuiEx.SetItemTooltip("允许您绑定热键（不支持控制器）。\n警告：文本宏可能会被滥用。");

            using (ImGuiEx.DisabledBlock.Begin(!ReAction.Config.EnableTurboHotbars))
            {
                ImGuiEx.Prefix(false);
                save |= ImGui.DragInt("频率", ref ReAction.Config.TurboHotbarInterval, 0.5f, 0, 1000, "%d ms");

                ImGuiEx.Prefix(false);
                save |= ImGui.DragInt("启动延迟", ref ReAction.Config.InitialTurboHotbarInterval, 0.5f, 0, 1000, "%d ms");

                ImGuiEx.Prefix(true);
                save |= ImGui.Checkbox("脱战时也生效#Turbo", ref ReAction.Config.EnableTurboHotbarsOutOfCombat);
            }

            save |= ImGui.Checkbox("地面目标施法", ref ReAction.Config.EnableInstantGroundTarget);
            ImGuiEx.SetItemTooltip("当预设中没有重定向的目标时，地面目标将立即将自己放置在当前光标位置。");

            using (ImGuiEx.DisabledBlock.Begin(!ReAction.Config.EnableInstantGroundTarget))
            {
                ImGuiEx.Prefix(true);
                save |= ImGui.Checkbox("阻止特殊地面目标技能", ref ReAction.Config.EnableBlockMiscInstantGroundTargets);
                ImGuiEx.SetItemTooltip("禁止在放置宠物等操作时激活上一个选项。");
            }

            save |= ImGui.Checkbox("智能咏唱技能面向", ref ReAction.Config.EnableEnhancedAutoFaceTarget);
            ImGuiEx.SetItemTooltip("不需要面对目标的动作将不再自动面对目标，例如治疗。");

            save |= ImGui.Checkbox("智能范围技能面向", ref ReAction.Config.EnableCameraRelativeDirectionals);
            ImGuiEx.SetItemTooltip("将直线型和方向型的技能(例如武装戍卫和穿甲散弹)改为摄像机方向施法而不是角色面向方向.");

            save |= ImGui.Checkbox("智能位移技能面向", ref ReAction.Config.EnableCameraRelativeDashes);
            ImGuiEx.SetItemTooltip("将各种位移技能(例如前冲步和回避跳跃)改为摄像机方向施法而不是角色面向方向.");

            using (ImGuiEx.DisabledBlock.Begin(!ReAction.Config.EnableCameraRelativeDashes))
            {
                ImGuiEx.Prefix(true);
                save |= ImGui.Checkbox("阻止后跳", ref ReAction.Config.EnableNormalBackwardDashes);
                ImGuiEx.SetItemTooltip("禁止上述特性对后跳类技能生效,例如回避跳跃.");
            }

            ImGuiEx.EndGroupBox();
        }

        ImGui.SameLine();

        if (ImGuiEx.BeginGroupBox("自动化", 0.5f))
        {
            save |= ImGui.Checkbox("自动下车", ref ReAction.Config.EnableAutoDismount);
            ImGuiEx.SetItemTooltip("Automatically dismounts when an action is used, prior to using the action.");

            save |= ImGui.Checkbox("自动中断读条", ref ReAction.Config.EnableAutoCastCancel);
            ImGuiEx.SetItemTooltip("Automatically cancels casting when the target dies.");

            save |= ImGui.Checkbox("自动选中敌人", ref ReAction.Config.EnableAutoTarget);
            ImGuiEx.SetItemTooltip("当没有指定针对性攻击的目标时，自动瞄准最近的敌人。");

            using (ImGuiEx.DisabledBlock.Begin(!ReAction.Config.EnableAutoTarget))
            {
                ImGuiEx.Prefix(true);
                save |= ImGui.Checkbox("自动改变选中的敌人", ref ReAction.Config.EnableAutoChangeTarget);
                ImGuiEx.SetItemTooltip("当您的主要目标不适合进行有针对性的攻击时，还会额外瞄准最近的敌人。");
            }

            var _ = ReAction.Config.AutoFocusTargetID != 0;
            if (ImGui.Checkbox("自动焦点目标", ref _))
            {
                ReAction.Config.AutoFocusTargetID = _ ? PronounManager.OrderedIDs.First() : 0;
                save = true;
            }
            ImGuiEx.SetItemTooltip("在可能的情况下自动将焦点目标设置为选定的目标类型。");

            using (ImGuiEx.DisabledBlock.Begin(!_))
            {
                ImGuiEx.Prefix(false);
                save |= DrawTargetTypeCombo("##AutoFocusTargetID", ref ReAction.Config.AutoFocusTargetID);

                ImGuiEx.Prefix(true);
                save |= ImGui.Checkbox("脱战时也生效##AutoFocusTarget", ref ReAction.Config.EnableAutoFocusTargetOutOfCombat);
            }

            save |= ImGui.Checkbox("自动重选焦点目标", ref ReAction.Config.EnableAutoRefocusTarget);
            ImGuiEx.SetItemTooltip("执行任务时，若焦点目标丢失，则尝试将焦点转移到先前所关注的任何目标。");

            save |= ImGui.Checkbox("启用法师自动攻击", ref ReAction.Config.EnableSpellAutoAttacks);
            ImGuiEx.SetItemTooltip("会导致魔法（以及一些其他动作）开始像战技一样使用自动攻击。");

            using (ImGuiEx.DisabledBlock.Begin(!ReAction.Config.EnableSpellAutoAttacks))
            {
                ImGuiEx.Prefix(true);
                if (ImGui.Checkbox("脱战时也生效##SpellAutos", ref ReAction.Config.EnableSpellAutoAttacksOutOfCombat))
                {
                    if (ReAction.Config.EnableSpellAutoAttacksOutOfCombat)
                        Game.spellAutoAttackPatch.Enable();
                    else
                        Game.spellAutoAttackPatch.Disable();
                    save = true;
                }
                ImGuiEx.SetItemTooltip("警告：这可能会在面对某些Boss时抢开！");
            }

            ImGuiEx.EndGroupBox();
        }

        if (ImGuiEx.BeginGroupBox("队列", 0.5f))
        {
            if (ImGui.Checkbox("地面目标技能进入队列", ref ReAction.Config.EnableGroundTargetQueuing))
            {
                Game.queueGroundTargetsPatch.Toggle();
                save = true;
            }
            ImGuiEx.SetItemTooltip("地面目标将插入到动作队列中，\n导致它们尽快被使用，就像其他即时技能一样。");

            save |= ImGui.Checkbox("允许特殊技能进入队列", ref ReAction.Config.EnableQueuingMore);
            ImGuiEx.SetItemTooltip("允许冲刺/使用道具/极限技进入队列");

            save |= ImGui.Checkbox("允许宏进入队列", ref ReAction.Config.EnableMacroQueue);
            ImGuiEx.SetItemTooltip("所有宏都将表现得好像使用过 /macroqueue 。");

            save |= ImGui.Checkbox("允许队列时间调整 (BETA)", ref ReAction.Config.EnableQueueAdjustments);
            ImGuiEx.SetItemTooltip("更改游戏处理动作排队的机制。\n这是一个 Beta 功能，如果您发现任何不按预期运行的问题，请告诉我。");

            using (ImGuiEx.DisabledBlock.Begin(!ReAction.Config.EnableQueueAdjustments))
            using (ImGuiEx.ItemWidthBlock.Begin(ImGui.CalcItemWidth() / 2)) 
            {
                ImGuiEx.Prefix(false);
                save |= ImGui.Checkbox("##目前的GCD", ref ReAction.Config.EnableGCDAdjustedQueueThreshold);
                ImGuiEx.SetItemTooltip("根据当前的GCD调整阈值。");

                ImGui.SameLine();
                save |= ImGui.SliderFloat("队列阈值", ref ReAction.Config.QueueThreshold, 0.1f, 2.5f, "%.1f");
                ImGuiEx.SetItemTooltip("动作冷却时间剩余以允许游戏在按下时提前排队下一个。默认值：0.5。" +
                    (ReAction.Config.EnableGCDAdjustedQueueThreshold ? $"\nGCD Adjusted Threshold: {ReAction.Config.QueueThreshold * ActionManager.GCDRecast / 2500f}" : string.Empty));

                ImGui.BeginGroup();
                ImGuiEx.Prefix(false);
                save |= ImGui.Checkbox("##Enable Requeuing", ref ReAction.Config.EnableRequeuing);
                using (ImGuiEx.DisabledBlock.Begin(!ReAction.Config.EnableRequeuing)) {
                    ImGui.SameLine();
                    save |= ImGui.SliderFloat("队列锁定阈值", ref ReAction.Config.QueueLockThreshold, 0.1f, 2.5f, "%.1f");
                }
                ImGui.EndGroup();
                ImGuiEx.SetItemTooltip("启用后，允许在排队动作冷却时间低于此值之前继续排队。");

                ImGuiEx.Prefix(false);
                save |= ImGui.SliderFloat("技能锁定", ref ReAction.Config.QueueActionLockout, 0, 2.5f, "%.1f");
                ImGuiEx.SetItemTooltip("如果该动作的冷却时间不足此值，则阻止再次排队该动作。");

                ImGuiEx.Prefix(true);
                save |= ImGui.Checkbox("启用GCD滑步插入", ref ReAction.Config.EnableSlidecastQueuing);
                ImGuiEx.SetItemTooltip("允许在最后一个 0.5 秒的GCD 施放期间排队下一个GCD。");
            }

            ImGuiEx.EndGroupBox();
        }

        ImGui.SameLine();

        if (ImGuiEx.BeginGroupBox("拆分", 0.5f))
        {
            save |= ImGui.Checkbox("拆分火炎之红", ref ReAction.Config.EnableDecomboFireInRed);
            ImGuiEx.SetItemTooltip("Removes the Fire in Red combo. You will need to use the hotbar\nfeature below to place it on your hotbar in order to use it again.\nAero in Green ID: 34651\nWater in Blue ID: 34652");

            save |= ImGui.Checkbox("拆分烈炎之红", ref ReAction.Config.EnableDecomboFire2InRed);
            ImGuiEx.SetItemTooltip("Removes the Fire II in Red combo. You will need to use the hotbar\nfeature below to place it on your hotbar in order to use it again.\nAero II in Green ID: 34657\nWater II in Blue ID: 34658");

            save |= ImGui.Checkbox("拆分冰结之蓝青", ref ReAction.Config.EnableDecomboBlizzardInCyan);
            ImGuiEx.SetItemTooltip("Removes the Blizzard in Cyan combo. You will need to use the hotbar\nfeature below to place it on your hotbar in order to use it again.\nStone in Yellow ID: 34654\nThunder in Magenta ID: 34655");

            save |= ImGui.Checkbox("拆分冰冻之蓝青", ref ReAction.Config.EnableDecomboBlizzard2InCyan);
            ImGuiEx.SetItemTooltip("Removes the Blizzard II in Cyan combo. You will need to use the hotbar\nfeature below to place it on your hotbar in order to use it again.\nStone II in Yellow ID: 34660\nThunder II in Magenta ID: 34661");

            save |= ImGui.Checkbox("拆分礼仪之铃", ref ReAction.Config.EnableDecomboLiturgy);
            ImGuiEx.SetItemTooltip("Removes the Liturgy of the Bell combo. You will need to use the hotbar\nfeature below to place it on your hotbar in order to use it again.\nLiturgy of the Bell (Detonate) ID: 28509");

            save |= ImGui.Checkbox("拆分地星", ref ReAction.Config.EnableDecomboEarthlyStar);
            ImGuiEx.SetItemTooltip("Removes the Earthly Star combo. You will need to use the hotbar\nfeature below to place it on your hotbar in order to use it again.\nStellar Detonation ID: 8324");

            ImGuiEx.EndGroupBox();
        }

        if (ImGuiEx.BeginGroupBox("杂项", 0.5f))
        {
            save |= ImGui.Checkbox("帧率对齐", ref ReAction.Config.EnableFrameAlignment);
            ImGuiEx.SetItemTooltip("Aligns the game's frames with the GCD and animation lock.\nNote: This option will cause an almost unnoticeable stutter when either of these timers ends.");

            if (ImGui.Checkbox("宏小数等待 (分数)", ref ReAction.Config.EnableFractionality)) {
                Game.waitSyntaxDecimalPatch.Toggle();
                Game.waitCommandDecimalPatch.Toggle();
                save = true;
            }
            ImGuiEx.SetItemTooltip("Allows decimals in wait commands and removes the 60 seconds cap (e.g. <wait.0.5> or /wait 0.5).");

            if (ImGui.Checkbox("在宏中启用原本不能用的技能", ref ReAction.Config.EnableUnassignableActions)) {
                Game.allowUnassignableActionsPatch.Toggle();
                save = true;
            }
            ImGuiEx.SetItemTooltip("Allows using normally unavailable actions in \"/ac\", such as The Forbidden Chakra or Stellar Detonation.");

            save |= ImGui.Checkbox("在宏中启用玩家名称", ref ReAction.Config.EnablePlayerNamesInCommands);
            ImGuiEx.SetItemTooltip("Allows using the \"First Last@World\" syntax for any command requiring a target.");

            ImGuiEx.EndGroupBox();
        }

        ImGui.SameLine();

        if (ImGuiEx.BeginGroupBox("热键栏功能 (将鼠标悬停于此以获取信息)", 0.5f, new ImGuiEx.GroupBoxOptions
        {
            HeaderTextAction = () => ImGuiEx.SetItemTooltip(
                "这样你就可以将各种通常无法放置在热键栏上的东西放置到热键栏上。" +
                "\n如果你不知道这有什么用途，就不要碰它。" +
                "\n以下是您可以采取的一些措施的示例：" +
                "\n\t将特定操作放在快捷栏上，以便与“拆分”功能之一一起使用。ID 在每个设置的工具提示中。" +
                "\n\t将特定的打瞌睡和坐下表情放在快捷栏上 (Emote, 88 and 95)." +
                "\n\t将货币（物品，1-99）放置在快捷栏上，无需打开货币菜单即可查看您有多少货币。" +
                "\n\t复活飞行坐骑轮盘 (GeneralAction, 24).")
        })) {
            ImGui.Combo("栏序", ref hotbar, "1\02\03\04\05\06\07\08\09\010\0XHB 1\0XHB 2\0XHB 3\0XHB 4\0XHB 5\0XHB 6\0XHB 7\0XHB 8");
            ImGui.Combo("格序", ref hotbarSlot, "1\02\03\04\05\06\07\08\09\010\011\012\013\014\015\016");
            var hotbarSlotType = Enum.GetName(typeof(RaptureHotbarModule.HotbarSlotType), commandType) ?? commandType.ToString();
            if (ImGui.BeginCombo("类型", hotbarSlotType)) {
                for (int i = 1; i <= 32; i++) {
                    if (!ImGui.Selectable($"{Enum.GetName(typeof(RaptureHotbarModule.HotbarSlotType), i) ?? i.ToString()}##{i}", commandType == i)) continue;
                    commandType = i;
                }
                ImGui.EndCombo();
            }

            DrawHotbarIDInput((RaptureHotbarModule.HotbarSlotType)commandType);

            if (ImGui.Button("执行"))
                Game.SetHotbarSlot(hotbar, hotbarSlot, (byte)commandType, commandID);
            ImGuiEx.EndGroupBox();
        }

        if (save)
            ReAction.Config.Save();
    }

    public static void DrawHotbarIDInput(RaptureHotbarModule.HotbarSlotType slotType)
    {
        switch ((RaptureHotbarModule.HotbarSlotType)commandType)
        {
            case RaptureHotbarModule.HotbarSlotType.Action:
                ImGuiEx.ExcelSheetCombo($"ID##{commandType}", ref commandID, new ImGuiEx.ExcelSheetComboOptions<Action> { FormatRow = r => $"[#{r.RowId}] {r.Name}" });
                break;
            case RaptureHotbarModule.HotbarSlotType.Item:
                const int hqID = 1_000_000;
                var _ = commandID >= hqID ? commandID - hqID : commandID;
                if (ImGuiEx.ExcelSheetCombo($"ID##{commandType}", ref _, new ImGuiEx.ExcelSheetComboOptions<Item> { FormatRow = r => $"[#{r.RowId}] {r.Name}" }))
                    commandID = commandID >= hqID ? _ + hqID : _;
                var hq = commandID >= hqID;
                if (ImGui.Checkbox("HQ", ref hq))
                    commandID = hq ? commandID + hqID : commandID - hqID;
                break;
            case RaptureHotbarModule.HotbarSlotType.EventItem:
                ImGuiEx.ExcelSheetCombo($"ID##{commandType}", ref commandID, new ImGuiEx.ExcelSheetComboOptions<EventItem> { FormatRow = r => $"[#{r.RowId}] {r.Name}" });
                break;
            case RaptureHotbarModule.HotbarSlotType.Emote:
                ImGuiEx.ExcelSheetCombo($"ID##{commandType}", ref commandID, new ImGuiEx.ExcelSheetComboOptions<Emote> { FormatRow = r => $"[#{r.RowId}] {r.Name}" });
                break;
            case RaptureHotbarModule.HotbarSlotType.Marker:
                ImGuiEx.ExcelSheetCombo($"ID##{commandType}", ref commandID, new ImGuiEx.ExcelSheetComboOptions<Marker> { FormatRow = r => $"[#{r.RowId}] {r.Name}" });
                break;
            case RaptureHotbarModule.HotbarSlotType.CraftAction:
                ImGuiEx.ExcelSheetCombo($"ID##{commandType}", ref commandID, new ImGuiEx.ExcelSheetComboOptions<CraftAction> { FormatRow = r => $"[#{r.RowId}] {r.Name}" });
                break;
            case RaptureHotbarModule.HotbarSlotType.GeneralAction:
                ImGuiEx.ExcelSheetCombo($"ID##{commandType}", ref commandID, new ImGuiEx.ExcelSheetComboOptions<GeneralAction> { FormatRow = r => $"[#{r.RowId}] {r.Name}" });
                break;
            case RaptureHotbarModule.HotbarSlotType.BuddyAction:
                ImGuiEx.ExcelSheetCombo($"ID##{commandType}", ref commandID, new ImGuiEx.ExcelSheetComboOptions<BuddyAction> { FormatRow = r => $"[#{r.RowId}] {r.Name}" });
                break;
            case RaptureHotbarModule.HotbarSlotType.MainCommand:
                ImGuiEx.ExcelSheetCombo($"ID##{commandType}", ref commandID, new ImGuiEx.ExcelSheetComboOptions<MainCommand> { FormatRow = r => $"[#{r.RowId}] {r.Name}" });
                break;
            case RaptureHotbarModule.HotbarSlotType.Companion:
                ImGuiEx.ExcelSheetCombo($"ID##{commandType}", ref commandID, new ImGuiEx.ExcelSheetComboOptions<Companion> { FormatRow = r => $"[#{r.RowId}] {r.Singular}" });
                break;
            case RaptureHotbarModule.HotbarSlotType.PetAction:
                ImGuiEx.ExcelSheetCombo($"ID##{commandType}", ref commandID, new ImGuiEx.ExcelSheetComboOptions<PetAction> { FormatRow = r => $"[#{r.RowId}] {r.Name}" });
                break;
            case RaptureHotbarModule.HotbarSlotType.Mount:
                ImGuiEx.ExcelSheetCombo($"ID##{commandType}", ref commandID, new ImGuiEx.ExcelSheetComboOptions<Mount> { FormatRow = r => $"[#{r.RowId}] {r.Singular}" });
                break;
            case RaptureHotbarModule.HotbarSlotType.FieldMarker:
                ImGuiEx.ExcelSheetCombo($"ID##{commandType}", ref commandID, new ImGuiEx.ExcelSheetComboOptions<FieldMarker> { FormatRow = r => $"[#{r.RowId}] {r.Name}" });
                break;
            case RaptureHotbarModule.HotbarSlotType.Recipe:
                ImGuiEx.ExcelSheetCombo($"ID##{commandType}", ref commandID, new ImGuiEx.ExcelSheetComboOptions<Recipe> { FormatRow = r => $"[#{r.RowId}] {r.ItemResult.ValueNullable?.Name}" });
                break;
            case RaptureHotbarModule.HotbarSlotType.ChocoboRaceAbility:
                ImGuiEx.ExcelSheetCombo($"ID##{commandType}", ref commandID, new ImGuiEx.ExcelSheetComboOptions<ChocoboRaceAbility> { FormatRow = r => $"[#{r.RowId}] {r.Name}" });
                break;
            case RaptureHotbarModule.HotbarSlotType.ChocoboRaceItem:
                ImGuiEx.ExcelSheetCombo($"ID##{commandType}", ref commandID, new ImGuiEx.ExcelSheetComboOptions<ChocoboRaceItem> { FormatRow = r => $"[#{r.RowId}] {r.Name}" });
                break;
            case RaptureHotbarModule.HotbarSlotType.ExtraCommand:
                ImGuiEx.ExcelSheetCombo($"ID##{commandType}", ref commandID, new ImGuiEx.ExcelSheetComboOptions<ExtraCommand> { FormatRow = r => $"[#{r.RowId}] {r.Name}" });
                break;
            case RaptureHotbarModule.HotbarSlotType.PvPQuickChat:
                ImGuiEx.ExcelSheetCombo($"ID##{commandType}", ref commandID, new ImGuiEx.ExcelSheetComboOptions<QuickChat> { FormatRow = r => $"[#{r.RowId}] {r.NameAction}" });
                break;
            case RaptureHotbarModule.HotbarSlotType.PvPCombo:
                ImGuiEx.ExcelSheetCombo($"ID##{commandType}", ref commandID, new ImGuiEx.ExcelSheetComboOptions<ActionComboRoute> { FormatRow = r => $"[#{r.RowId}] {r.Name}" });
                break;
            case RaptureHotbarModule.HotbarSlotType.BgcArmyAction:
                // Sheet is BgcArmyAction, but it doesn't appear to be in Lumina
                var __ = (int)commandID;
                if (ImGui.Combo("ID", ref __, "[#0]\0[#1] Engage\0[#2] Disengage\0[#3] Re-engage\0[#4] Execute Limit Break\0[#5] Display Order Hotbar"))
                    commandID = (uint)__;
                break;
            case RaptureHotbarModule.HotbarSlotType.PerformanceInstrument:
                ImGuiEx.ExcelSheetCombo($"ID##{commandType}", ref commandID, new ImGuiEx.ExcelSheetComboOptions<Perform> { FormatRow = r => $"[#{r.RowId}] {r.Instrument}" });
                break;
            case RaptureHotbarModule.HotbarSlotType.McGuffin:
                ImGuiEx.ExcelSheetCombo($"ID##{commandType}", ref commandID, new ImGuiEx.ExcelSheetComboOptions<McGuffin> { FormatRow = r => $"[#{r.RowId}] {r.UIData.ValueNullable?.Name}" });
                break;
            case RaptureHotbarModule.HotbarSlotType.Ornament:
                ImGuiEx.ExcelSheetCombo($"ID##{commandType}", ref commandID, new ImGuiEx.ExcelSheetComboOptions<Ornament> { FormatRow = r => $"[#{r.RowId}] {r.Singular}" });
                break;
            // Doesn't appear to have a sheet
            //case HotbarSlotType.LostFindsItem:
            //    ImGuiEx.ExcelSheetCombo($"ID##{commandType}", ref commandID, new ImGuiEx.ExcelSheetComboOptions<> { FormatRow = r => $"[#{r.RowId}] {r.Name}" });
            //    break;
            default:
                var ___ = (int)commandID;
                if (ImGui.InputInt("ID", ref ___))
                    commandID = (uint)___;
                break;
        }
    }

    private static unsafe void DrawCustomPlaceholders()
    {
        if (!ImGui.BeginTable("自定义占位符", 3, ImGuiTableFlags.Borders | ImGuiTableFlags.ScrollY)) return;

        ImGui.TableSetupScrollFreeze(0, 1);
        ImGui.TableSetupColumn("名称");
        ImGui.TableSetupColumn("占位符");
        ImGui.TableSetupColumn("当前目标");
        ImGui.TableHeadersRow();

        foreach (var (placeholder, pronoun) in PronounManager.CustomPlaceholders)
        {
            ImGui.TableNextRow();
            ImGui.TableNextColumn();

            ImGui.TextUnformatted(pronoun.Name);
            ImGui.TableNextColumn();
            ImGui.TextUnformatted(placeholder);

            var p = pronoun.GetGameObject();
            if (p == null) continue;
            ImGui.TableNextColumn();
            ImGui.TextUnformatted(p->NameString);
        }

        ImGui.EndTable();
    }

    private static void DrawStackHelp()
    {
        ImGui.Text("Creating a Stack");
        ImGui.Indent();
        ImGui.TextWrapped("To start, click the + button in the top left corner, this will create a new stack that you can begin adding actions and functionality to.");
        ImGui.Unindent();

        ImGui.Separator();

        ImGui.Text("Editing a Stack");
        ImGui.Indent();
        ImGui.TextWrapped("Click on a stack from the top left list to display the editing panes for that it. The bottom left pane is where the " +
            "main settings reside, these will change the base functionality for the stack itself.");
        ImGui.Unindent();

        ImGui.Separator();

        ImGui.Text("Editing a Stack's Actions");
        ImGui.Indent();
        ImGui.TextWrapped("The top right pane is where you can add actions, click the + to bring up a box that you can search for them through. " +
            "After adding every action that you would like to change the functionality of, you can additionally select which ones you would like to " +
            "\"adjust\". This means that the selected action will match any other one that replaces it on the hotbar. This can be due to a trait " +
            "(Holy <-> Holy III), a buff (Play -> The Balance) or another plugin (XIVCombo). An example case where you might want it off is when the " +
            "adjusted action has a separate use case, such as XIVCombo turning Play into Draw. You can change the functionality of the individual " +
            "cards while not affecting Draw by adding each of them to the list. Additionally, if the action is currently adjusted by the game, the " +
            "option will be highlighted in green as an indicator.");
        ImGui.Unindent();

        ImGui.Separator();

        ImGui.Text("Editing a Stack's Functionality");
        ImGui.Indent();
        ImGui.TextWrapped("The bottom right pane is where you can change the functionality of the selected actions, by setting a list of targets to " +
            "extend or replace the game's. When the action is used, the plugin will attempt to determine, from top to bottom, which target is a valid choice. " +
            "This will execute before the game's own target priority system and only allow it to continue if not blocked by the stack. If any of the targets " +
            "are valid choices, the plugin will change the action's target to the new one and, additionally, replace the action with the override if set.");
        ImGui.Unindent();

        ImGui.Separator();

        ImGui.Text("Stack Priority");
        ImGui.Indent();
        ImGui.TextWrapped("The executed stack will depend on which one, from top to bottom, first contains the action being used and has its modifier " +
            "keys held. If you would like to use \"All Actions\" in a stack, you can utilize this to add overrides above it in the list. Note that a stack " +
            "does not need to contain any functionality in the event that you would like for a set of actions to never be changed by \"All Actions\" and " +
            "instead use the original.");
        ImGui.Unindent();
    }
}