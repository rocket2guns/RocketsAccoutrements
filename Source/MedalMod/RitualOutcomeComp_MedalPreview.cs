using System.Text;
using HarmonyLib;
using UnityEngine;

namespace MedalMod;
using RimWorld;
using Verse;

public class RitualOutcomeComp_MedalPreview : RitualOutcomeComp
{
    public override bool Applies(LordJob_Ritual ritual) =>
        ritual?.selectedTarget.Thing is RocketMedal;

    public override bool DataRequired => false;

    public override float QualityOffset(LordJob_Ritual ritual, RitualOutcomeComp_Data data) => 0f;

    public override string GetDesc(LordJob_Ritual ritual = null, RitualOutcomeComp_Data data = null)
    {
        if (ritual?.selectedTarget.Thing is not RocketMedal medal)
            return "Medal details unavailable.";
        return medal.MedalLabel;
    }

    public override QualityFactor GetQualityFactor(
        Precept_Ritual ritual,
        TargetInfo ritualTarget,
        RitualObligation obligation,
        RitualRoleAssignments assignments,
        RitualOutcomeComp_Data data)
    {
        if (ritualTarget.Thing is not RocketMedal medal) return null;

        var sb = new StringBuilder();

        var statOffsets = medal.def.equippedStatOffsets;
        if (statOffsets is { Count: > 0 })
        {
            foreach (var stat in statOffsets)
            {
                var sign = stat.value > 0 ? "+" : "";
                sb.AppendLine($"{stat.stat.LabelCap}: {sign}{stat.stat.ValueToString(stat.value)}");
            }
        }

        if (MedalMod.Settings.MedalDynamicTraits)
        {
            var ext = medal.def.GetModExtension<MedalExtension>();
            if (ext != null)
            {
                if (ext.removesTraits is { Count: > 0 })
                    foreach (var entry in ext.removesTraits)
                        sb.AppendLine($"May remove {entry.Label.CapitalizeFirst()} ({entry.chance.ToStringPercent()})");
                if (ext.addsTraits is { Count: > 0 })
                    foreach (var entry in ext.addsTraits)
                        sb.AppendLine($"May grant {entry.Label.CapitalizeFirst()} ({entry.chance.ToStringPercent()})");
            }
        }

        return new QualityFactor
        {
            label = medal.MedalLabel,
            count = !medal.citation.NullOrEmpty() ? "Citation written" : "No citation",
            qualityChange = "",
            quality = 0f,
            positive = true,
            present = true,
            noMiddleColumnInfo = false,
            toolTip = sb.ToString().TrimEnd(),
            priority = 100f
        };
    }
}

public class RitualOutcomeComp_CeremonyAttendance : RitualOutcomeComp
{
    public override bool Applies(LordJob_Ritual ritual) => true;

    public override bool DataRequired => false;

    public override float QualityOffset(LordJob_Ritual ritual, RitualOutcomeComp_Data data)
    {
        if (ritual?.Map == null) return 0f;
        var attendees = ritual.assignments.Participants.Count;
        var total = ritual.Map.mapPawns.FreeColonistsSpawnedCount;
        var ratio = total > 0 ? Mathf.Clamp01((float)attendees / total) : 0f;
        return ratio * 0.4f;
    }

    public override string GetDesc(LordJob_Ritual ritual = null, RitualOutcomeComp_Data data = null)
    {
        return "Ceremony attendance";
    }

    public override QualityFactor GetQualityFactor(
        Precept_Ritual ritual,
        TargetInfo ritualTarget,
        RitualObligation obligation,
        RitualRoleAssignments assignments,
        RitualOutcomeComp_Data data)
    {
        var map = ritualTarget.Map ?? Find.CurrentMap;
        if (map == null) return null;

        var attendees = assignments.Participants.Count;
        var total = map.mapPawns.FreeColonistsSpawnedCount;
        var ratio = total > 0 ? Mathf.Clamp01((float)attendees / total) : 0f;
        var contribution = ratio * 0.4f;

        return new QualityFactor
        {
            label = "Attendance",
            count = $"{attendees} / {total}",
            qualityChange = $"+{contribution.ToStringPercent()}",
            quality = contribution,
            positive = ratio >= 0.25f,
            present = true,
            toolTip = "Up to 40% of ceremony quality. More colonists means a more meaningful ceremony.",
            priority = 50f
        };
    }
}

