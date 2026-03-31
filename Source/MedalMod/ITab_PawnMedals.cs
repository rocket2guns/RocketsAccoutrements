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
        private const float MEDAL_ROW_HEIGHT = 90f;
        private const float ICON_SIZE = 80f;
        private const float PADDING = 10f;
        private const float TAB_WIDTH = 400f;
        private const float TAB_HEIGHT = 480f;

        public ITab_PawnMedals()
        {
            labelKey = "ROCKET_MedalsTab";
            size = new(TAB_WIDTH, TAB_HEIGHT);
        }

        public override bool IsVisible => HasMedals(SelPawnForGear);

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
        
        private bool HasAwardInfo(RocketMedal medal) => medal.awardedBy != null || medal.awardedTick >= 0;

        private string GetAwardInfo(RocketMedal medal)
        {
            var sb = new StringBuilder();
            if (medal.awardedBy != null)
            {
                sb.Append("Presented by ");
                sb.Append(medal.awardedBy.LabelShort);
            }
            if (medal.awardedTick >= 0)
            {
                if (sb.Length > 0) sb.Append(" on ");
                sb.Append(GenDate.DateFullStringAt(
                    GenDate.TickGameToAbs(medal.awardedTick),
                    Find.WorldGrid.LongLatOf(Find.CurrentMap.Tile)
                ));
            }
            return sb.ToString();
        }

        private bool HasMedals(Pawn pawn)
        {
            if (pawn?.apparel == null) return false;
            foreach (var apparel in pawn.apparel.WornApparel)
            {
                if (apparel is RocketMedal)
                    return true;
            }
            return false;
        }

        protected override void FillTab()
        {
            var pawn = SelPawnForGear;
            if (pawn == null) return;

            var outerRect = new Rect(0f, 0f, size.x, size.y).ContractedBy(PADDING);

            var headerRect = new Rect(outerRect.x, outerRect.y, outerRect.width, 30f);
            Text.Font = GameFont.Medium;
            Widgets.Label(headerRect, "ROCKET_MedalsTab".Translate());
            Text.Font = GameFont.Small;

            if (!HasMedals(pawn))
            {
                var emptyRect = new Rect(outerRect.x, headerRect.yMax + PADDING, outerRect.width, 40f);
                GUI.color = Color.gray;
                Widgets.Label(emptyRect, "ROCKET_NoMedals".Translate());
                GUI.color = Color.white;
                return;
            }

            var listRect = new Rect(outerRect.x, headerRect.yMax + PADDING, outerRect.width, outerRect.height - 30f - PADDING);
            var viewWidth = listRect.width - 16f; // scrollbar

            var totalHeight = 0f;
            foreach (var apparel in pawn.apparel.WornApparel)
                if (apparel is RocketMedal medal)
                    totalHeight += GetRowHeight(medal, viewWidth) + PADDING;
            var viewRect = new Rect(0f, 0f, viewWidth, totalHeight);

            Widgets.BeginScrollView(listRect, ref _scrollPosition, viewRect);

            var curY = 0f;
            foreach (var apparel in pawn.apparel.WornApparel)
                if (apparel is RocketMedal medal)
                    DrawMedalRow(viewWidth, ref curY, medal);

            Widgets.EndScrollView();
        }

        private float GetRowHeight(RocketMedal medal, float width)
        {
            var textWidth = GetTextWidth(width);
            var height = 4f;

            Text.Font = GameFont.Small;
            height += Text.CalcHeight(medal.MedalLabel, textWidth) + 2f;

            Text.Font = GameFont.Tiny;
            if (!medal.citation.NullOrEmpty())
            {
                height += Text.CalcHeight($"\"{medal.citation}\"", textWidth) + 2f;
            }
            else
            {
                height += 20f + 2f;
            }

            var statText = GetStatSummary(medal);
            if (!statText.NullOrEmpty())
            {
                height += Text.CalcHeight(statText, textWidth);
            }
            
            if (HasAwardInfo(medal))
            {
                height += Text.CalcHeight(GetAwardInfo(medal), textWidth) + 2f;
            }
            
            var ext = medal.def.GetModExtension<MedalExtension>();
            var honor = ext?.honorAwarded ?? 0;
            if (honor > 0)
                height += Text.CalcHeight("0", textWidth) + 2f;

            Text.Font = GameFont.Small;
            height += 8f;
            return Mathf.Max(MEDAL_ROW_HEIGHT, height);
        }
        
        private float GetTextWidth(float availableWidth)
        {
            return availableWidth - ICON_SIZE - (PADDING * 3) - LOCK_BTN_SIZE - PADDING;
        }

        private const float LOCK_BTN_SIZE = 24f;

        private void DrawMedalRow(float width, ref float curY, RocketMedal medal)
        {
            var rowHeight = GetRowHeight(medal, width);
            var rowRect = new Rect(0f, curY, width, rowHeight);

            if (Mouse.IsOver(rowRect))
                Widgets.DrawHighlight(rowRect);

            // Lock toggle
            var lockRect = new Rect(
                rowRect.xMax - LOCK_BTN_SIZE - PADDING,
                rowRect.y + 4f,
                LOCK_BTN_SIZE,
                LOCK_BTN_SIZE
            );
            var lockIcon = medal.isLocked ?  MedalTextures.LockedIcon : MedalTextures.UnlockedIcon;
            var lockTip = medal.isLocked
                ? "ROCKET_MedalLocked".Translate()
                : "ROCKET_MedalUnlocked".Translate();

            GUI.color = Color.gray;
            if (Widgets.ButtonImage(lockRect, lockIcon, GUI.color))
                medal.isLocked = !medal.isLocked;
            GUI.color = Color.white;
            TooltipHandler.TipRegion(lockRect, lockTip);

            // Icon
            var iconRect = new Rect(rowRect.x + PADDING, rowRect.y + (rowHeight - ICON_SIZE) / 2f, ICON_SIZE, ICON_SIZE);
            Widgets.ThingIcon(iconRect, medal);

            var textX = iconRect.xMax + PADDING;
            var textWidth = GetTextWidth(width);

            // Medal name
            Text.Font = GameFont.Small;
            Text.Anchor = TextAnchor.MiddleLeft;
            var nameHeight = Text.CalcHeight(medal.MedalLabel, textWidth);
            var nameRect = new Rect(textX, rowRect.y + 4f, textWidth, nameHeight);
            Widgets.Label(nameRect, medal.MedalLabel);

            // Citation
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
            
            var statsBottom = descBottom;
            var ext = medal.def.GetModExtension<MedalExtension>();
            var honor = ext?.honorAwarded ?? 0;
            if (ModsConfig.RoyaltyActive && honor > 0)
            {
                Text.Font = GameFont.Tiny;
                var iconSize = 14f;
                var crownRect = new Rect(textX, descBottom + 4f, iconSize, iconSize);
                var labelRect = new Rect(crownRect.xMax + 4f, descBottom + 2f, textWidth - iconSize - 4f, Text.CalcHeight($"+{honor}", textWidth));
                GUI.color = Dialog_MedalAwarded.GoldColor;
                if (MedalTextures.HonorIcon != null)
                    GUI.DrawTexture(crownRect, MedalTextures.HonorIcon);
                Widgets.Label(labelRect, $"honor +{honor}");
                GUI.color = Color.white;
                statsBottom = labelRect.yMax;
            }

            // Stat bonuses summary
            var statText = GetStatSummary(medal);
            if (!statText.NullOrEmpty())
            {
                Text.Font = GameFont.Tiny;
                var statHeight = Text.CalcHeight(statText, textWidth);
                var statsRect = new Rect(textX, statsBottom + 2f, textWidth, statHeight);
                GUI.color = new Color(0.5f, 0.8f, 0.5f);
                Widgets.Label(statsRect, statText);
                GUI.color = Color.white;
                statsBottom = statsRect.yMax;
            }
            
            // Award info
            if (HasAwardInfo(medal))
            {
                Text.Font = GameFont.Tiny;
                var awardText = GetAwardInfo(medal);
                var awardHeight = Text.CalcHeight(awardText, textWidth);
                var awardRect = new Rect(textX, statsBottom + 2f, textWidth, awardHeight);
                GUI.color = Color.gray;
                Widgets.Label(awardRect, awardText);
                GUI.color = Color.white;
            }
            
            Text.Font = GameFont.Small;
            Text.Anchor = TextAnchor.UpperLeft;

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
                sb.Append(offsets[i].stat.ValueToString(val));
                sb.Append(' ');
                sb.Append(offsets[i].stat.LabelCap);
            }
            return sb.ToString();
        }
    }
}