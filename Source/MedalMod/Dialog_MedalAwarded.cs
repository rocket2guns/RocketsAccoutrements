using RimWorld;
using UnityEngine;
using Verse;

namespace MedalMod;

public class Dialog_MedalAwarded : Window
{
    public static readonly Color GoldColor = new(0.9f, 0.85f, 0.4f);
    private static readonly Color GreenColor = new(0.4f, 0.9f, 0.4f);

    private readonly RocketMedal medal;
    private readonly Pawn awardee;
    private readonly Pawn presenter;

    public Dialog_MedalAwarded(RocketMedal medal, Pawn awardee, Pawn presenter)
    {
        this.medal = medal;
        this.awardee = awardee;
        this.presenter = presenter;
        forcePause = true;
        doCloseX = true;
        absorbInputAroundWindow = true;
    }

    public override Vector2 InitialSize => new(500f, 500f);

    public override void DoWindowContents(Rect inRect)
    {
        var curY = inRect.y;

        // Medal icon, centered
        var iconRect = new Rect(inRect.x + (inRect.width - 128f) / 2f, curY, 128f, 128f);
        Widgets.ThingIcon(iconRect, medal);
        curY += 138f;

        // Medal name
        Text.Font = GameFont.Medium;
        Text.Anchor = TextAnchor.MiddleCenter;
        var nameHeight = Text.CalcHeight(medal.MedalLabel, inRect.width);
        Widgets.Label(new Rect(inRect.x, curY, inRect.width, nameHeight), medal.MedalLabel);
        curY += nameHeight + 6f;

        // Awarded to
        Text.Font = GameFont.Small;
        Text.Anchor = TextAnchor.MiddleCenter;
        var awardLine = "ROCKET_MedalAwardedTo".Translate(awardee.NameFullColored);
        var awardHeight = Text.CalcHeight(awardLine, inRect.width);
        Widgets.Label(new Rect(inRect.x, curY, inRect.width, awardHeight), awardLine);
        curY += awardHeight + 4f;

        // Presented by
        Text.Font = GameFont.Tiny;
        var presenterLine = "ROCKET_MedalPresentedBy".Translate(presenter.NameFullColored);
        var presenterHeight = Text.CalcHeight(presenterLine, inRect.width);
        Widgets.Label(new Rect(inRect.x, curY, inRect.width, presenterHeight), presenterLine);
        curY += presenterHeight + 10f;
        Text.Font = GameFont.Small;
        
        if (medal.awardedTick >= 0)
        {
            var tile = awardee.Map?.Tile ?? Find.CurrentMap?.Tile ?? 0;
            var dateStr = GenDate.DateFullStringAt(
                GenDate.TickGameToAbs(medal.awardedTick),
                Find.WorldGrid.LongLatOf(tile));
            Text.Font = GameFont.Tiny;
            GUI.color = Color.gray;
            var dateHeight = Text.CalcHeight(dateStr, inRect.width);
            Widgets.Label(new Rect(inRect.x, curY, inRect.width, dateHeight), dateStr);
            GUI.color = Color.white;
            Text.Font = GameFont.Small;
            curY += dateHeight + 10f;
        }
        else
        {
            curY += 6f;
        }

        // Citation
        if (!medal.citation.NullOrEmpty())
        {
            GUI.color = GoldColor;
            Text.Anchor = TextAnchor.MiddleCenter;
            var citationText = $"\"{medal.citation}\"";
            var citationHeight = Text.CalcHeight(citationText, inRect.width - 40f);
            Widgets.Label(new Rect(inRect.x + 20f, curY, inRect.width - 40f, citationHeight), citationText);
            GUI.color = Color.white;
            curY += citationHeight + 12f;
        }
        
        
        var ext = medal.def.GetModExtension<MedalExtension>();
        var honor = ext?.honorAwarded ?? 0;
        if (ModsConfig.RoyaltyActive && honor > 0)
        {
            Text.Font = GameFont.Tiny;
            GUI.color = GoldColor;
            var iconSize = 14f;
            var gap = 4f;
            var labelText = $"honor +{honor}";
            var textWidth = Text.CalcSize(labelText).x;
            var totalWidth = iconSize + gap + textWidth;
            var startX = inRect.x + (inRect.width - totalWidth) / 2f;
            var centerY = curY + (24f - iconSize) / 2f;
            if (MedalTextures.HonorIcon != null)
                GUI.DrawTexture(new Rect(startX, centerY, iconSize, iconSize), MedalTextures.HonorIcon);
            Widgets.Label(new Rect(startX + iconSize + gap, curY, textWidth, 24f), labelText);
            GUI.color = Color.white;
            Text.Font = GameFont.Small;
            curY += 24f;
        }
        
        // Stat bonuses
        var statOffsets = medal.def.equippedStatOffsets;
        if (statOffsets is { Count: > 0 })
        {
            Text.Font = GameFont.Tiny;
            GUI.color = GreenColor;
            Text.Anchor = TextAnchor.MiddleCenter;
            foreach (var stat in statOffsets)
            {
                var sign = stat.value > 0 ? "+" : "";
                var line = $"{stat.stat.LabelCap}: {sign}{stat.stat.ValueToString(stat.value)}";
                Widgets.Label(new Rect(inRect.x, curY, inRect.width, 24f), line);
                curY += 24f;
            }
            GUI.color = Color.white;
            Text.Font = GameFont.Small;
        }

        // Reset and close button
        Text.Font = GameFont.Small;
        Text.Anchor = TextAnchor.UpperLeft;
        GUI.color = Color.white;

        if (Widgets.ButtonText(new Rect(inRect.x + (inRect.width - 120f) / 2f, inRect.yMax - 45f, 120f, 35f), "ROCKET_CloseButton".Translate()))
            Close();
    }
}