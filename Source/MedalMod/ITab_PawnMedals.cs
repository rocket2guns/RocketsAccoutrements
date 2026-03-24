using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;

namespace MedalMod
{
    [StaticConstructorOnStartup]
    public static class InjectMedalTab
    {
        static InjectMedalTab()
        {
            var tabType = typeof(ITab_PawnMedals);
            var tabInstance = InspectTabManager.GetSharedInstance(tabType);
            var humanDef = ThingDef.Named("Human");
            var corpseDef = ThingDef.Named("Corpse_Human");
            InjectIntoDef(humanDef, tabType, tabInstance);
            InjectIntoDef(corpseDef, tabType, tabInstance);
        }

        private static void InjectIntoDef(ThingDef def, Type tabType, InspectTabBase tabInstance)
        {
            if (def == null) return;
            if (def.inspectorTabs == null)
                def.inspectorTabs = new List<Type>();
            if (!def.inspectorTabs.Contains(tabType))
                def.inspectorTabs.Add(tabType);
            if (def.inspectorTabsResolved == null)
                def.inspectorTabsResolved = new List<InspectTabBase>();
            if (!def.inspectorTabsResolved.Contains(tabInstance))
                def.inspectorTabsResolved.Add(tabInstance);
        }
    }
    
    public class ITab_PawnMedals : ITab
    {
        private Vector2 _scrollPosition;
        private const float MEDAL_ROW_HEIGHT = 80f;
        private const float ICON_SIZE = 64f;
        private const float PADDING = 10f;
        private const float TAB_WIDTH = 400f;
        private const float TAB_HEIGHT = 480f;

        public ITab_PawnMedals()
        {
            labelKey = "ROCKET_MedalsTab";
            size = new(TAB_WIDTH, TAB_HEIGHT);
        }

        public override bool IsVisible
        {
            get
            {
                var pawn = SelPawnForGear;
                return pawn is { apparel: not null };
            }
        }

        private Pawn SelPawnForGear
        {
            get
            {
                return SelThing switch
                {
                    Pawn p => p,
                    Corpse corpse => corpse.InnerPawn,
                    _ => null
                };
            }
        }

        private static List<RocketMedal> GetMedals(Pawn pawn) => 
            pawn.apparel.WornApparel.OfType<RocketMedal>().ToList();

        protected override void FillTab()
        {
            var pawn = SelPawnForGear;
            if (pawn == null) return;

            var medals = GetMedals(pawn);
            var outerRect = new Rect(0f, 0f, size.x, size.y).ContractedBy(PADDING);

            // --- Header ---
            var headerRect = new Rect(outerRect.x, outerRect.y, outerRect.width, 30f);
            Text.Font = GameFont.Medium;
            Widgets.Label(headerRect, "ROCKET_MedalsTab".Translate());
            Text.Font = GameFont.Small;

            // --- No medals state ---
            if (medals.Count == 0)
            {
                var emptyRect = new Rect(outerRect.x, headerRect.yMax + PADDING, outerRect.width, 40f);
                GUI.color = Color.gray;
                Widgets.Label(emptyRect, "ROCKET_NoMedals".Translate());
                GUI.color = Color.white;
                return;
            }

            // --- Scrollable medal list ---
            var listRect = new Rect(outerRect.x, headerRect.yMax + PADDING, outerRect.width, outerRect.height - 30f - PADDING);
            var totalHeight = 0f;
            foreach (var medal in medals)
                totalHeight += GetRowHeight(medal) + PADDING;
            var viewRect = new Rect(0f, 0f, listRect.width - 16f, totalHeight);

            Widgets.BeginScrollView(listRect, ref _scrollPosition, viewRect);

            var curY = 0f;
            foreach (var medal in medals) 
                DrawMedalRow(viewRect.width, ref curY, medal);

            Widgets.EndScrollView();
        }

        private float GetRowHeight(RocketMedal medal)
        {
            if (medal.citation.NullOrEmpty()) return MEDAL_ROW_HEIGHT;
            
            // Citation can be longer, so calculate the wrapped text height
            Text.Font = GameFont.Tiny;
            var textWidth = size.x - (PADDING * 4) - ICON_SIZE - 16f;
            var citationHeight = Text.CalcHeight($"\"{medal.citation}\"", textWidth);
            Text.Font = GameFont.Small;
            
            // name(24) + gap(2) + citation + gap(2) + stats(20) + padding(8)
            var contentHeight = 24f + 2f + citationHeight + 2f + 20f + 8f;
            return Mathf.Max(MEDAL_ROW_HEIGHT, contentHeight);
        }

