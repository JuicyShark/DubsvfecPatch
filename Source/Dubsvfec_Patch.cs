using System.Collections.Generic;
using System.Linq;
using DubsBadHygiene;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;
using VFEC.Buildings;
namespace DubsvfecPatch
{
    public class Building_ThermaePatch : Building, IBuildingPawns
    {
        private Building_SteamGeyser geyser;

        private List<Pawn> occupants = new List<Pawn>();
        public List<Pawn> Occupants()
        {
            return occupants;
        }

        public void Notify_Entered(Pawn pawn)
        {
            occupants.Add(pawn);
        }

        public void Notify_Left(Pawn pawn)
        {
            occupants.Remove(pawn);
        }

        public override void Tick()
        {
            base.Tick();
            if (geyser == null)
            {
                geyser = (Building_SteamGeyser)base.Map.thingGrid.ThingAt(base.Position, ThingDefOf.SteamGeyser);
            }
            if (geyser != null)
            {
                geyser.harvester = this;
            }
            if (this.IsHashIntervalTick(Rand.Range(5, 10)))
            {
                CellRect cellRect = this.OccupiedRect();
                FleckMaker.ThrowAirPuffUp(new Vector3(Rand.Range(cellRect.minX, cellRect.maxX), DrawPos.y, Rand.Range(cellRect.minZ, cellRect.maxX)), base.Map);
            }
            if (occupants.Count > 5 && this.IsHashIntervalTick(250))
            {
                Pawn pawn = occupants.RandomElement();
                Pawn pawn2 = occupants.Except(pawn).RandomElement();
                FleckMaker.ThrowMetaIcon(pawn.Position, pawn.Map, FleckDefOf.Heart);
                FleckMaker.ThrowMetaIcon(pawn2.Position, pawn2.Map, FleckDefOf.Heart);
                Thought_Memory thought_Memory = (Thought_Memory)ThoughtMaker.MakeThought(ThoughtDefOf.GotSomeLovin);
                Pawn_HealthTracker health = pawn.health;
                if ((health != null && health.hediffSet != null && pawn.health.hediffSet.hediffs.Any((Hediff h) => h.def == HediffDefOf.LoveEnhancer)) || (pawn2.health?.hediffSet != null && pawn2.health.hediffSet.hediffs.Any((Hediff h) => h.def == HediffDefOf.LoveEnhancer)))
                {
                    thought_Memory.moodPowerFactor = 1.5f;
                }
                pawn.needs.mood?.thoughts.memories.TryGainMemory(thought_Memory, pawn2);
                Find.HistoryEventsManager.RecordEvent(new HistoryEvent(HistoryEventDefOf.GotLovin, pawn.Named(HistoryEventArgsNames.Doer)));
                Find.HistoryEventsManager.RecordEvent(new HistoryEvent(pawn.relations.DirectRelationExists(PawnRelationDefOf.Spouse, pawn2) ? HistoryEventDefOf.GotLovin_Spouse : HistoryEventDefOf.GotLovin_NonSpouse, pawn.Named(HistoryEventArgsNames.Doer)));
            }
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Collections.Look(ref occupants, "occupants", LookMode.Reference);
        }
    }
    public class JoyGiver_UseThermae : JoyGiver_InteractBuilding
    {
        protected override Job TryGivePlayJob(Pawn pawn, Thing bestGame)
        {
            return JobMaker.MakeJob(targetB: bestGame.OccupiedRect().EdgeCells.Where((IntVec3 c) => c.GetFirstThing<Pawn>(pawn.Map) == null).RandomElement(), def: def.jobDef, targetA: bestGame);
        }
    }
    public class JobDriver_UseThermae : JobDriver
    {
        public override Vector3 ForcedBodyOffset => new Vector3(0.75f, 0f, 0f).RotatedBy(pawn.Position.ToVector3().AngleToFlat(base.TargetThingA.TrueCenter()));
        public override bool TryMakePreToilReservations(bool errorOnFailed)
        {
            return pawn.Reserve(job.targetA, job, job.def.joyMaxParticipants, 0, null, errorOnFailed) && pawn.Reserve(job.targetB, job, 1, 0, null, errorOnFailed);
        }
        public void chillticker()
        {
            Pawn actor = GetActor();
            actor.Drawer.renderer.graphics.ClearCache();
            actor.Drawer.renderer.graphics.apparelGraphics.Clear();
            pawn.rotationTracker.Face(base.TargetThingA.TrueCenter());
            actor.GainComfortFromCellIfPossible();
            SanitationUtil.Hydrotherapy(pawn, (Thermaebath)base.TargetThingA);
            pawn.needs.TryGetNeed<Need_Hygiene>()?.clean(0.0001f);
            if (!pawn.IsPrisoner)
            {
                JoyUtility.JoyTickCheckEnd(pawn, job.doUntilGatheringEnded ? JoyTickFullJoyAction.None : JoyTickFullJoyAction.EndJob, 1f, (Thermaebath)base.TargetThingA);
            }
        }
        protected override IEnumerable<Toil> MakeNewToils()
        {
            this.EndOnDespawnedOrNull(TargetIndex.A);
            yield return Toils_Goto.GotoCell(TargetIndex.B, PathEndMode.OnCell);
            Toil toil = new Toil
            {
                initAction = delegate
                {
                    if (base.TargetThingA is IBuildingPawns buildingPawns2)
                    {
                        buildingPawns2.Notify_Entered(pawn);
                    }
                },
                tickAction = delegate
                {
                    chillticker();
                },
                defaultCompleteMode = ToilCompleteMode.Delay,
                defaultDuration = (job.doUntilGatheringEnded ? job.expiryInterval : job.def.joyDuration)
            };
            toil.AddFinishAction(delegate
            {
                if (base.TargetThingA is IBuildingPawns buildingPawns)
                {
                    buildingPawns.Notify_Left(pawn);
                    pawn.Drawer.renderer.graphics.ResolveApparelGraphics();

                }
            });
            yield return toil;
        }

        public override object[] TaleParameters()
        {
            return new object[2]
            {
            pawn,
            base.TargetA.Thing.def
            };
        }
    }
}