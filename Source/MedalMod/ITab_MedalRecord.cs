using RimWorld;
using UnityEngine;
using Verse;

namespace MedalMod;

public class ITab_MedalRecord : ITab
{
    private static readonly Color GoldColor = new(0.9f, 0.85f, 0.4f);
    private static readonly Color GreenColor = new(0.4f, 0.9f, 0.4f);
    private static readonly Color MutedColor = new(0.7f, 0.7f, 0.7f);

    public ITab_MedalRecord()
    {
        this.size = new Vector2(400f, 520f);
        this.labelKey = "ROCKET_Record";
    }

    public override bool IsVisible => SelThing is RocketMedal;

    protected override void FillTab()
    {
        if (SelThing is not RocketMedal medal) return;

        var rect = new Rect(0f, 0f, size.x, size.y).ContractedBy(16f);
        var curY = rect.y;

        // Medal icon, centered
        var iconSize = 128f;
        var iconRect = new Rect(rect.x + (rect.width - iconSize) / 2f, curY, iconSize, iconSize);
        Widgets.ThingIcon(iconRect, medal);
        curY += iconSize + 10f;

        // Medal name, centered
        Text.Font = GameFont.Medium;
        Text.Anchor = TextAnchor.MiddleCenter;
        var nameHeight = Text.CalcHeight(medal.MedalLabel, rect.width);
        Widgets.Label(new Rect(rect.x, curY, rect.width, nameHeight), medal.MedalLabel);
        curY += nameHeight + 6f;

        // Award details, centered
        Text.Font = GameFont.Small;
        Text.Anchor = TextAnchor.MiddleCenter;

        if (medal.BiocodeComp is { Biocoded: true, CodedPawn: not null })
        {
            var awardLine = "ROCKET_MedalAwardedTo".Translate(medal.BiocodeComp.CodedPawn.NameFullColored);
            var awardHeight = Text.CalcHeight(awardLine, rect.width);
            Widgets.Label(new Rect(rect.x, curY, rect.width, awardHeight), awardLine);
            curY += awardHeight + 4f;
        }
        
        if (medal.awardedBy != null)
        {
            Text.Font = GameFont.Tiny;
            GUI.color = MutedColor;
            var presenterName = medal.awardedBy.Dead
                ? $"{medal.awardedBy.LabelShort} (deceased)"
                : medal.awardedBy.NameFullColored.ToString();
            var presenterLine = "ROCKET_MedalPresentedBy".Translate(presenterName);
            var presenterHeight = Text.CalcHeight(presenterLine, rect.width);
            Widgets.Label(new Rect(rect.x, curY, rect.width, presenterHeight), presenterLine);
            curY += presenterHeight + 4f;
            Text.Font = GameFont.Small;
        }

        // Date and ceremony quality on one line
        if (medal.awardedTick >= 0)
        {
            Text.Font = GameFont.Tiny;
            var tile = medal.Wearer?.Map?.Tile
                       ?? medal.MapHeld?.Tile
                       ?? Find.CurrentMap?.Tile ?? 0;
            var dateStr = GenDate.DateFullStringAt(
                GenDate.TickGameToAbs(medal.awardedTick),
                Find.WorldGrid.LongLatOf(tile));

            var dateLine = dateStr;
            if (medal.ceremonyQuality >= 0)
                dateLine += $" at a {CeremonyQuality.GetQualityLabel(medal.ceremonyQuality).CapitalizeFirst()} ceremony";

            GUI.color = MutedColor;
            var dateHeight = Text.CalcHeight(dateLine, rect.width);
            Widgets.Label(new Rect(rect.x, curY, rect.width, dateHeight), dateLine);
            GUI.color = Color.white;
            curY += dateHeight + 4f;
            Text.Font = GameFont.Small;
        }

        // Days worn
        if (medal.awardedTick >= 0)
        {
            Text.Font = GameFont.Tiny;
            var days = (Find.TickManager.TicksGame - medal.awardedTick) / GenDate.TicksPerDay;
            GUI.color = MutedColor;
            var daysText = $"Worn for {days} days";
            var daysHeight = Text.CalcHeight(daysText, rect.width);
            Widgets.Label(new Rect(rect.x, curY, rect.width, daysHeight), daysText);
            GUI.color = Color.white;
            curY += daysHeight + 4f;
            Text.Font = GameFont.Small;
        }

        curY += 6f;

        // Citation, gold, centered
        if (!medal.citation.NullOrEmpty())
        {
            GUI.color = GoldColor;
            Text.Anchor = TextAnchor.MiddleCenter;
            var citationText = $"\"{medal.citation}\"";
            var citationHeight = Text.CalcHeight(citationText, rect.width - 40f);
            Widgets.Label(new Rect(rect.x + 20f, curY, rect.width - 40f, citationHeight), citationText);
            GUI.color = Color.white;
            curY += citationHeight + 12f;
        }

        // Stat bonuses, green, centered
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
                Widgets.Label(new Rect(rect.x, curY, rect.width, 24f), line);
                curY += 24f;
            }
            GUI.color = Color.white;
            curY += 8f;
            Text.Font = GameFont.Small;
        }

        // Trait effects, centered
        if (MedalMod.Settings.MedalDynamicTraits && medal.BiocodeComp is not { Biocoded: true })
        {
            Text.Font = GameFont.Tiny;
            var ext = medal.def.GetModExtension<MedalExtension>();
            if (ext != null && ((ext.removesTraits is { Count: > 0 }) || (ext.addsTraits is { Count: > 0 })))
            {
                Text.Anchor = TextAnchor.MiddleCenter;
                if (ext.removesTraits != null)
                {
                    foreach (var entry in ext.removesTraits)
                    {
                        var label = entry.Label;
                        GUI.color = Color.grey;
                        var line = $"May remove {label.CapitalizeFirst()} ({entry.chance.ToStringPercent()})";
                        Widgets.Label(new Rect(rect.x, curY, rect.width, 24f), line);
                        curY += 24f;
                    }
                }
                if (ext.addsTraits != null)
                {
                    foreach (var entry in ext.addsTraits)
                    {
                        var label = entry.Label;
                        GUI.color = Color.grey;;
                        var line = $"May grant {label.CapitalizeFirst()} ({entry.chance.ToStringPercent()})";
                        Widgets.Label(new Rect(rect.x, curY, rect.width, 24f), line);
                        curY += 24f;
                    }
                }
                GUI.color = Color.white;
                Text.Font = GameFont.Small;
            }
        }

        // Reset
        Text.Font = GameFont.Small;
        Text.Anchor = TextAnchor.UpperLeft;
        GUI.color = Color.white;
    }
}