        private void DrawMedalRow(float width, ref float curY, RocketMedal medal)
        {
            var rowHeight = GetRowHeight(medal);
            var rowRect = new Rect(0f, curY, width, rowHeight);

            if (Mouse.IsOver(rowRect))
                Widgets.DrawHighlight(rowRect);

            // --- Icon ---
            var iconRect = new Rect(rowRect.x + PADDING, rowRect.y + (rowHeight - ICON_SIZE) / 2f, ICON_SIZE, ICON_SIZE);
            Widgets.ThingIcon(iconRect, medal);

            // --- Text area to the right of the icon ---
            var textX = iconRect.xMax + PADDING;
            var textWidth = width - textX - PADDING;

            // Medal name
            var nameRect = new Rect(textX, rowRect.y + 4f, textWidth, 24f);
            Text.Font = GameFont.Small;
            Text.Anchor = TextAnchor.MiddleLeft;
            Widgets.Label(nameRect, medal.LabelCap);

            // Citation (yellow) OR description (grey)
            Text.Font = GameFont.Tiny;
            float descBottom;
            if (!medal.citation.NullOrEmpty())
            {
                var citationText = $"\"{medal.citation}\"";
                var citationHeight = Text.CalcHeight(citationText, textWidth);
                var citationRect = new Rect(textX, nameRect.yMax + 2f, textWidth, citationHeight);
                GUI.color = new Color(0.9f, 0.85f, 0.4f);
                Widgets.Label(citationRect, citationText);
                GUI.color = Color.white;
                descBottom = citationRect.yMax;
            }
            else
            {
                var descRect = new Rect(textX, nameRect.yMax + 2f, textWidth, 20f);
                GUI.color = Color.gray;
                Widgets.Label(descRect, (medal.def.description ?? "").Truncate(descRect.width));
                GUI.color = Color.white;
                descBottom = descRect.yMax;
            }

            // Stat bonuses summary
            var statsRect = new Rect(textX, descBottom + 2f, textWidth, 20f);
            var statText = GetStatSummary(medal);
            if (!statText.NullOrEmpty())
            {
                GUI.color = new Color(0.5f, 0.8f, 0.5f);
                Widgets.Label(statsRect, statText);
                GUI.color = Color.white;
            }

            Text.Font = GameFont.Small;
            Text.Anchor = TextAnchor.UpperLeft;

            // Tooltip always shows BOTH description and citation. might be confusing
            // TODO: make tooltip show only one of them?
            if (Mouse.IsOver(rowRect))
            {
                var sb = new StringBuilder();
                sb.Append(medal.LabelCap);
                sb.AppendLine();
                sb.AppendLine();
                sb.Append(medal.def.description);
                if (medal.BiocodeComp is { Biocoded: true, CodedPawn: not null })
                {
                    sb.AppendLine();
                    sb.Append("ROCKET_MedalAwardedTo".Translate(medal.BiocodeComp.CodedPawn.LabelShort));
                }
                if (!medal.citation.NullOrEmpty())
                {
                    sb.AppendLine();
                    sb.AppendLine();
                    sb.Append('"');
                    sb.Append(medal.citation);
                    sb.Append('"');
                }
                TooltipHandler.TipRegion(rowRect, sb.ToString());
            }

            curY += rowHeight + PADDING;
        }

        private string GetStatSummary(RocketMedal medal)
        {
            var offsets = medal.def.equippedStatOffsets;
            if (offsets == null || offsets.Count == 0) return null;

            var sb = new StringBuilder();
            for (var i = 0; i < offsets.Count; i++)
            {
                if (i > 0) sb.Append(", ");
                var val = offsets[i].value;
                sb.Append(val >= 0 ? "+" : "");
                sb.Append(val.ToStringPercent());
                sb.Append(' ');
                sb.Append(offsets[i].stat.LabelCap);
            }
            return sb.ToString();
        }
    }
}