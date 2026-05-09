/*************************************************
 * Project: Panoptes
 * File: StaticCatalogCache.cs
 * Author: Panoptes Team
 * Date: 2026-04-07
 * Description: Unified static catalog cache backed by generated JSON bundles.
 *************************************************/

using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using Panoptes.Protocol.V1;
using UnityEngine;

namespace Panoptes.Core.Application.Cache
{
    public sealed class StaticCatalogCache : MonoBehaviour
    {
        [Serializable]
        public sealed class ManifestJson
        {
            public string schema_version;
            public string content_version;
            public string default_locale;
            public string default_map_id;
            public string bundle_hash;
            public string[] required_sections;
            public CatalogSectionHashJson[] section_hashes;
        }

        [Serializable]
        public sealed class CatalogSectionHashJson
        {
            public string section_name;
            public string hash;
        }

        [Serializable]
        public sealed class ResourceEntryJson
        {
            public string key;
            public string display_name;
            public string description;
            public string icon_key;
            public int sort_order;
            public bool visible_in_hud;
        }

        [Serializable]
        public sealed class PointEntryJson
        {
            public string key;
            public string display_name;
            public string description;
            public string icon_key;
            public int sort_order;
            public bool visible_in_hud;
        }

        [Serializable]
        public sealed class BuildingEntryJson
        {
            public string id;
            public string name;
            public string description;
            public string icon_key;
            public string prefab_key;
            public string placement_kind;
            public string building_scope;
            public string required_resource_type;
            public string takeover_mode;
            public int sort_order;
            public string default_recipe_id;
            public IntAmountEntryJson[] resource_costs;
            public IntAmountEntryJson[] point_costs;
            public string[] recipe_ids;
            public string[] tags;
            public int max_hp;
        }

        [Serializable]
        public sealed class TechnologyEntryJson
        {
            public string id;
            public string name;
            public string description;
            public string icon_key;
            public string branch;
            public int tier;
            public int research_cost;
            public int sort_order;
            public string[] tags;
            public PrerequisiteEntryJson[] prerequisites;
            public ExplicitEffectEntryJson[] explicit_effects;
        }

        [Serializable]
        public sealed class ExplicitEffectEntryJson
        {
            public string type;
            public string target_id;
            public int institution_slots;
        }

        [Serializable]
        public sealed class PrerequisiteEntryJson
        {
            public string type;
            public string target_id;
        }

        [Serializable]
        public sealed class PolicyEntryJson
        {
            public string id;
            public string name;
            public string description;
            public string icon_key;
            public string layer;
            public string activation_timing;
            public ModifierEffectEntryJson[] modifier_effects;
        }

        [Serializable]
        public sealed class InstitutionCategoryEntryJson
        {
            public string id;
            public string name;
            public string description;
            public int sort_order;
            public string[] tags;
        }

        [Serializable]
        public sealed class InstitutionEntryJson
        {
            public string id;
            public string name;
            public string description;
            public string icon_key;
            public string category;
            public string activation_timing;
            public int sort_order;
            public string[] tags;
            public PrerequisiteEntryJson[] prerequisites;
            public ExplicitEffectEntryJson[] explicit_effects;
            public ModifierEffectEntryJson[] modifier_effects;
        }

        [Serializable]
        public sealed class ModifierEffectEntryJson
        {
            public string trigger;
            public string target_id;
            public string resource_key;
            public string point_key;
            public string modifier_type;
            public int value;
        }

        [Serializable]
        public sealed class RecipeEntryJson
        {
            public string id;
            public string name;
            public string description;
            public string icon_key;
            public string building_id;
            public int work_amount;
            public int base_progress;
            public int sort_order;
            public string[] tags;
            public IntAmountEntryJson[] resource_inputs;
            public IntAmountEntryJson[] point_inputs;
            public RecipeOutputsJson outputs;
        }

        [Serializable]
        public sealed class RecipeOutputsJson
        {
            public IntAmountEntryJson[] resources;
            public string[] units;
            public IntAmountEntryJson[] point_progress;
            public IntAmountEntryJson[] state_changes;
        }

        [Serializable]
        public sealed class IntAmountEntryJson
        {
            public string key;
            public int amount;
        }

        [Serializable]
        public sealed class UnitEntryJson
        {
            [Serializable]
            public sealed class UnitFlagsJson
            {
                public bool can_attack_structures;
                public bool can_destroy_road;
            }

            public string id;
            public string name;
            public string description;
            public string icon_key;
            public string prefab_key;
            public string @class;
            public int max_hp;
            public int attack;
            public int attack_range;
            public int move_range;
            public int vision_range;
            public int road_speed_bonus;
            public float charge_bonus;
            public UnitFlagsJson flags;
            public string[] tags;
        }

        [Serializable]
        public sealed class TerrainEntryJson
        {
            public string id;
            public string name;
            public string description;
            public string icon_key;
            public string material_key;
            public int move_cost_no_road;
            public bool passable;
            public bool passable_with_road;
            public bool blocks_cavalry;
            public int sort_order;
            public string[] tags;
        }

        [Serializable]
        public sealed class EmoteSeriesEntryJson
        {
            public string id;
            public string display_name;
            public string icon_key;
            public int sort_order;
        }

        [Serializable]
        public sealed class EmoteEntryJson
        {
            public string id;
            public string series_id;
            public string display_name;
            public string asset_key;
            public int sort_order;
            public string[] tags;
        }

        [Serializable]
        public sealed class MapEntryJson
        {
            public string id;
            public string name;
            public string description;
            public string thumbnail_key;
            public int width;
            public int height;
        }

        [Serializable]
        public sealed class RulesJson
        {
            public int turn_time_limit_planning;
            public int tokens_per_turn;
            public int bonus_tokens_per_turn;
            public int max_turns;
            public int city_core_max_hp;
            public int safe_zone_radius;
            public int facility_takeover_turns;
            public int base_research_output_per_turn;
            public int base_industry_output_per_turn;
            public int minimum_city_distance;
            public int initial_city_territory_radius;
        }

        [Serializable]
        public sealed class MinisterJson
        {
            public string id;
            public string name;
            public string role;
            public string icon_key;
            public int ability;
            public string personality;
            public string personality_desc;
            public int loyalty;
            public int ambition;
            public int cautiousness;
            public int decisiveness;
            public int loyalty_tendency;
            public int ambition_style;
        }

        [Serializable]
        public sealed class MinisterSkillCardJson
        {
            public string id;
            public string name;
            public string description;
            public string icon_key;
            public string[] role_tags;
            public string rarity;
            public string effect_key;
            public string trigger_timing;
            public int delay_turns;
            public int duration_turns;
            public int sort_order;
            public string[] tags;
        }

        [Serializable]
        public sealed class TechTreeLayoutPointJson
        {
            public float x;
            public float y;
        }

        [Serializable]
        public sealed class TechTreeLayoutNodeJson
        {
            public string id;
            public string technology_id;
            public string title;
            public string description;
            public float x;
            public float y;
            public float width;
            public float height;
            public bool visible;
        }

        [Serializable]
        public sealed class TechTreeLayoutEdgeJson
        {
            public string id;
            public string from;
            public string to;
            public bool show_arrow;
            public string arrow;
            public float thickness;
            public TechTreeLayoutPointJson[] points;
        }

        [Serializable]
        public sealed class TechTreeLayoutJson
        {
            public string config_version;
            public TechTreeLayoutNodeJson[] nodes;
            public TechTreeLayoutEdgeJson[] edges;
        }

