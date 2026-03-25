using System.Collections.Generic;
using System.Globalization;
using System.Text;
using RimWorld;
using Verse;
using Verse.AI.Group;

namespace MedalMod
{
    public class RocketMedal : Apparel
    {
        public CompBiocodable BiocodeComp => field ??= this.GetComp<CompBiocodable>();
        private Pawn _cachedPawn = null;
            
        public string citation;
        public int ceremonyQuality = -1; // -1 = not yet awarded
        public bool isLocked = true;
        public Pawn awardedBy;
        public int awardedTick = -1;
            
        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref citation, "citation");
            Scribe_Values.Look(ref isLocked, "isLocked", true);
            Scribe_Values.Look(ref ceremonyQuality, "ceremonyQuality", -1);
            Scribe_References.Look(ref awardedBy, "awardedBy", true);
        }

        private void OpenCitationDialog()
        {
            Find.WindowStack.Add(new Dialog_WriteCitation(this));
        }

        public override string LabelNoCount
        {
            get
            {
                if (BiocodeComp is not { Biocoded: true, CodedPawn: not null }) return base.LabelNoCount;
                if (field != null && _cachedPawn == BiocodeComp.CodedPawn) return field;
                    
                _cachedPawn = BiocodeComp.CodedPawn;

                var cleanLabel = GenLabel.ThingLabel(this.def, this.Stuff, 1);
                if (AllComps != null)
                {
                    foreach (var comp in AllComps)
                    {
                        if (comp is CompBiocodable) continue; 
                        cleanLabel = comp.TransformLabel(cleanLabel);
                    }
                }
                var sb = new StringBuilder();
                sb.Append(_cachedPawn.LabelShort);
                sb.Append("'s");
                sb.Append(' ');
                sb.Append(cleanLabel);
                field = sb.ToString();
                return field;
            }
        } = null;
            
        public override IEnumerable<Gizmo> GetGizmos()
        {
            foreach (var gizmo in base.GetGizmos())
                yield return gizmo;

            var awardCeremonyBtn = new Command_Action
            {
                defaultLabel = "Award Ceremony",
                defaultDesc = "Gather the colony to officially bestow this medal upon a colonist.",
                icon = this.def.uiIcon, 
                action = StartMedalRitual
            };
                
            if (!ModsConfig.IdeologyActive)
                awardCeremonyBtn.Disable("Ideology DLC must be active to award medals.");
                
            if (BiocodeComp.Biocoded)
                awardCeremonyBtn.Disable("Medal has already been awarded.");

            // Check if the player actually has a leader in their colony to enable the button
            if (!ColonyHasPresenter(out _)) 
                awardCeremonyBtn.Disable("Requires a viable presenter.");
                
            yield return awardCeremonyBtn;
                
            var citationBtn = new Command_Action
            {
                defaultLabel = citation.NullOrEmpty() ? "Write Citation" : "Edit Citation",
                defaultDesc = "Write or edit the citation engraved on this medal.",
                icon = MedalTextures.CitationIcon,
                action = OpenCitationDialog
            };
                
            yield return citationBtn;
        }

        private bool ColonyHasPresenter(out Pawn result)
        {
            result = null;
            if (this.Map == null) return false;
            foreach (var p in this.Map.mapPawns.FreeColonistsSpawned)
            {
                var role = p.Ideo?.GetRole(p);
                if (role != null && (role.def == PreceptDefOf.IdeoRole_Leader || role.def == PreceptDefOf.IdeoRole_Moralist))
                {
                    result = p;
                    return true; 
                }
            }
            return false;
        }
            
        public string MedalLabel
        {
            get
            {
                var cleanLabel = GenLabel.ThingLabel(this.def, this.Stuff, 1);
                return CultureInfo.CurrentCulture.TextInfo.ToTitleCase(cleanLabel);
            }
        }

        private void StartMedalRitual()
        {
            if (!ColonyHasPresenter(out Pawn presenter)) return;
            var pattern = DefDatabase<RitualPatternDef>.GetNamedSilentFail("ROCKET_AwardMedalPattern");
            var dummyPreceptDef = DefDatabase<PreceptDef>.GetNamedSilentFail("ROCKET_MedalCeremonyPrecept");

            if (pattern == null || dummyPreceptDef == null)
            {
                Log.Error("[MedalMod] Missing Ritual Defs! Check your XML names.");
                return;
            }

            var safeMap = this.Map ?? this.MapHeld;
            if (safeMap == null) return;

            var ritualTarget = new TargetInfo(this);

            var fakeRitual = (Precept_Ritual)PreceptMaker.MakePrecept(dummyPreceptDef);
            fakeRitual.ideo = presenter.Ideo;
            fakeRitual.sourcePattern = pattern;
            var nameValue = MedalLabel;
            var ceremonyName = $"{nameValue} Award Ceremony";
            fakeRitual.SetName("Award Ceremony");
                
            fakeRitual.behavior = pattern.ritualBehavior.GetInstance();
            fakeRitual.behavior.def = pattern.ritualBehavior;
            fakeRitual.outcomeEffect = pattern.ritualOutcomeEffect.GetInstance();
            fakeRitual.outcomeEffect.def = pattern.ritualOutcomeEffect;
            fakeRitual.outcomeEffect.compDatas ??= new();
                
            Dialog_BeginRitual.ActionCallback startAction = delegate (RitualRoleAssignments assignments)
            {
                LordJob_Ritual lordJob = new LordJob_Ritual(
                    selectedTarget: ritualTarget,
                    ritual: fakeRitual,
                    obligation: null,
                    allStages: pattern.ritualBehavior.stages,
                    assignments: assignments,
                    organizer: null,
                    spotOverride: null
                );
                LordMaker.MakeNewLord(Faction.OfPlayer, lordJob, safeMap, assignments.Participants);
                return true; 
            };

            var outcomeDef = pattern.ritualOutcomeEffect;

            Find.WindowStack.Add(new Dialog_BeginRitual(
                ritualLabel: ceremonyName,
                ritual: fakeRitual,       
                target: ritualTarget,     
                map: safeMap,             
                action: startAction,      
                organizer: null,
                obligation: null,         
                filter: null,
                okButtonText: "Begin Ceremony",
                requiredPawns: null,
                forcedForRole: null,
                outcomeDef        
            ));
        } 
        
    public override string GetInspectString()
    {
        var sb = new StringBuilder(base.GetInspectString());

        // Awarded to (biocoded pawn)
        if (BiocodeComp is { Biocoded: true, CodedPawn: not null })
        {
            if (sb.Length > 0) sb.AppendLine();
            sb.Append("Awarded to: ");
            sb.Append(BiocodeComp.CodedPawn.LabelShort);
        }

        // Awarded by
        if (awardedBy != null)
        {
            if (sb.Length > 0) sb.AppendLine();
            sb.Append("Presented by: ");
            sb.Append(awardedBy.LabelShort);
        }

        // Date
        if (awardedTick >= 0)
        {
            if (sb.Length > 0) sb.AppendLine();
            sb.Append("Awarded: ");
            sb.Append(GenDate.DateFullStringAt(
                GenDate.TickGameToAbs(awardedTick),
                Find.WorldGrid.LongLatOf(
                    Wearer?.Map?.Tile ?? Find.CurrentMap?.Tile ?? 0)));
        }

        // Ceremony quality
        if (ceremonyQuality >= 0)
        {
            if (sb.Length > 0) sb.AppendLine();
            sb.Append("Ceremony: ");
            sb.Append(CeremonyQuality.GetQualityLabel(ceremonyQuality).CapitalizeFirst());
        }

        // Citation
        if (!citation.NullOrEmpty())
        {
            if (sb.Length > 0) sb.AppendLine();
            sb.Append('"');
            sb.Append(citation);
            sb.Append('"');
        }

        return sb.ToString();
    }

    // Custom ITab for the full medal history card

    }
}
