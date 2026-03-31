using UnityEngine;
using Verse;

namespace MedalMod;

public class Dialog_WriteCitation : Window
{
    private readonly RocketMedal medal;
    private string draft;
    private const int MAX_LENGTH = 300;
    private const float MEDAL_COLUMN_WIDTH = 160f;
    private const float ICON_SIZE = 160f;
    private const float BUTTON_HEIGHT = 35f;
    private const float BUTTON_AREA_HEIGHT = BUTTON_HEIGHT + 10f;

    public Dialog_WriteCitation(RocketMedal medal)
    {
        this.medal = medal;
        this.draft = medal.citation ?? "";

        forcePause = true;
        doCloseX = true;
        absorbInputAroundWindow = true;
        closeOnClickedOutside = false;
    }

    public override Vector2 InitialSize => new(600f, 400f);

    public override void DoWindowContents(Rect inRect)
    {
        var medalCol = new Rect(inRect.x, inRect.y, MEDAL_COLUMN_WIDTH, inRect.height - 45f);
        DrawMedalColumn(medalCol);

        // Vertical separator
        var separatorX = medalCol.xMax + 10f;
        GUI.color = new Color(1f, 1f, 1f, 0.15f);
        Widgets.DrawLineVertical(separatorX, inRect.y, inRect.height - 45f);
        GUI.color = Color.white;

        var writeCol = new Rect(separatorX + 10f, inRect.y, inRect.width - MEDAL_COLUMN_WIDTH - 30f, inRect.height);
        DrawWritingColumn(writeCol);
    }

    private void DrawMedalColumn(Rect rect)
    {
        // Medal icon
        var iconX = rect.x + (rect.width - ICON_SIZE) / 2f;
        var iconRect = new Rect(iconX, rect.y + 10f, ICON_SIZE, ICON_SIZE);
        Widgets.ThingIcon(iconRect, medal);

        // Medal name
        Text.Font = GameFont.Small;
        Text.Anchor = TextAnchor.UpperCenter;
        var nameHeight = Text.CalcHeight(medal.LabelCap, rect.width);
        var nameRect = new Rect(rect.x, iconRect.yMax + 10f, rect.width, nameHeight);
        Widgets.Label(nameRect, medal.LabelCap);

        // Medal description
        Text.Font = GameFont.Tiny;
        var desc = medal.def.description ?? "";
        var descHeight = Text.CalcHeight(desc, rect.width - 10f);
        var descRect = new Rect(rect.x + 5f, nameRect.yMax + 6f, rect.width - 10f, descHeight);
        GUI.color = Color.gray;
        Widgets.Label(descRect, desc);
        GUI.color = Color.white;
        var statsY = descRect.yMax + 10f;
        
        var ext = medal.def.GetModExtension<MedalExtension>();
        var honor = ext?.honorAwarded ?? 0;
        if (ModsConfig.RoyaltyActive && honor > 0)
        {
            GUI.color = Dialog_MedalAwarded.GoldColor;
            var iconSize = 14f;
            var gap = 4f;
            var lineRect = new Rect(rect.x + 5f, statsY, rect.width - 10f, 18f);
            var labelText = $"honor +{honor}";
            var textWidth = Text.CalcSize(labelText).x;
            var totalWidth = iconSize + gap + textWidth;
            var startX = lineRect.x + (lineRect.width - totalWidth) / 2f;
            var centerY = lineRect.y + (lineRect.height - iconSize) / 2f;
            if (MedalTextures.HonorIcon != null)
                GUI.DrawTexture(new Rect(startX, centerY, iconSize, iconSize), MedalTextures.HonorIcon);
            Widgets.Label(new Rect(startX + iconSize + gap, lineRect.y, textWidth, lineRect.height), labelText);
            statsY += 18f + 6f;
            GUI.color = Color.white;
        }

        // Stat bonuses
        var offsets = medal.def.equippedStatOffsets;
        if (offsets is { Count: > 0 })
        {
            GUI.color = new Color(0.5f, 0.8f, 0.5f);
            foreach (var mod in offsets)
            {
                var sign = mod.value >= 0 ? "+" : "";
                var line = $"{sign}{mod.stat.ValueToString(mod.value)} {mod.stat.LabelCap}";
                var lineRect = new Rect(rect.x + 5f, statsY, rect.width - 10f, 18f);
                Widgets.Label(lineRect, line);
                statsY += 18f;
            }
            GUI.color = Color.white;
        }

        Text.Font = GameFont.Small;
        Text.Anchor = TextAnchor.UpperLeft;
    }

    private void DrawWritingColumn(Rect rect)
    {
        // Header
        Text.Font = GameFont.Medium;
        var headerRect = new Rect(rect.x, rect.y + 10f, rect.width, 30f);
        Widgets.Label(headerRect, "ROCKET_WriteCitation".Translate());
        Text.Font = GameFont.Small;

        // Instruction
        var instrRect = new Rect(rect.x, headerRect.yMax + 4f, rect.width, 36f);
        GUI.color = Color.gray;
        Text.Font = GameFont.Tiny;
        Widgets.Label(instrRect, "ROCKET_WriteCitationDesc".Translate());
        GUI.color = Color.white;

        // Text area — fills all space between instructions and the button row
        Text.Font = GameFont.Small;
        var btnY = rect.yMax - BUTTON_AREA_HEIGHT;
        var textRect = new Rect(rect.x, instrRect.yMax + 8f, rect.width, btnY - instrRect.yMax - 8f - 28f);
        draft = Widgets.TextArea(textRect, draft);
        if (draft.Length > MAX_LENGTH)
            draft = draft.Substring(0, MAX_LENGTH);

        // Character count — sits just above the buttons
        var countColor = draft.Length > MAX_LENGTH - 30 ? Color.yellow : Color.gray;
        GUI.color = countColor;
        Text.Font = GameFont.Tiny;
        var countRect = new Rect(rect.x, textRect.yMax + 4f, rect.width, 20f);
        Text.Anchor = TextAnchor.UpperRight;
        Widgets.Label(countRect, $"{draft.Length} / {MAX_LENGTH}");
        Text.Anchor = TextAnchor.UpperLeft;
        GUI.color = Color.white;

        // Buttons — anchored to bottom of column
        Text.Font = GameFont.Small;
        var btnWidth = (rect.width - 10f) / 2f;

        if (Widgets.ButtonText(new Rect(rect.x, btnY, btnWidth, BUTTON_HEIGHT), "ROCKET_ConfirmButton".Translate()))
        {
            medal.citation = draft.NullOrEmpty() ? null : draft.Trim();
            Close();
        }

        if (Widgets.ButtonText(new Rect(rect.x + btnWidth + 10f, btnY, btnWidth, BUTTON_HEIGHT), "ROCKET_ClearButton".Translate()))
        {
            draft = "";
        }
    }
}