        [Serializable]
        public sealed class BuildMenuLayoutJson
        {
            public string config_version;
            public string[] building_order;
            public string[] hidden_building_ids;
        }

        [Serializable]
        public sealed class RecipeLayoutJson
        {
            public string config_version;
            public string[] recipe_order;
        }

#pragma warning disable CS0649
        [Serializable]
        private sealed class CatalogBundleJson
        {
            public ManifestJson manifest;
            public ResourceEntryJson[] resources;
            public PointEntryJson[] points;
            public UnitEntryJson[] units;
            public BuildingEntryJson[] buildings;
            public TechnologyEntryJson[] technologies;
            public PolicyEntryJson[] policies;
            public InstitutionCategoryEntryJson[] institution_categories;
            public InstitutionEntryJson[] institutions;
            public RecipeEntryJson[] recipes;
            public TerrainEntryJson[] terrains;
            public EmoteSeriesEntryJson[] emote_series;
            public EmoteEntryJson[] emotes;
            public RulesJson rules;
            public MinisterJson[] ministers;
            public MinisterSkillCardJson[] minister_skill_cards;
            public MapEntryJson[] maps;
            public TechTreeLayoutJson ui_tech_tree_layout;
            public BuildMenuLayoutJson ui_build_menu_layout;
            public RecipeLayoutJson ui_recipe_layout;
        }
#pragma warning restore CS0649

        [Serializable]
        private sealed class ResourcesSectionJson
        {
            public ResourceEntryJson[] resources;
        }

        [Serializable]
        private sealed class PointsSectionJson
        {
            public PointEntryJson[] points;
        }

        [Serializable]
        private sealed class UnitsSectionJson
        {
            public UnitEntryJson[] units;
        }

        [Serializable]
        private sealed class BuildingsSectionJson
        {
            public BuildingEntryJson[] buildings;
        }

        [Serializable]
        private sealed class TechnologiesSectionJson
        {
            public TechnologyEntryJson[] technologies;
        }

        [Serializable]
        private sealed class PoliciesSectionJson
        {
            public PolicyEntryJson[] policies;
        }

        [Serializable]
        private sealed class InstitutionsSectionJson
        {
            public InstitutionCategoryEntryJson[] categories;
            public InstitutionEntryJson[] institutions;
        }

        [Serializable]
        private sealed class RecipesSectionJson
        {
            public RecipeEntryJson[] recipes;
        }

        [Serializable]
        private sealed class TerrainsSectionJson
        {
            public TerrainEntryJson[] terrains;
        }

        [Serializable]
        private sealed class EmotesSectionJson
        {
            public EmoteSeriesEntryJson[] series;
            public EmoteEntryJson[] emotes;
        }

        [Serializable]
        private sealed class RulesSectionJson
        {
            public RulesJson rules;
        }

        [Serializable]
        private sealed class MinistersSectionJson
        {
            public MinisterJson[] ministers;
        }

        [Serializable]
        private sealed class MinisterSkillCardsSectionJson
        {
            public MinisterSkillCardJson[] minister_skill_cards;
        }

        [Serializable]
        private sealed class MapsSectionJson
        {
            public MapEntryJson[] maps;
        }

        [Serializable]
        public sealed class MapRuntimeNodeJson
        {
            public string id;
            public int x;
            public int y;
            public string terrain;
            public bool has_road;
            public bool is_resource_point;
            public string resource_type;
            public string node_name;
            public string owner;
            public string territory_owner;
            public string building_type;
            public int building_hp;
        }

        [Serializable]
        public sealed class MapRuntimeBundleJson
        {
            public string id;
            public string name;
            public int width;
            public int height;
            public MapRuntimeNodeJson[] nodes;
        }

        public sealed class CatalogSyncDecision
        {
            public string ServerBundleHash = string.Empty;
            public bool ForceFullSync;
            public bool HashMatches;
            public string[] RequestedSections = Array.Empty<string>();

            public bool RequiresSync => ForceFullSync || RequestedSections.Length > 0 || !HashMatches;
        }

        private sealed class SectionSyncAccumulator
        {
            public string SectionName;
            public string SectionHash;
            public string Compression;
            public int ChunkCount;
            public readonly Dictionary<int, byte[]> Chunks = new();
        }

        public static StaticCatalogCache Instance { get; private set; }

        [SerializeField] private bool autoLoadOnAwake = true;
        [SerializeField] private string catalogBundleResourcePath = "Data/catalog.bundle";
        [SerializeField] private bool logStatus = true;

        private readonly Dictionary<string, BuildingEntryJson> _buildingsById = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, MapEntryJson> _mapsById = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, PointEntryJson> _pointsByKey = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, PolicyEntryJson> _policiesById = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, InstitutionCategoryEntryJson> _institutionCategoriesById = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, InstitutionEntryJson> _institutionsById = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, RecipeEntryJson> _recipesById = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, ResourceEntryJson> _resourcesByKey = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, TerrainEntryJson> _terrainsById = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, TechnologyEntryJson> _technologiesById = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, UnitEntryJson> _unitsById = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, EmoteSeriesEntryJson> _emoteSeriesById = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, EmoteEntryJson> _emotesById = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, MapRuntimeBundleJson> _mapBundleCache = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, SectionSyncAccumulator> _pendingSectionSync = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, string> _syncedSectionHashes = new(StringComparer.OrdinalIgnoreCase);

        private RulesJson _rules;
        private MinisterJson[] _ministers = Array.Empty<MinisterJson>();
        private MinisterSkillCardJson[] _ministerSkillCards = Array.Empty<MinisterSkillCardJson>();
        private TechTreeLayoutJson _techTreeLayout;
        private BuildMenuLayoutJson _buildMenuLayout;
        private RecipeLayoutJson _recipeLayout;

        public ManifestJson LocalManifest { get; private set; }
        public StaticCatalogManifest ServerManifest { get; private set; }
        public event System.Action CatalogChanged;

        public IReadOnlyDictionary<string, BuildingEntryJson> Buildings => _buildingsById;
        public IReadOnlyDictionary<string, PointEntryJson> Points => _pointsByKey;
        public IReadOnlyDictionary<string, PolicyEntryJson> Policies => _policiesById;
        public IReadOnlyDictionary<string, InstitutionCategoryEntryJson> InstitutionCategories => _institutionCategoriesById;
        public IReadOnlyDictionary<string, InstitutionEntryJson> Institutions => _institutionsById;
        public IReadOnlyDictionary<string, RecipeEntryJson> Recipes => _recipesById;
        public IReadOnlyDictionary<string, ResourceEntryJson> Resources => _resourcesByKey;
        public IReadOnlyDictionary<string, TerrainEntryJson> Terrains => _terrainsById;
        public IReadOnlyDictionary<string, TechnologyEntryJson> Technologies => _technologiesById;
        public IReadOnlyDictionary<string, UnitEntryJson> Units => _unitsById;
        public IReadOnlyDictionary<string, EmoteSeriesEntryJson> EmoteSeries => _emoteSeriesById;
        public IReadOnlyDictionary<string, EmoteEntryJson> Emotes => _emotesById;
        public RulesJson Rules => _rules;
        public IReadOnlyList<MinisterJson> Ministers => _ministers;
        public IReadOnlyList<MinisterSkillCardJson> MinisterSkillCards => _ministerSkillCards;
        public TechTreeLayoutJson TechTreeLayout => _techTreeLayout;
        public BuildMenuLayoutJson BuildMenuLayout => _buildMenuLayout;
        public RecipeLayoutJson RecipeLayout => _recipeLayout;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);

