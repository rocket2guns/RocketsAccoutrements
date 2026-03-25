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

    public Dialog_WriteCitation(RocketMedal medal)
    {
        this.medal = medal;
        this.draft = medal.citation ?? "";

        forcePause = true;
        doCloseX = true;
        absorbInputAroundWindow = true;
        closeOnClickedOutside = false;
    }

    public override Vector2 InitialSize => new(600f, 380f);

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

        // Stat bonuses
        var offsets = medal.def.equippedStatOffsets;
        if (offsets != null && offsets.Count > 0)
        {
            var statsY = descRect.yMax + 10f;
            GUI.color = new Color(0.5f, 0.8f, 0.5f);
            foreach (var mod in offsets)
            {
                var sign = mod.value >= 0 ? "+" : "";
                var line = $"{sign}{mod.value.ToStringPercent()} {mod.stat.LabelCap}";
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
        Widgets.Label(headerRect, "Write Citation");
        Text.Font = GameFont.Small;

        // Instruction
        var instrRect = new Rect(rect.x, headerRect.yMax + 4f, rect.width, 36f);
        GUI.color = Color.gray;
        Text.Font = GameFont.Tiny;
        Widgets.Label(instrRect, "Write the citation to be engraved on this medal. This will be permanently sealed once the medal is awarded.");
        GUI.color = Color.white;

        // Text area
        Text.Font = GameFont.Small;
        var textRect = new Rect(rect.x, instrRect.yMax + 8f, rect.width, 150f);
        draft = Widgets.TextArea(textRect, draft);
        if (draft.Length > MAX_LENGTH)
            draft = draft.Substring(0, MAX_LENGTH);

        // Character count
        var countColor = draft.Length > MAX_LENGTH - 30 ? Color.yellow : Color.gray;
        GUI.color = countColor;
        Text.Font = GameFont.Tiny;
        var countRect = new Rect(rect.x, textRect.yMax + 4f, rect.width, 20f);
        Text.Anchor = TextAnchor.UpperRight;
        Widgets.Label(countRect, $"{draft.Length} / {MAX_LENGTH}");
        Text.Anchor = TextAnchor.UpperLeft;
        GUI.color = Color.white;

        // Buttons
        Text.Font = GameFont.Small;
        var btnY = textRect.yMax + 28f;
        var btnWidth = (rect.width - 10f) / 2f;

        if (Widgets.ButtonText(new Rect(rect.x, btnY, btnWidth, 35f), "Confirm"))
        {
            medal.citation = draft.NullOrEmpty() ? null : draft.Trim();
            Close();
        }

        if (Widgets.ButtonText(new Rect(rect.x + btnWidth + 10f, btnY, btnWidth, 35f), "Clear"))
        {
            draft = "";
        }
    }
}