using System.Collections.Generic;
using NUnit.Framework;
using Panoptes.Core.Application.Stores;
using Panoptes.Core.Domain;
using Panoptes.Presentation.Map;
using Panoptes.Presentation.UI.HUD;
using UnityEngine;

namespace Panoptes.Tests.EditMode.Presentation
{
    public sealed class UnitStackOverlayStateBuilderTests
    {
        private readonly List<GameObject> _objects = new List<GameObject>();

        [TearDown]
        public void TearDown()
        {
            for (var i = 0; i < _objects.Count; i++)
            {
                Object.DestroyImmediate(_objects[i]);
            }

            _objects.Clear();
        }

        [Test]
        public void Build_ShouldUseCatalogIdentityForSingleUnitType()
        {
            var builder = new UnitStackOverlayStateBuilder();
            var catalog = CreateCatalog();
            var units = new[]
            {
                CreateUnit("u1", "cavalry", 7, 10),
                CreateUnit("u2", "cavalry", 5, 10)
            };

            var state = builder.Build(units, catalog);

            Assert.That(state.HasUnits, Is.True);
            Assert.That(state.Count, Is.EqualTo(2));
            Assert.That(state.Hp, Is.EqualTo(12));
            Assert.That(state.MaxHp, Is.EqualTo(20));
            Assert.That(state.UnitType, Is.EqualTo("cavalry"));
            Assert.That(state.IconKey, Is.EqualTo("unit_cavalry"));
            Assert.That(state.DisplayName, Is.EqualTo("Cavalry"));
            Assert.That(state.IsMixed, Is.False);
        }

        [Test]
        public void Build_ShouldMarkMixedStacksAndKeepMajorityIcon()
        {
            var builder = new UnitStackOverlayStateBuilder();
            var catalog = CreateCatalog();
            var units = new[]
            {
                CreateUnit("u1", "cavalry", 7, 10),
                CreateUnit("u2", "archer", 4, 8),
                CreateUnit("u3", "cavalry", 10, 10)
            };

            var state = builder.Build(units, catalog);

            Assert.That(state.Count, Is.EqualTo(3));
            Assert.That(state.UnitType, Is.EqualTo("cavalry"));
            Assert.That(state.IconKey, Is.EqualTo("unit_cavalry"));
            Assert.That(state.DisplayName, Is.EqualTo("Mixed"));
            Assert.That(state.FallbackText, Is.EqualTo("M"));
            Assert.That(state.IsMixed, Is.True);
        }

        private UnitView CreateUnit(string unitId, string unitType, int hp, int maxHp)
        {
            var go = new GameObject(unitId);
            _objects.Add(go);
            var view = go.AddComponent<UnitView>();
            view.Bind(new UnitDto
            {
                Id = unitId,
                Type = unitType,
                Owner = "p1",
                Hp = hp,
                MaxHp = maxHp
            }, Vector3.zero);
            return view;
        }

        private static StaticCatalogState CreateCatalog()
        {
            return new StaticCatalogState(units: new Dictionary<string, CatalogUnitDto>
            {
                ["cavalry"] = new CatalogUnitDto
                {
                    Id = "cavalry",
                    Name = "Cavalry",
                    IconKey = "unit_cavalry"
                },
                ["archer"] = new CatalogUnitDto
                {
                    Id = "archer",
                    Name = "Archer",
                    IconKey = "unit_archer"
                }
            });
        }
    }
}