public class RitualOutcomeComp_CeremonyRoom : RitualOutcomeComp
{
    public override bool Applies(LordJob_Ritual ritual) => true;

    public override bool DataRequired => false;

    public override float QualityOffset(LordJob_Ritual ritual, RitualOutcomeComp_Data data)
    {
        var impressiveness = CeremonyQuality.GetRoomImpressiveness(ritual.selectedTarget);
        var roomScore = Mathf.Clamp01(impressiveness / 170f);
        return roomScore * 0.4f;
    }

    public override string GetDesc(LordJob_Ritual ritual = null, RitualOutcomeComp_Data data = null)
    {
        return "Ceremony venue";
    }

    public override QualityFactor GetQualityFactor(
        Precept_Ritual ritual,
        TargetInfo ritualTarget,
        RitualObligation obligation,
        RitualRoleAssignments assignments,
        RitualOutcomeComp_Data data)
    {
        var impressiveness = CeremonyQuality.GetRoomImpressiveness(ritualTarget);
        var roomScore = Mathf.Clamp01(impressiveness / 170f);
        var contribution = roomScore * 0.4f;

        var roomLabel = impressiveness <= 0f ? "Outdoors" : $"{impressiveness:F0}";

        return new QualityFactor
        {
            label = "Venue",
            count = roomLabel,
            qualityChange = $"+{contribution.ToStringPercent()}",
            quality = contribution,
            positive = impressiveness >= 25f,
            present = true,
            toolTip = "Up to 40% of ceremony quality. Hold the ceremony in an impressive room for a better outcome.",
            priority = 40f
        };
    }
}

[HarmonyPatch(typeof(Dialog_BeginLordJob), nameof(Dialog_BeginLordJob.DoLeftColumn))]
public static class PatchDrawMedalInRitual
{
    public static void Prefix(Dialog_BeginLordJob __instance, ref RectDivider layout)
    {
        if (__instance is not Dialog_BeginRitual ritual) return;
        
        var target = Traverse.Create(ritual).Field("target").GetValue<TargetInfo>();
        if (target.Thing is not RocketMedal medal) return;
        
        var row = layout.NewRow(60f, marginOverride: 6f);
        var iconRect = new Rect(row.Rect.x, row.Rect.y, 50f, 50f);
        Widgets.ThingIcon(iconRect, medal);
        
        var labelRect = new Rect(iconRect.xMax + 10f, row.Rect.y, row.Rect.width - 60f, 50f);
        Text.Anchor = TextAnchor.MiddleLeft;
        Text.Font = GameFont.Medium;
        Widgets.Label(labelRect, medal.MedalLabel);
        Text.Font = GameFont.Small;
        Text.Anchor = TextAnchor.UpperLeft;
    }
}

public class RitualOutcomeComp_CeremonyCitation : RitualOutcomeComp
{
    public override bool Applies(LordJob_Ritual ritual) =>
        ritual?.selectedTarget.Thing is RocketMedal;

    public override bool DataRequired => false;

    public override float QualityOffset(LordJob_Ritual ritual, RitualOutcomeComp_Data data)
    {
        if (ritual?.selectedTarget.Thing is not RocketMedal medal) return 0f;
        return medal.citation.NullOrEmpty() ? 0f : 0.2f;
    }

    public override string GetDesc(LordJob_Ritual ritual = null, RitualOutcomeComp_Data data = null)
    {
        return "Citation";
    }

    public override QualityFactor GetQualityFactor(
        Precept_Ritual ritual,
        TargetInfo ritualTarget,
        RitualObligation obligation,
        RitualRoleAssignments assignments,
        RitualOutcomeComp_Data data)
    {
        if (ritualTarget.Thing is not RocketMedal medal) return null;

        var hasCitation = !medal.citation.NullOrEmpty();

        return new QualityFactor
        {
            label = "Citation",
            count = hasCitation ? "Written" : "None",
            qualityChange = hasCitation ? "+20%" : "+0%",
            quality = hasCitation ? 0.2f : 0f,
            positive = hasCitation,
            present = true,
            toolTip = hasCitation 
                ? $"\"{medal.citation}\"\n\n20% of ceremony quality. The citation adds weight and meaning to the award."
                : "No citation written. You will be prompted during the ceremony, or write one before the ceremony for 20% bonus to ceremony quality.",
            priority = 30f
        };
    }
}