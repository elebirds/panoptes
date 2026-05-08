using System;
using Panoptes.Core.Application.Stores;
using Panoptes.Core.Domain;

namespace Panoptes.Presentation.ViewModels
{
    internal static class CatalogDisplayNameResolver
    {
        public static string ResolveRecipeName(string recipeId, StaticCatalogState catalog)
        {
            var id = Normalize(recipeId);
            if (string.IsNullOrEmpty(id))
            {
                return string.Empty;
            }

            if (catalog?.Recipes != null &&
                catalog.Recipes.TryGetValue(id, out var recipe) &&
                recipe != null &&
                !string.IsNullOrWhiteSpace(recipe.Name))
            {
                return recipe.Name.Trim();
            }

            return ResolveKnownName(id);
        }

        public static string ResolveTechnologyName(string technologyId, StaticCatalogState catalog, string emptyLabel)
        {
            var id = Normalize(technologyId);
            if (string.IsNullOrEmpty(id))
            {
                return emptyLabel;
            }

            if (catalog?.Technologies != null &&
                catalog.Technologies.TryGetValue(id, out var technology) &&
                technology != null &&
                !string.IsNullOrWhiteSpace(technology.Name))
            {
                return technology.Name.Trim();
            }

            return ResolveKnownName(id);
        }

        public static string ResolvePolicyName(string policyId, StaticCatalogState catalog, string emptyLabel)
        {
            var id = Normalize(policyId);
            if (string.IsNullOrEmpty(id))
            {
                return emptyLabel;
            }

            if (catalog?.Policies != null &&
                catalog.Policies.TryGetValue(id, out var policy) &&
                policy != null &&
                !string.IsNullOrWhiteSpace(policy.Name))
            {
                return policy.Name.Trim();
            }

            return ResolveKnownName(id);
        }

        public static string ResolveEffectTargetName(string targetId, StaticCatalogState catalog)
        {
            var id = Normalize(targetId);
            if (string.IsNullOrEmpty(id))
            {
                return string.Empty;
            }

            var recipe = ResolveRecipeName(id, catalog);
            if (!string.Equals(recipe, ToReadableKey(id), StringComparison.Ordinal))
            {
                return recipe;
            }

            if (catalog?.Buildings != null &&
                catalog.Buildings.TryGetValue(id, out var building) &&
                building != null &&
                !string.IsNullOrWhiteSpace(building.Name))
            {
                return building.Name.Trim();
            }

            if (catalog?.Units != null &&
                catalog.Units.TryGetValue(id, out var unit) &&
                unit != null &&
                !string.IsNullOrWhiteSpace(unit.Name))
            {
                return unit.Name.Trim();
            }

            if (catalog?.Technologies != null &&
                catalog.Technologies.TryGetValue(id, out var technology) &&
                technology != null &&
                !string.IsNullOrWhiteSpace(technology.Name))
            {
                return technology.Name.Trim();
            }

            return ResolveKnownName(id);
        }

        public static string ResolveResourceName(string resourceKey)
        {
            return ResolveKnownName(resourceKey);
        }

        public static string ResolvePointName(string pointKey)
        {
            return ResolveKnownName(pointKey);
        }

        public static string ResolveKnownName(string key)
        {
            var id = Normalize(key);
            return id switch
            {
                "food" => "粮食",
                "wood" => "木材",
                "ore" => "矿石",
                "research_output" => "科研产出",
                "industry_output" => "工业产出",
                "settler" => "开拓者",
                "infantry" => "步兵",
                "archer" => "弓手",
                "cavalry" => "骑兵",
                "spearman" => "长矛兵",
                "scout" => "斥候",
                "raider" => "掠袭兵",
                "siege_engine" => "攻城器械",
                "city_core_provisions" => "城市供给",
                "city_core_settler" => "组织开拓",
                "farm_food" => "基础农耕",
                "farm_harvest" => "增产收割",
                "lumber_wood" => "基础伐木",
                "lumber_sawmill" => "锯木加工",
                "mine_ore" => "基础采矿",
                "mine_deep_ore" => "深层采掘",
                "barracks_infantry" => "训练步兵",
                "archery_archer" => "训练弓手",
                "granary_rations" => "整备口粮",
                "smelter_refined_ore" => "矿石冶炼",
                "stable_cavalry" => "训练骑兵",
                "engineer_camp_raider" => "训练掠袭兵",
                "engineer_camp_siege_engine" => "组装攻城器械",
                "warehouse_reserve_rations" => "后备口粮",
                "market_grain_contracts" => "粮食契约",
                "market_lumber_contracts" => "木材契约",
                "market_ore_contracts" => "矿石契约",
                "watchtower_scout" => "训练斥候",
                "training_ground_spearman" => "训练长矛兵",
                "expansion" => "扩张",
                "war_preparedness" => "备战",
                "recovery" => "恢复",
                "reorganization" => "整饬",
                "royal_prerogative" => "王权亲政",
                "noble_council" => "贵族议会",
                "bureaucratic_cabinet" => "官僚内阁",
                "ministerial_devolution" => "部长分权",
                "estate_subjects" => "等级臣民",
                "civic_citizenship" => "城邦公民",
                "martial_citizenship" => "军役公民",
                "universal_subjecthood" => "普遍臣民权",
                "manor_economy" => "庄园经济",
                "guild_regulation" => "行会管制",
                "state_workshops" => "国家工坊",
                "free_market" => "自由市场",
                "inward_rationing" => "内向配给",
                "chartered_trade" => "特许贸易",
                "protective_tariffs" => "保护关税",
                "free_trade" => "自由贸易",
                "vassal_levies" => "封臣兵役",
                "professional_army" => "职业军",
                "mass_conscription" => "征召制",
                "munitions_committee" => "军需委员会",
                "local_autonomy" => "地方自治",
                "central_bureaucracy" => "中央官僚",
                "inspectorate_system" => "巡察体系",
                "secretariat_cabinet" => "秘书处内阁",
                "metallurgy" => "冶金术",
                "trade_levies" => "商税法",
                "scholastic_bureaucracy" => "学官制",
                _ => ToReadableKey(id)
            };
        }

        public static string ToReadableKey(string key)
        {
            return string.IsNullOrWhiteSpace(key) ? string.Empty : key.Trim().Replace('_', ' ');
        }

        private static string Normalize(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim().ToLowerInvariant();
        }
    }
}
