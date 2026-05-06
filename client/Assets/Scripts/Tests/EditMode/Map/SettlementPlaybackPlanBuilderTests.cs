using System.Collections.Generic;
using NUnit.Framework;
using Panoptes.Core.Domain;
using Panoptes.Core.Infrastructure.Mapper;
using Panoptes.Presentation.Map;
using Panoptes.Protocol.V1;

namespace Panoptes.Tests.EditMode.Map
{
    public sealed class SettlementPlaybackPlanBuilderTests
    {
        [Test]
        public void SettlementMapperKeepsDamagedUnitSeparateFromAttacker()
        {
            var msg = new MsgGameSync
            {
                Phase = "resolving"
            };
            var evt = new DomainEventEnvelope
            {
                Channel = "unit",
                Kind = "unit_damaged"
            };
            evt.Data.Add("unit_id", "defender-1");
            evt.Data.Add("attacker", "attacker-1");
            evt.Data.Add("damage", "3");
            evt.Data.Add("hp_after", "7");
            msg.Events.Add(evt);

            var settlement = SettlementMapper.ToDto(msg);
            var mapped = settlement.Sections[0].Events[0];

            Assert.AreEqual("defender-1", mapped.UnitId);
            Assert.AreEqual("attacker-1", mapped.AttackerUnitId);
        }

        [Test]
        public void SettlementMapperReadsTypedUnitMovedCoordinates()
        {
            var msg = new MsgGameSync
            {
                Phase = "resolving"
            };
            msg.Events.Add(new DomainEventEnvelope
            {
                Channel = "unit",
                Kind = "unit_moved",
                UnitMoved = new DomainUnitMovedEvent
                {
                    UnitId = "unit-a",
                    FromQ = 1,
                    FromR = 2,
                    ToQ = 3,
                    ToR = 4
                }
            });

            var settlement = SettlementMapper.ToDto(msg);
            var mapped = settlement.Sections[0].Events[0];

            Assert.AreEqual("unit-a", mapped.UnitId);
            Assert.AreEqual(1, mapped.FromQ);
            Assert.AreEqual(2, mapped.FromR);
            Assert.AreEqual(3, mapped.ToQ);
            Assert.AreEqual(4, mapped.ToR);
        }

        [Test]
        public void BuildAttachesMoveAndDamageToSameActorStep()
        {
            var settlement = Settlement(
                Move("attacker-1", 0, 0, 1, 0),
                Damage("defender-1", "attacker-1"));

            var steps = SettlementPlaybackPlanBuilder.Build(settlement);

            Assert.AreEqual(1, steps.Count);
            Assert.AreEqual("attacker-1", steps[0].ActorUnitId);
            Assert.IsNotNull(steps[0].MoveEvent);
            Assert.AreEqual(1, steps[0].ImpactEvents.Count);
            Assert.AreEqual(1, steps[0].Impacts.Count);
            Assert.AreEqual("defender-1", steps[0].ImpactEvents[0].UnitId);
        }

        [Test]
        public void BuildKeepsMultipleMovesSequential()
        {
            var settlement = Settlement(
                Move("unit-a", 0, 0, 1, 0),
                Move("unit-b", 2, 0, 3, 0));

            var steps = SettlementPlaybackPlanBuilder.Build(settlement);

            Assert.AreEqual(2, steps.Count);
            Assert.AreEqual("unit-a", steps[0].ActorUnitId);
            Assert.AreEqual("unit-b", steps[1].ActorUnitId);
        }

        [Test]
        public void BuildCreatesAttackOnlyStep()
        {
            var settlement = Settlement(Damage("defender-1", "attacker-1"));

            var steps = SettlementPlaybackPlanBuilder.Build(settlement);

            Assert.AreEqual(1, steps.Count);
            Assert.AreEqual("attacker-1", steps[0].ActorUnitId);
            Assert.IsNull(steps[0].MoveEvent);
            Assert.AreEqual(1, steps[0].ImpactEvents.Count);
            Assert.AreEqual(1, steps[0].Impacts.Count);
        }

        [Test]
        public void BuildDoesNotInventActorForDamageWithoutAttacker()
        {
            var settlement = Settlement(Damage("defender-1", string.Empty));

            var steps = SettlementPlaybackPlanBuilder.Build(settlement);

            Assert.AreEqual(1, steps.Count);
            Assert.IsFalse(steps[0].HasActor);
            Assert.AreEqual("defender-1", steps[0].ImpactEvents[0].UnitId);
        }

        [Test]
        public void BuildMergesDamageAndDeathForSameTargetVisual()
        {
            var settlement = Settlement(
                Damage("defender-1", "attacker-1"),
                Death("defender-1", "attacker-1"));

            var steps = SettlementPlaybackPlanBuilder.Build(settlement);

            Assert.AreEqual(1, steps.Count);
            Assert.AreEqual(2, steps[0].ImpactEvents.Count);
            Assert.AreEqual(1, steps[0].Impacts.Count);
            Assert.AreSame(steps[0].ImpactEvents[0], steps[0].Impacts[0].DamageEvent);
            Assert.AreSame(steps[0].ImpactEvents[1], steps[0].Impacts[0].DeathEvent);
        }

        private static TurnSettlementDto Settlement(params TurnEventDto[] events)
        {
            return new TurnSettlementDto
            {
                Sections = new List<SettlementSectionDto>
                {
                    new SettlementSectionDto
                    {
                        Section = "unit",
                        Events = new List<TurnEventDto>(events)
                    }
                }
            };
        }

        private static TurnEventDto Move(string unitId, int fromQ, int fromR, int toQ, int toR)
        {
            return new TurnEventDto
            {
                Type = "unit_moved",
                UnitId = unitId,
                FromQ = fromQ,
                FromR = fromR,
                ToQ = toQ,
                ToR = toR
            };
        }

        private static TurnEventDto Damage(string unitId, string attackerUnitId)
        {
            return new TurnEventDto
            {
                Type = "unit_damaged",
                UnitId = unitId,
                AttackerUnitId = attackerUnitId,
                Damage = 3,
                HpAfter = 7
            };
        }

        private static TurnEventDto Death(string unitId, string attackerUnitId)
        {
            return new TurnEventDto
            {
                Type = "unit_died",
                UnitId = unitId,
                AttackerUnitId = attackerUnitId,
                Damage = 7,
                HpAfter = 0
            };
        }
    }
}