            if (autoLoadOnAwake)
            {
                LoadLocalCatalog();
            }
        }

        public bool LoadLocalCatalog()
        {
            var asset = UnityEngine.Resources.Load<TextAsset>(catalogBundleResourcePath);
            if (asset == null || string.IsNullOrWhiteSpace(asset.text))
            {
                if (logStatus)
                {
                    PanoptesLog.Warning($"[StaticCatalogCache] Catalog bundle missing at Resources/{catalogBundleResourcePath}.");
                }
                return false;
            }

            CatalogBundleJson parsed = null;
            try
            {
                parsed = JsonUtility.FromJson<CatalogBundleJson>(NormalizeCatalogAmountMaps(asset.text));
            }
            catch (Exception ex)
            {
                PanoptesLog.Warning($"[StaticCatalogCache] Failed to parse local catalog bundle: {ex.Message}");
                return false;
            }

            if (parsed == null || parsed.manifest == null)
            {
                return false;
            }

            LocalManifest = parsed.manifest;
            RebuildIndex(_resourcesByKey, parsed.resources, entry => entry != null ? entry.key : string.Empty);
            RebuildIndex(_pointsByKey, parsed.points, entry => entry != null ? entry.key : string.Empty);
            RebuildIndex(_unitsById, parsed.units, entry => entry != null ? entry.id : string.Empty);
            RebuildIndex(_buildingsById, parsed.buildings, entry => entry != null ? entry.id : string.Empty);
            RebuildIndex(_technologiesById, parsed.technologies, entry => entry != null ? entry.id : string.Empty);
            RebuildIndex(_policiesById, parsed.policies, entry => entry != null ? entry.id : string.Empty);
            RebuildIndex(_institutionCategoriesById, parsed.institution_categories, entry => entry != null ? entry.id : string.Empty);
            RebuildIndex(_institutionsById, parsed.institutions, entry => entry != null ? entry.id : string.Empty);
            RebuildIndex(_recipesById, parsed.recipes, entry => entry != null ? entry.id : string.Empty);
            RebuildIndex(_terrainsById, parsed.terrains, entry => entry != null ? entry.id : string.Empty);
            RebuildIndex(_emoteSeriesById, parsed.emote_series, entry => entry != null ? entry.id : string.Empty);
            RebuildIndex(_emotesById, parsed.emotes, entry => entry != null ? entry.id : string.Empty);
            RebuildIndex(_mapsById, parsed.maps, entry => entry != null ? entry.id : string.Empty);
            _rules = parsed.rules;
            _ministers = parsed.ministers ?? Array.Empty<MinisterJson>();
            _ministerSkillCards = parsed.minister_skill_cards ?? Array.Empty<MinisterSkillCardJson>();
            _techTreeLayout = parsed.ui_tech_tree_layout;
            _buildMenuLayout = parsed.ui_build_menu_layout;
            _recipeLayout = parsed.ui_recipe_layout;
            _mapBundleCache.Clear();
            _pendingSectionSync.Clear();
            _syncedSectionHashes.Clear();

            if (logStatus)
            {
                PanoptesLog.Log($"[StaticCatalogCache] Loaded local catalog bundle hash={LocalManifest.bundle_hash}.");
            }

            CatalogChanged?.Invoke();
            return true;
        }

        public void ApplyManifest(StaticCatalogManifest manifest)
        {
            ServerManifest = manifest;
            if (logStatus && LocalManifest != null && manifest != null &&
                !string.IsNullOrWhiteSpace(LocalManifest.bundle_hash) &&
                !string.IsNullOrWhiteSpace(manifest.BundleHash) &&
                !string.Equals(LocalManifest.bundle_hash, manifest.BundleHash, StringComparison.Ordinal))
            {
                PanoptesLog.Warning($"[StaticCatalogCache] Local bundle hash '{LocalManifest.bundle_hash}' differs from server '{manifest.BundleHash}'.");
            }
        }

        public CatalogSyncDecision CompareManifest(StaticCatalogManifest manifest)
        {
            ApplyManifest(manifest);

            var decision = new CatalogSyncDecision
            {
                ServerBundleHash = manifest != null ? manifest.BundleHash ?? string.Empty : string.Empty,
                HashMatches = manifest != null &&
                              !string.IsNullOrWhiteSpace(LocalManifest != null ? LocalManifest.bundle_hash : string.Empty) &&
                              string.Equals(LocalManifest.bundle_hash, manifest.BundleHash ?? string.Empty, StringComparison.Ordinal),
            };
            if (manifest == null)
            {
                return decision;
            }

            var requiredSections = manifest.RequiredSections != null && manifest.RequiredSections.Count > 0
                ? manifest.RequiredSections.ToArray()
                : Array.Empty<string>();
            if (!string.Equals(LocalManifest != null ? LocalManifest.schema_version : string.Empty, manifest.SchemaVersion ?? string.Empty, StringComparison.Ordinal))
            {
                decision.ForceFullSync = requiredSections.Length > 0;
                decision.RequestedSections = requiredSections;
                return decision;
            }

            var localHashes = BuildCurrentSectionHashMap();
            var requested = new List<string>();
            var serverHashes = manifest.SectionHashes != null ? manifest.SectionHashes : null;
            for (var i = 0; i < requiredSections.Length; i++)
            {
                var sectionName = Normalize(requiredSections[i]);
                if (string.IsNullOrEmpty(sectionName))
                {
                    continue;
                }

                if (!TryGetServerSectionHash(serverHashes, sectionName, out var serverHash))
                {
                    requested.Add(sectionName);
                    continue;
                }

                if (!localHashes.TryGetValue(sectionName, out var localHash) ||
                    !string.Equals(localHash, serverHash, StringComparison.Ordinal))
                {
                    requested.Add(sectionName);
                }
            }

            decision.RequestedSections = requested.ToArray();
            return decision;
        }

        public void BeginSectionSync(StaticCatalogManifest manifest, IEnumerable<string> sectionNames)
        {
            ApplyManifest(manifest);
            _pendingSectionSync.Clear();
            if (sectionNames == null)
            {
                return;
            }

            foreach (var rawSectionName in sectionNames)
            {
                var sectionName = Normalize(rawSectionName);
                if (string.IsNullOrEmpty(sectionName) || _pendingSectionSync.ContainsKey(sectionName))
                {
                    continue;
                }

                _pendingSectionSync[sectionName] = new SectionSyncAccumulator
                {
                    SectionName = sectionName
                };
            }
        }

        public void ApplySectionChunk(MsgStaticCatalogSectionChunk chunk)
        {
            if (chunk == null)
            {
                return;
            }

            var sectionName = Normalize(chunk.SectionName);
            if (string.IsNullOrEmpty(sectionName))
            {
                return;
            }

            if (!_pendingSectionSync.TryGetValue(sectionName, out var accumulator))
            {
                accumulator = new SectionSyncAccumulator
                {
                    SectionName = sectionName
                };
                _pendingSectionSync[sectionName] = accumulator;
            }

            accumulator.SectionHash = chunk.SectionHash ?? string.Empty;
            accumulator.Compression = chunk.Compression ?? string.Empty;
            accumulator.ChunkCount = Mathf.Max(1, (int)chunk.ChunkCount);
            accumulator.Chunks[(int)chunk.ChunkIndex] = chunk.Payload != null ? chunk.Payload.ToByteArray() : Array.Empty<byte>();
        }

        public bool FinalizeSectionSync(MsgStaticCatalogSyncComplete complete)
        {
            if (complete == null || !complete.Success)
            {
                _pendingSectionSync.Clear();
                return false;
            }

            var appliedAny = false;
            foreach (var pair in _pendingSectionSync)
            {
                var accumulator = pair.Value;
                if (accumulator == null)
                {
                    continue;
                }
                if (accumulator.ChunkCount <= 0 || accumulator.Chunks.Count < accumulator.ChunkCount)
                {
                    _pendingSectionSync.Clear();
                    return false;
                }

                var raw = ReassembleSection(accumulator);
                if (raw == null)
                {
                    _pendingSectionSync.Clear();
                    return false;
                }
                if (!ApplySectionJson(accumulator.SectionName, raw))
                {
                    _pendingSectionSync.Clear();
                    return false;
                }
                _syncedSectionHashes[accumulator.SectionName] =
                    !string.IsNullOrWhiteSpace(accumulator.SectionHash)
                        ? accumulator.SectionHash
                        : ComputeHash(raw);
                appliedAny = true;
            }

            _pendingSectionSync.Clear();
            if (ServerManifest != null)
            {
                LocalManifest = new ManifestJson
                {
                    schema_version = ServerManifest.SchemaVersion ?? string.Empty,
                    content_version = ServerManifest.ContentVersion ?? string.Empty,
                    default_locale = ServerManifest.DefaultLocale ?? string.Empty,
                    default_map_id = ServerManifest.DefaultMapId ?? string.Empty,
                    bundle_hash = complete.AppliedBundleHash ?? ServerManifest.BundleHash ?? string.Empty,
                    required_sections = ServerManifest.RequiredSections != null ? ServerManifest.RequiredSections.ToArray() : Array.Empty<string>(),
                    section_hashes = ServerManifest.SectionHashes != null
                        ? ServerManifest.SectionHashes
                            .Select(entry => new CatalogSectionHashJson
                            {
                                section_name = entry != null ? entry.SectionName : string.Empty,
                                hash = entry != null ? entry.Hash : string.Empty
                            })
                            .ToArray()
                        : Array.Empty<CatalogSectionHashJson>()
                };
            }

            if (appliedAny)
            {
                CatalogChanged?.Invoke();
            }
            return true;
        }

        public void ApplySnapshot(StaticCatalogSnapshot snapshot)
        {
            if (snapshot == null)
            {
                return;
            }

            ApplyManifest(snapshot.Manifest);

            // Keep richer local-only fields (e.g. prerequisites) when server snapshot schema is narrower.
            var oldBuildings = new Dictionary<string, BuildingEntryJson>(_buildingsById, StringComparer.OrdinalIgnoreCase);
            var oldTechnologies = new Dictionary<string, TechnologyEntryJson>(_technologiesById, StringComparer.OrdinalIgnoreCase);
            var oldRecipes = new Dictionary<string, RecipeEntryJson>(_recipesById, StringComparer.OrdinalIgnoreCase);
            var oldInstitutions = new Dictionary<string, InstitutionEntryJson>(_institutionsById, StringComparer.OrdinalIgnoreCase);

            RebuildIndex(_resourcesByKey, ConvertResources(snapshot.Resources), entry => entry != null ? entry.key : string.Empty);
            RebuildIndex(_pointsByKey, ConvertPoints(snapshot.Points), entry => entry != null ? entry.key : string.Empty);
            RebuildIndex(_unitsById, ConvertUnits(snapshot.Units), entry => entry != null ? entry.id : string.Empty);
            RebuildIndex(_buildingsById, ConvertBuildings(snapshot.Buildings, oldBuildings), entry => entry != null ? entry.id : string.Empty);
            RebuildIndex(_technologiesById, ConvertTechnologies(snapshot.Technologies, oldTechnologies), entry => entry != null ? entry.id : string.Empty);
            RebuildIndex(_policiesById, ConvertPolicies(snapshot.Policies), entry => entry != null ? entry.id : string.Empty);
            RebuildIndex(_institutionCategoriesById, ConvertInstitutionCategories(snapshot.InstitutionCategories), entry => entry != null ? entry.id : string.Empty);
            RebuildIndex(_institutionsById, ConvertInstitutions(snapshot.Institutions, oldInstitutions), entry => entry != null ? entry.id : string.Empty);
            RebuildIndex(_recipesById, ConvertRecipes(snapshot.Recipes, oldRecipes), entry => entry != null ? entry.id : string.Empty);
            RebuildIndex(_terrainsById, ConvertTerrains(snapshot.Terrains), entry => entry != null ? entry.id : string.Empty);
            RebuildIndex(_emoteSeriesById, ConvertEmoteSeries(snapshot.EmoteSeries), entry => entry != null ? entry.id : string.Empty);
            RebuildIndex(_emotesById, ConvertEmotes(snapshot.Emotes), entry => entry != null ? entry.id : string.Empty);

            if (logStatus)
            {
                PanoptesLog.Log($"[StaticCatalogCache] Applied server snapshot: techs={_technologiesById.Count} recipes={_recipesById.Count}.");
            }

            CatalogChanged?.Invoke();
        }

        public bool TryGetBuilding(string buildingId, out BuildingEntryJson entry)
        {
            return _buildingsById.TryGetValue(Normalize(buildingId), out entry);
        }

        public bool TryGetPoint(string pointKey, out PointEntryJson entry)
        {
            return _pointsByKey.TryGetValue(Normalize(pointKey), out entry);
        }

        public bool TryGetPolicy(string policyId, out PolicyEntryJson entry)
        {
            return _policiesById.TryGetValue(Normalize(policyId), out entry);
        }

        public bool TryGetInstitution(string institutionId, out InstitutionEntryJson entry)
        {
            return _institutionsById.TryGetValue(Normalize(institutionId), out entry);
        }

        public bool TryGetRecipe(string recipeId, out RecipeEntryJson entry)
        {
            return _recipesById.TryGetValue(Normalize(recipeId), out entry);
        }

        public bool TryGetTerrain(string terrainId, out TerrainEntryJson entry)
        {
            return _terrainsById.TryGetValue(Normalize(terrainId), out entry);
        }

        public bool TryGetTechnology(string technologyId, out TechnologyEntryJson entry)
        {
            return _technologiesById.TryGetValue(Normalize(technologyId), out entry);
        }

        public bool TryGetUnit(string unitId, out UnitEntryJson entry)
        {
            return _unitsById.TryGetValue(Normalize(unitId), out entry);
        }

        public bool TryGetEmote(string emoteId, out EmoteEntryJson entry)
        {
            return _emotesById.TryGetValue(Normalize(emoteId), out entry);
        }

        public bool TryGetDefaultMap(out MapRuntimeBundleJson bundle)
        {
            bundle = null;
            var mapId = Normalize(LocalManifest != null ? LocalManifest.default_map_id : string.Empty);
            if (string.IsNullOrEmpty(mapId))
            {
                return false;
            }

            return TryGetMapBundle(mapId, out bundle);
        }

        public bool TryGetMapBundle(string mapId, out MapRuntimeBundleJson bundle)
        {
            bundle = null;
            var key = Normalize(mapId);
            if (string.IsNullOrEmpty(key))
            {
                return false;
            }

            if (_mapBundleCache.TryGetValue(key, out bundle) && bundle != null)
            {
                return true;
            }

            var asset = UnityEngine.Resources.Load<TextAsset>($"Data/maps/{key}.runtime");
            if (asset == null || string.IsNullOrWhiteSpace(asset.text))
            {
                return false;
            }

            try
            {
                bundle = JsonUtility.FromJson<MapRuntimeBundleJson>(asset.text);
            }
            catch (Exception ex)
            {
                PanoptesLog.Warning($"[StaticCatalogCache] Failed to parse map bundle '{key}': {ex.Message}");
                return false;
            }

            if (bundle == null)
            {
                return false;
            }

            _mapBundleCache[key] = bundle;
            return true;
        }

        public void Clear()
        {
            LocalManifest = null;
            ServerManifest = null;
            _pointsByKey.Clear();
            _policiesById.Clear();
            _institutionCategoriesById.Clear();
            _institutionsById.Clear();
            _recipesById.Clear();
            _resourcesByKey.Clear();
            _technologiesById.Clear();
            _unitsById.Clear();
            _buildingsById.Clear();
            _terrainsById.Clear();
            _emoteSeriesById.Clear();
            _emotesById.Clear();
            _mapsById.Clear();
            _mapBundleCache.Clear();
            _pendingSectionSync.Clear();
            _syncedSectionHashes.Clear();
            _rules = null;
            _ministers = Array.Empty<MinisterJson>();
            _ministerSkillCards = Array.Empty<MinisterSkillCardJson>();
            _techTreeLayout = null;
            _buildMenuLayout = null;
            _recipeLayout = null;
        }

        private Dictionary<string, string> BuildCurrentSectionHashMap()
        {
            var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            var requiredSections = LocalManifest != null && LocalManifest.required_sections != null
                ? LocalManifest.required_sections
                : Array.Empty<string>();

            for (var i = 0; i < requiredSections.Length; i++)
            {
                var sectionName = Normalize(requiredSections[i]);
                if (string.IsNullOrEmpty(sectionName))
                {
                    continue;
                }

                if (TryLoadLocalSectionHash(sectionName, out var hash))
                {
                    map[sectionName] = hash;
                    continue;
                }

                if (_syncedSectionHashes.TryGetValue(sectionName, out hash) && !string.IsNullOrWhiteSpace(hash))
                {
                    map[sectionName] = hash;
                }
            }

            if (map.Count > 0)
            {
                return map;
            }

            foreach (var section in CreateSectionPayloads())
            {
                if (section.Payload == null)
                {
                    continue;
                }

                map[section.Name] = ComputeHash(section.Payload);
            }
            return map;
        }

        private bool TryLoadLocalSectionHash(string sectionName, out string hash)
        {
            hash = string.Empty;
            var normalized = Normalize(sectionName);
            if (string.IsNullOrEmpty(normalized))
            {
                return false;
            }

            var asset = UnityEngine.Resources.Load<TextAsset>($"Data/sections/{normalized}");
            if (asset == null || string.IsNullOrEmpty(asset.text))
            {
                return false;
            }

            hash = ComputeHash(asset.text);
            return !string.IsNullOrWhiteSpace(hash);
        }

        private IEnumerable<(string Name, string Payload)> CreateSectionPayloads()
        {
            yield return ("resources", JsonUtility.ToJson(new ResourcesSectionJson { resources = _resourcesByKey.Values.OrderBy(entry => Normalize(entry.key)).ToArray() }));
            yield return ("points", JsonUtility.ToJson(new PointsSectionJson { points = _pointsByKey.Values.OrderBy(entry => Normalize(entry.key)).ToArray() }));
            yield return ("units", JsonUtility.ToJson(new UnitsSectionJson { units = _unitsById.Values.OrderBy(entry => Normalize(entry.id)).ToArray() }));
            yield return ("buildings", JsonUtility.ToJson(new BuildingsSectionJson { buildings = _buildingsById.Values.OrderBy(entry => Normalize(entry.id)).ToArray() }));
            yield return ("technologies", JsonUtility.ToJson(new TechnologiesSectionJson { technologies = _technologiesById.Values.OrderBy(entry => Normalize(entry.id)).ToArray() }));
            yield return ("policies", JsonUtility.ToJson(new PoliciesSectionJson { policies = _policiesById.Values.OrderBy(entry => Normalize(entry.id)).ToArray() }));
            yield return ("institutions", JsonUtility.ToJson(new InstitutionsSectionJson
            {
                categories = _institutionCategoriesById.Values.OrderBy(entry => entry != null ? entry.sort_order : 0).ThenBy(entry => Normalize(entry.id)).ToArray(),
                institutions = _institutionsById.Values.OrderBy(entry => Normalize(entry.id)).ToArray()
            }));
            yield return ("recipes", JsonUtility.ToJson(new RecipesSectionJson { recipes = _recipesById.Values.OrderBy(entry => Normalize(entry.id)).ToArray() }));
            yield return ("terrains", JsonUtility.ToJson(new TerrainsSectionJson { terrains = _terrainsById.Values.OrderBy(entry => Normalize(entry.id)).ToArray() }));
            yield return ("emotes", JsonUtility.ToJson(new EmotesSectionJson
            {
                series = _emoteSeriesById.Values.OrderBy(entry => Normalize(entry.id)).ToArray(),
                emotes = _emotesById.Values.OrderBy(entry => Normalize(entry.id)).ToArray()
            }));
            yield return ("rules", JsonUtility.ToJson(new RulesSectionJson { rules = _rules ?? new RulesJson() }));
            yield return ("ministers", JsonUtility.ToJson(new MinistersSectionJson { ministers = _ministers ?? Array.Empty<MinisterJson>() }));
            yield return ("minister_skill_cards", JsonUtility.ToJson(new MinisterSkillCardsSectionJson { minister_skill_cards = _ministerSkillCards ?? Array.Empty<MinisterSkillCardJson>() }));
            yield return ("maps", JsonUtility.ToJson(new MapsSectionJson { maps = _mapsById.Values.OrderBy(entry => Normalize(entry.id)).ToArray() }));
            yield return ("ui_tech_tree_layout", JsonUtility.ToJson(_techTreeLayout ?? new TechTreeLayoutJson()));
            yield return ("ui_build_menu_layout", JsonUtility.ToJson(_buildMenuLayout ?? new BuildMenuLayoutJson()));
            yield return ("ui_recipe_layout", JsonUtility.ToJson(_recipeLayout ?? new RecipeLayoutJson()));
        }

        private static bool TryGetServerSectionHash(IList<CatalogSectionHash> hashes, string sectionName, out string hash)
        {
            hash = string.Empty;
            if (hashes == null)
            {
                return false;
            }

            for (var i = 0; i < hashes.Count; i++)
            {
                var entry = hashes[i];
                if (entry == null)
                {
                    continue;
                }

                if (!string.Equals(Normalize(entry.SectionName), sectionName, StringComparison.Ordinal))
                {
                    continue;
                }

                hash = entry.Hash ?? string.Empty;
                return !string.IsNullOrWhiteSpace(hash);
            }

            return false;
        }

        private static string ComputeHash(string payload)
        {
            if (string.IsNullOrWhiteSpace(payload))
            {
                return string.Empty;
            }

            using var sha256 = SHA256.Create();
            var raw = Encoding.UTF8.GetBytes(payload);
            var sum = sha256.ComputeHash(raw);
            return BitConverter.ToString(sum).Replace("-", string.Empty).ToLowerInvariant();
        }

        private static string ReassembleSection(SectionSyncAccumulator accumulator)
        {
            if (accumulator == null)
            {
                return null;
            }

            var chunkCount = Mathf.Max(1, accumulator.ChunkCount);
            var buffer = new List<byte>();
            for (var i = 0; i < chunkCount; i++)
            {
                if (!accumulator.Chunks.TryGetValue(i, out var chunk) || chunk == null)
                {
                    return null;
                }
                buffer.AddRange(chunk);
            }

            var raw = buffer.ToArray();
            if (string.Equals(accumulator.Compression, "gzip", StringComparison.OrdinalIgnoreCase))
            {
                try
                {
                    using var input = new System.IO.MemoryStream(raw);
                    using var gzip = new System.IO.Compression.GZipStream(input, System.IO.Compression.CompressionMode.Decompress);
                    using var output = new System.IO.MemoryStream();
                    gzip.CopyTo(output);
                    raw = output.ToArray();
                }
                catch (Exception ex)
                {
                    PanoptesLog.Warning($"[StaticCatalogCache] Failed to decompress section '{accumulator.SectionName}': {ex.Message}");
                    return null;
                }
            }

            return Encoding.UTF8.GetString(raw);
        }

        private bool ApplySectionJson(string sectionName, string payload)
        {
            if (string.IsNullOrWhiteSpace(sectionName) || string.IsNullOrWhiteSpace(payload))
            {
                return false;
            }

            try
            {
                switch (Normalize(sectionName))
                {
                    case "resources":
                        RebuildIndex(_resourcesByKey, JsonUtility.FromJson<ResourcesSectionJson>(payload)?.resources, entry => entry != null ? entry.key : string.Empty);
                        return true;
                    case "points":
                        RebuildIndex(_pointsByKey, JsonUtility.FromJson<PointsSectionJson>(payload)?.points, entry => entry != null ? entry.key : string.Empty);
                        return true;
                    case "units":
                        RebuildIndex(_unitsById, JsonUtility.FromJson<UnitsSectionJson>(payload)?.units, entry => entry != null ? entry.id : string.Empty);
                        return true;
                    case "buildings":
                        RebuildIndex(_buildingsById, JsonUtility.FromJson<BuildingsSectionJson>(NormalizeAmountMapFields(payload, "resource_costs", "point_costs"))?.buildings, entry => entry != null ? entry.id : string.Empty);
                        return true;
                    case "technologies":
                        RebuildIndex(_technologiesById, JsonUtility.FromJson<TechnologiesSectionJson>(payload)?.technologies, entry => entry != null ? entry.id : string.Empty);
                        return true;
                    case "policies":
                        RebuildIndex(_policiesById, JsonUtility.FromJson<PoliciesSectionJson>(payload)?.policies, entry => entry != null ? entry.id : string.Empty);
                        return true;
                    case "institutions":
                        var institutions = JsonUtility.FromJson<InstitutionsSectionJson>(payload);
                        RebuildIndex(_institutionCategoriesById, institutions?.categories, entry => entry != null ? entry.id : string.Empty);
                        RebuildIndex(_institutionsById, institutions?.institutions, entry => entry != null ? entry.id : string.Empty);
                        return true;
                    case "recipes":
                        RebuildIndex(_recipesById, JsonUtility.FromJson<RecipesSectionJson>(NormalizeAmountMapFields(payload, "resource_inputs", "point_inputs", "resources", "point_progress", "state_changes"))?.recipes, entry => entry != null ? entry.id : string.Empty);
                        return true;
                    case "terrains":
                        RebuildIndex(_terrainsById, JsonUtility.FromJson<TerrainsSectionJson>(payload)?.terrains, entry => entry != null ? entry.id : string.Empty);
                        return true;
                    case "emotes":
                        var emotesSection = JsonUtility.FromJson<EmotesSectionJson>(payload);
                        RebuildIndex(_emoteSeriesById, emotesSection?.series, entry => entry != null ? entry.id : string.Empty);
                        RebuildIndex(_emotesById, emotesSection?.emotes, entry => entry != null ? entry.id : string.Empty);
                        return true;
                    case "rules":
                        _rules = JsonUtility.FromJson<RulesSectionJson>(payload)?.rules;
                        return true;
                    case "ministers":
                        _ministers = JsonUtility.FromJson<MinistersSectionJson>(payload)?.ministers ?? Array.Empty<MinisterJson>();
                        return true;
                    case "minister_skill_cards":
                        _ministerSkillCards = JsonUtility.FromJson<MinisterSkillCardsSectionJson>(payload)?.minister_skill_cards ?? Array.Empty<MinisterSkillCardJson>();
                        return true;
                    case "maps":
                        RebuildIndex(_mapsById, JsonUtility.FromJson<MapsSectionJson>(payload)?.maps, entry => entry != null ? entry.id : string.Empty);
                        _mapBundleCache.Clear();
                        return true;
                    case "ui_tech_tree_layout":
                        _techTreeLayout = JsonUtility.FromJson<TechTreeLayoutJson>(payload);
                        return _techTreeLayout != null;
                    case "ui_build_menu_layout":
                        _buildMenuLayout = JsonUtility.FromJson<BuildMenuLayoutJson>(payload);
                        return _buildMenuLayout != null;
                    case "ui_recipe_layout":
                        _recipeLayout = JsonUtility.FromJson<RecipeLayoutJson>(payload);
                        return _recipeLayout != null;
                    default:
                        return false;
                }
            }
            catch (Exception ex)
            {
                PanoptesLog.Warning($"[StaticCatalogCache] Failed to apply section '{sectionName}': {ex.Message}");
                return false;
            }
        }

        private static void RebuildIndex<T>(Dictionary<string, T> target, T[] source, Func<T, string> keySelector)
        {
            target.Clear();
            if (source == null)
            {
                return;
            }

            for (var i = 0; i < source.Length; i++)
            {
                var entry = source[i];
                var key = Normalize(keySelector(entry));
                if (string.IsNullOrEmpty(key))
                {
                    continue;
                }

                target[key] = entry;
            }
        }

        private static ResourceEntryJson[] ConvertResources(System.Collections.Generic.IList<ResourceDescriptor> source)
        {
            if (source == null || source.Count == 0)
            {
                return Array.Empty<ResourceEntryJson>();
            }

            var result = new ResourceEntryJson[source.Count];
            for (var i = 0; i < source.Count; i++)
            {
                var item = source[i];
                result[i] = new ResourceEntryJson
                {
                    key = item != null ? item.Key : string.Empty,
                    display_name = item != null ? item.DisplayName : string.Empty,
                    description = item != null ? item.Description : string.Empty,
                    icon_key = item != null ? item.IconKey : string.Empty,
                    sort_order = item != null ? item.SortOrder : 0,
                    visible_in_hud = item != null && item.VisibleInHud
                };
            }

            return result;
        }

        private static PointEntryJson[] ConvertPoints(System.Collections.Generic.IList<PointDescriptor> source)
        {
            if (source == null || source.Count == 0)
            {
                return Array.Empty<PointEntryJson>();
            }

            var result = new PointEntryJson[source.Count];
            for (var i = 0; i < source.Count; i++)
            {
                var item = source[i];
                result[i] = new PointEntryJson
                {
                    key = item != null ? item.Key : string.Empty,
                    display_name = item != null ? item.DisplayName : string.Empty,
                    description = item != null ? item.Description : string.Empty,
                    icon_key = item != null ? item.IconKey : string.Empty,
                    sort_order = item != null ? item.SortOrder : 0,
                    visible_in_hud = item != null && item.VisibleInHud
                };
            }

            return result;
        }

        private static UnitEntryJson[] ConvertUnits(System.Collections.Generic.IList<UnitCatalogEntry> source)
        {
            if (source == null || source.Count == 0)
            {
                return Array.Empty<UnitEntryJson>();
            }

            var result = new UnitEntryJson[source.Count];
            for (var i = 0; i < source.Count; i++)
            {
                var item = source[i];
                result[i] = new UnitEntryJson
                {
                    id = item != null ? item.Id : string.Empty,
                    name = item != null ? item.Name : string.Empty,
                    description = item != null ? item.Description : string.Empty,
                    icon_key = item != null ? item.IconKey : string.Empty,
                    prefab_key = item != null ? item.PrefabKey : string.Empty,
                    flags = new UnitEntryJson.UnitFlagsJson
                    {
                        can_attack_structures = item != null && item.CanAttackStructures,
                        can_destroy_road = false
                    },
                    tags = item != null ? item.Tags.ToArray() : Array.Empty<string>()
                };
            }

            return result;
        }

        private static BuildingEntryJson[] ConvertBuildings(
            System.Collections.Generic.IList<BuildingCatalogEntry> source,
            IReadOnlyDictionary<string, BuildingEntryJson> previous)
        {
            if (source == null || source.Count == 0)
            {
                return Array.Empty<BuildingEntryJson>();
            }

            var result = new BuildingEntryJson[source.Count];
            for (var i = 0; i < source.Count; i++)
            {
                var item = source[i];
                var id = item != null ? item.Id : string.Empty;
                previous.TryGetValue(Normalize(id), out var old);
                result[i] = new BuildingEntryJson
                {
                    id = id,
                    name = item != null ? item.Name : string.Empty,
                    description = item != null ? item.Description : string.Empty,
                    icon_key = item != null ? item.IconKey : string.Empty,
                    prefab_key = item != null ? item.PrefabKey : string.Empty,
                    placement_kind = item != null ? item.PlacementKind : string.Empty,
                    building_scope = item != null ? item.BuildingScope : string.Empty,
                    required_resource_type = item != null ? item.RequiredResourceType : string.Empty,
                    takeover_mode = item != null ? item.TakeoverMode : string.Empty,
                    tags = item != null ? item.Tags.ToArray() : (old != null ? old.tags : Array.Empty<string>()),
                    resource_costs = old != null ? old.resource_costs : Array.Empty<IntAmountEntryJson>(),
                    point_costs = old != null ? old.point_costs : Array.Empty<IntAmountEntryJson>(),
                    recipe_ids = old != null ? old.recipe_ids : Array.Empty<string>(),
                    default_recipe_id = old != null ? old.default_recipe_id : string.Empty
                };
            }

            return result;
        }

        private static TechnologyEntryJson[] ConvertTechnologies(
            System.Collections.Generic.IList<TechnologyCatalogEntry> source,
            IReadOnlyDictionary<string, TechnologyEntryJson> previous)
        {
            if (source == null || source.Count == 0)
            {
                return Array.Empty<TechnologyEntryJson>();
            }

            var result = new TechnologyEntryJson[source.Count];
            for (var i = 0; i < source.Count; i++)
            {
                var item = source[i];
                var id = item != null ? item.Id : string.Empty;
                previous.TryGetValue(Normalize(id), out var old);
                result[i] = new TechnologyEntryJson
                {
                    id = id,
                    name = item != null ? item.Name : string.Empty,
                    description = item != null ? item.Description : string.Empty,
                    icon_key = item != null ? item.IconKey : string.Empty,
                    branch = item != null ? item.Branch : string.Empty,
                    tier = item != null ? item.Tier : 0,
                    research_cost = item != null ? item.ResearchCost : 0,
                    tags = item != null ? item.Tags.ToArray() : (old != null ? old.tags : Array.Empty<string>()),
                    sort_order = old != null ? old.sort_order : 0,
                    prerequisites = old != null ? old.prerequisites : Array.Empty<PrerequisiteEntryJson>(),
                    explicit_effects = old != null ? old.explicit_effects : Array.Empty<ExplicitEffectEntryJson>()
                };
            }

            return result;
        }

        private static PolicyEntryJson[] ConvertPolicies(System.Collections.Generic.IList<PolicyCatalogEntry> source)
        {
            if (source == null || source.Count == 0)
            {
                return Array.Empty<PolicyEntryJson>();
            }

            var result = new PolicyEntryJson[source.Count];
            for (var i = 0; i < source.Count; i++)
            {
                var item = source[i];
                result[i] = new PolicyEntryJson
                {
                    id = item != null ? item.Id : string.Empty,
                    name = item != null ? item.Name : string.Empty,
                    description = item != null ? item.Description : string.Empty,
                    icon_key = item != null ? item.IconKey : string.Empty,
                    layer = item != null ? item.Layer : string.Empty,
                    activation_timing = item != null ? item.ActivationTiming : string.Empty,
                    modifier_effects = Array.Empty<ModifierEffectEntryJson>()
                };
            }

            return result;
        }

        private static InstitutionCategoryEntryJson[] ConvertInstitutionCategories(System.Collections.Generic.IList<InstitutionCategoryCatalogEntry> source)
        {
            if (source == null || source.Count == 0)
            {
                return Array.Empty<InstitutionCategoryEntryJson>();
            }

            var result = new InstitutionCategoryEntryJson[source.Count];
            for (var i = 0; i < source.Count; i++)
            {
                var item = source[i];
                result[i] = new InstitutionCategoryEntryJson
                {
                    id = item != null ? item.Id : string.Empty,
                    name = item != null ? item.Name : string.Empty,
                    description = item != null ? item.Description : string.Empty,
                    sort_order = item != null ? item.SortOrder : 0,
                    tags = item != null ? item.Tags.ToArray() : Array.Empty<string>()
                };
            }

            return result;
        }

        private static InstitutionEntryJson[] ConvertInstitutions(
            System.Collections.Generic.IList<InstitutionCatalogEntry> source,
            IReadOnlyDictionary<string, InstitutionEntryJson> previous)
        {
            if (source == null || source.Count == 0)
            {
                return Array.Empty<InstitutionEntryJson>();
            }

            var result = new InstitutionEntryJson[source.Count];
            for (var i = 0; i < source.Count; i++)
            {
                var item = source[i];
                var id = item != null ? item.Id : string.Empty;
                previous.TryGetValue(Normalize(id), out var old);
                result[i] = new InstitutionEntryJson
                {
                    id = id,
                    name = item != null ? item.Name : string.Empty,
                    description = item != null ? item.Description : string.Empty,
                    icon_key = item != null ? item.IconKey : string.Empty,
                    category = item != null ? item.Category : string.Empty,
                    activation_timing = item != null ? item.ActivationTiming : string.Empty,
                    tags = item != null ? item.Tags.ToArray() : (old != null ? old.tags : Array.Empty<string>()),
                    sort_order = old != null ? old.sort_order : 0,
                    prerequisites = old != null ? old.prerequisites : Array.Empty<PrerequisiteEntryJson>(),
                    explicit_effects = old != null ? old.explicit_effects : Array.Empty<ExplicitEffectEntryJson>(),
                    modifier_effects = old != null ? old.modifier_effects : Array.Empty<ModifierEffectEntryJson>()
                };
            }

            return result;
        }

        private static RecipeEntryJson[] ConvertRecipes(
            System.Collections.Generic.IList<RecipeCatalogEntry> source,
            IReadOnlyDictionary<string, RecipeEntryJson> previous)
        {
            if (source == null || source.Count == 0)
            {
                return Array.Empty<RecipeEntryJson>();
            }

            var result = new RecipeEntryJson[source.Count];
            for (var i = 0; i < source.Count; i++)
            {
                var item = source[i];
                var id = item != null ? item.Id : string.Empty;
                previous.TryGetValue(Normalize(id), out var old);
                result[i] = new RecipeEntryJson
                {
                    id = id,
                    name = item != null ? item.Name : string.Empty,
                    description = item != null ? item.Description : string.Empty,
                    icon_key = item != null ? item.IconKey : string.Empty,
                    building_id = item != null ? item.BuildingId : string.Empty,
                    work_amount = item != null ? item.WorkAmount : 0,
                    base_progress = item != null ? item.BaseProgress : 0,
                    tags = item != null ? item.Tags.ToArray() : (old != null ? old.tags : Array.Empty<string>()),
                    sort_order = old != null ? old.sort_order : 0,
                    resource_inputs = old != null ? old.resource_inputs : Array.Empty<IntAmountEntryJson>(),
                    point_inputs = old != null ? old.point_inputs : Array.Empty<IntAmountEntryJson>(),
                    outputs = old != null ? old.outputs : new RecipeOutputsJson()
                };
            }

            return result;
        }

        private static TerrainEntryJson[] ConvertTerrains(System.Collections.Generic.IList<TerrainCatalogEntry> source)
        {
            if (source == null || source.Count == 0)
            {
                return Array.Empty<TerrainEntryJson>();
            }

            var result = new TerrainEntryJson[source.Count];
            for (var i = 0; i < source.Count; i++)
            {
                var item = source[i];
                result[i] = new TerrainEntryJson
                {
                    id = item != null ? item.Id : string.Empty,
                    name = item != null ? item.Name : string.Empty,
                    description = item != null ? item.Description : string.Empty,
                    icon_key = item != null ? item.IconKey : string.Empty,
                    material_key = item != null ? item.MaterialKey : string.Empty,
                    tags = item != null ? item.Tags.ToArray() : Array.Empty<string>()
                };
            }

            return result;
        }

        private static EmoteSeriesEntryJson[] ConvertEmoteSeries(System.Collections.Generic.IList<EmoteSeriesCatalogEntry> source)
        {
            if (source == null || source.Count == 0)
            {
                return Array.Empty<EmoteSeriesEntryJson>();
            }

            var result = new EmoteSeriesEntryJson[source.Count];
            for (var i = 0; i < source.Count; i++)
            {
                var item = source[i];
                result[i] = new EmoteSeriesEntryJson
                {
                    id = item != null ? item.Id : string.Empty,
                    display_name = item != null ? item.DisplayName : string.Empty,
                    icon_key = item != null ? item.IconKey : string.Empty,
                    sort_order = item != null ? item.SortOrder : 0
                };
            }

            return result;
        }

        private static EmoteEntryJson[] ConvertEmotes(System.Collections.Generic.IList<EmoteCatalogEntry> source)
        {
            if (source == null || source.Count == 0)
            {
                return Array.Empty<EmoteEntryJson>();
            }

            var result = new EmoteEntryJson[source.Count];
            for (var i = 0; i < source.Count; i++)
            {
                var item = source[i];
                result[i] = new EmoteEntryJson
                {
                    id = item != null ? item.Id : string.Empty,
                    series_id = item != null ? item.SeriesId : string.Empty,
                    display_name = item != null ? item.DisplayName : string.Empty,
                    asset_key = item != null ? item.AssetKey : string.Empty,
                    sort_order = item != null ? item.SortOrder : 0,
                    tags = item != null ? item.Tags.ToArray() : Array.Empty<string>()
                };
            }

            return result;
        }

        private static string Normalize(string value)
        {
            return (value ?? string.Empty).Trim().ToLowerInvariant();
        }

        private static string NormalizeCatalogAmountMaps(string payload)
        {
            return NormalizeAmountMapFields(
                payload,
                "resource_costs",
                "point_costs",
                "resource_inputs",
                "point_inputs",
                "resources",
                "point_progress",
                "state_changes");
        }

        private static string NormalizeAmountMapFields(string payload, params string[] fieldNames)
        {
            if (string.IsNullOrWhiteSpace(payload) || fieldNames == null || fieldNames.Length == 0)
            {
                return payload;
            }

            var result = payload;
            for (var i = 0; i < fieldNames.Length; i++)
            {
                result = NormalizeAmountMapField(result, fieldNames[i]);
            }

            return result;
        }

        private static string NormalizeAmountMapField(string payload, string fieldName)
        {
            if (string.IsNullOrWhiteSpace(payload) || string.IsNullOrWhiteSpace(fieldName))
            {
                return payload;
            }

            var search = "\"" + fieldName + "\"";
            var builder = new StringBuilder(payload.Length);
            var cursor = 0;
            while (cursor < payload.Length)
            {
                var fieldIndex = payload.IndexOf(search, cursor, StringComparison.Ordinal);
                if (fieldIndex < 0)
                {
                    builder.Append(payload, cursor, payload.Length - cursor);
                    break;
                }

                builder.Append(payload, cursor, fieldIndex - cursor);
                var colonIndex = payload.IndexOf(':', fieldIndex + search.Length);
                if (colonIndex < 0)
                {
                    builder.Append(payload, fieldIndex, payload.Length - fieldIndex);
                    break;
                }

                var valueStart = colonIndex + 1;
                while (valueStart < payload.Length && char.IsWhiteSpace(payload[valueStart]))
                {
                    valueStart++;
                }

                if (valueStart >= payload.Length || payload[valueStart] != '{')
                {
                    builder.Append(payload, fieldIndex, valueStart - fieldIndex);
                    cursor = valueStart;
                    continue;
                }

                var valueEnd = FindMatchingBrace(payload, valueStart);
                if (valueEnd < 0)
                {
                    builder.Append(payload, fieldIndex, payload.Length - fieldIndex);
                    break;
                }

                builder.Append(payload, fieldIndex, valueStart - fieldIndex);
                builder.Append(ConvertAmountObjectToArray(payload.Substring(valueStart + 1, valueEnd - valueStart - 1)));
                cursor = valueEnd + 1;
            }

            return builder.ToString();
        }

        private static int FindMatchingBrace(string text, int openIndex)
        {
            var depth = 0;
            var inString = false;
            var escaped = false;
            for (var i = openIndex; i < text.Length; i++)
            {
                var current = text[i];
                if (inString)
                {
                    if (escaped)
                    {
                        escaped = false;
                    }
                    else if (current == '\\')
                    {
                        escaped = true;
                    }
                    else if (current == '"')
                    {
                        inString = false;
                    }

                    continue;
                }

                if (current == '"')
                {
                    inString = true;
                    continue;
                }

                if (current == '{')
                {
                    depth++;
                }
                else if (current == '}')
                {
                    depth--;
                    if (depth == 0)
                    {
                        return i;
                    }
                }
            }

            return -1;
        }

        private static string ConvertAmountObjectToArray(string objectBody)
        {
            var matches = Regex.Matches(objectBody ?? string.Empty, "\"(?<key>(?:\\\\.|[^\"\\\\])*)\"\\s*:\\s*(?<amount>-?\\d+)");
            if (matches.Count == 0)
            {
                return "[]";
            }

            var builder = new StringBuilder();
            builder.Append('[');
            for (var i = 0; i < matches.Count; i++)
            {
                if (i > 0)
                {
                    builder.Append(',');
                }

                builder.Append("{\"key\":\"");
                builder.Append(matches[i].Groups["key"].Value);
                builder.Append("\",\"amount\":");
                builder.Append(matches[i].Groups["amount"].Value);
                builder.Append('}');
            }

            builder.Append(']');
            return builder.ToString();
        }
    }
}
