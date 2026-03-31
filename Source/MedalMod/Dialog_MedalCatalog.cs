using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using RimWorld;
using UnityEngine;
using Verse;

namespace MedalMod;

public class Dialog_MedalCatalog : Window
{
    private Vector2 _scrollPosition;
    private List<ThingDef> _medals;

    private const float ICON_SIZE = 64f;
    private const float ROW_PADDING = 10f;
    private const float MIN_ROW_HEIGHT = 80f;
    private const float WINDOW_WIDTH = 500f;
    private const float WINDOW_HEIGHT = 600f;

    public Dialog_MedalCatalog()
    {
        doCloseX = true;
        doCloseButton = false;
        absorbInputAroundWindow = false;
        forcePause = false;

        _medals = DefDatabase<ThingDef>.AllDefs
            .Where(d => d.thingClass != null && typeof(RocketMedal).IsAssignableFrom(d.thingClass))
            .OrderBy(d => d.uiOrder)
            .ThenBy(d => d.label)
            .ToList();
    }

    public override Vector2 InitialSize => new(WINDOW_WIDTH, WINDOW_HEIGHT);

    public override void DoWindowContents(Rect inRect)
    {
        // Header
        Text.Font = GameFont.Medium;
        var headerRect = new Rect(inRect.x, inRect.y, inRect.width, 35f);
        Widgets.Label(headerRect, "ROCKET_MedalCatalog".Translate());
        Text.Font = GameFont.Small;

        GUI.color = new Color(1f, 1f, 1f, 0.15f);
        Widgets.DrawLineHorizontal(inRect.x, headerRect.yMax + 2f, inRect.width);
        GUI.color = Color.white;

        // Scroll area
        var listRect = new Rect(inRect.x, headerRect.yMax + 10f, inRect.width, inRect.height - headerRect.height - 10f);
        var viewWidth = listRect.width - 16f;

        var totalHeight = _medals.Sum(d => GetRowHeight(d, viewWidth) + ROW_PADDING);
        var viewRect = new Rect(0f, 0f, viewWidth, totalHeight);

        Widgets.BeginScrollView(listRect, ref _scrollPosition, viewRect);

        var curY = 0f;
        foreach (var def in _medals)
            DrawMedalRow(def, viewWidth, ref curY);

        Widgets.EndScrollView();
    }

    private float GetRowHeight(ThingDef def, float width)
    {
        var textWidth = width - ICON_SIZE - (ROW_PADDING * 3);
        var height = ROW_PADDING;

        Text.Font = GameFont.Small;
        height += Text.CalcHeight(def.LabelCap, textWidth) + 2f;

        Text.Font = GameFont.Tiny;
        height += Text.CalcHeight(def.description ?? "", textWidth) + 2f;

        var statText = GetStatSummary(def);
        if (!statText.NullOrEmpty())
            height += Text.CalcHeight(statText, textWidth) + 2f;

        var ext = def.GetModExtension<MedalExtension>();
        if (ModsConfig.RoyaltyActive && (ext?.honorAwarded ?? 0) > 0)
            height += 18f + 2f;

        height += ROW_PADDING;
        Text.Font = GameFont.Small;
        return Mathf.Max(MIN_ROW_HEIGHT, height);
    }

    private void DrawMedalRow(ThingDef def, float width, ref float curY)
    {
        var rowHeight = GetRowHeight(def, width);
        var rowRect = new Rect(0f, curY, width, rowHeight);

        if (Mouse.IsOver(rowRect))
            Widgets.DrawHighlight(rowRect);

        // Icon
        var iconRect = new Rect(ROW_PADDING, curY + (rowHeight - ICON_SIZE) / 2f, ICON_SIZE, ICON_SIZE);
        Widgets.DefIcon(iconRect, def);

        var textX = iconRect.xMax + ROW_PADDING;
        var textWidth = width - ICON_SIZE - (ROW_PADDING * 3);
        var textY = curY + ROW_PADDING;

        // Name
        var nameValue = CultureInfo.CurrentCulture.TextInfo.ToTitleCase(def.LabelCap);
        Text.Font = GameFont.Small;
        var nameHeight = Text.CalcHeight(nameValue, textWidth);
        Widgets.Label(new Rect(textX, textY, textWidth, nameHeight), nameValue);
        textY += nameHeight + 2f;

        // Description
        Text.Font = GameFont.Tiny;
        GUI.color = Color.gray;
        var desc = def.description ?? "";
        var descHeight = Text.CalcHeight(desc, textWidth);
        Widgets.Label(new Rect(textX, textY, textWidth, descHeight), desc);
        GUI.color = Color.white;
        textY += descHeight + 2f;

        // Honor
        var ext = def.GetModExtension<MedalExtension>();
        var honor = ext?.honorAwarded ?? 0;
        if (ModsConfig.RoyaltyActive && honor > 0)
        {
            GUI.color = Dialog_MedalAwarded.GoldColor;
            var iconSize = 14f;
            var gap = 4f;
            var labelText = $"honor +{honor}";
            if (MedalTextures.HonorIcon != null)
                GUI.DrawTexture(new Rect(textX, textY + 2f, iconSize, iconSize), MedalTextures.HonorIcon);
            Widgets.Label(new Rect(textX + iconSize + gap, textY, textWidth - iconSize - gap, 18f), labelText);
            GUI.color = Color.white;
            textY += 18f + 2f;
        }

        // Stat offsets
        var statText = GetStatSummary(def);
        if (!statText.NullOrEmpty())
        {
            Text.Font = GameFont.Tiny;
            GUI.color = new Color(0.5f, 0.8f, 0.5f);
            var statHeight = Text.CalcHeight(statText, textWidth);
            Widgets.Label(new Rect(textX, textY, textWidth, statHeight), statText);
            GUI.color = Color.white;
        }

        Text.Font = GameFont.Small;

        // Separator
        GUI.color = new Color(1f, 1f, 1f, 0.08f);
        Widgets.DrawLineHorizontal(0f, curY + rowHeight, width);
        GUI.color = Color.white;

        curY += rowHeight + ROW_PADDING;
    }

    private string GetStatSummary(ThingDef def)
    {
        var offsets = def.equippedStatOffsets;
        if (offsets == null || offsets.Count == 0) return null;

        var sb = new StringBuilder();
        for (var i = 0; i < offsets.Count; i++)
        {
            if (i > 0) sb.Append(", ");
            var val = offsets[i].value;
            sb.Append(val >= 0 ? "+" : "");
            sb.Append(offsets[i].stat.ValueToString(val));
            sb.Append(' ');
            sb.Append(offsets[i].stat.LabelCap);
        }
        return sb.ToString();
    }
}