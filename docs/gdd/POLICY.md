# 政策科技树设计文档

> 版本：v0.2 | 状态：草稿

---

## 1. 概述

政策科技树是玩家通过解锁特定节点来获得持续性全局增益的核心机制。共分为 **5 条分支**，每条分支最多 **5 个层级（Tier 1–5）**，总计约 25 个政策节点。

解锁政策需要同时满足两类条件：

1. **部长属性条件**：当前已指派的对应角色部长，其某项属性值落在指定区间内。
2. **其他前置条件**：上级政策节点已解锁、资源储量、回合数、控制节点数等。

解锁后效果持续生效（资源产量修正、战斗力修正、特殊行动解锁等）。解锁政策需消耗**科技点数**，玩家的科技点数上限决定了可解锁政策的总量（具体机制见第 8 节待讨论）。

---

## 2. 分支总览

| 分支 ID | 分支名称 | 核心主题 |
|---|---|---|
| `agriculture` | 农业政策 | 食物产量、粮仓体系、军粮保障 |
| `military` | 军事政策 | 兵力规模、战斗力、精锐兵种 |
| `diplomacy` | 外交政策 | 贸易、情报、霸权 |
| `industry` | 工业政策 | 资源采集、冶炼、建造效率 |
| `intelligence` | 情报政策 | （预留，待扩展） |

---

## 3. 数据库表结构

### 3.1 `policy_nodes` — 政策节点主表

存储所有政策节点的静态定义（策划数据，游戏内只读）。

```sql
CREATE TABLE policy_nodes (
    id               VARCHAR(64)  PRIMARY KEY,
    name             VARCHAR(128) NOT NULL,
    description      TEXT,
    branch           VARCHAR(32)  NOT NULL,  -- agriculture | military | diplomacy | industry | intelligence
    tier             INT          NOT NULL CHECK (tier BETWEEN 1 AND 5),
    tech_point_cost  INT          NOT NULL DEFAULT 1,  -- 解锁消耗的科技点数
    max_level        INT          NOT NULL DEFAULT 1,  -- 见第 8 节待讨论
    icon             VARCHAR(256)
);
```

---

### 3.2 `policy_prerequisites` — 前置条件表

**AND / OR 组合规则：`group_id` 相同的条件之间为 AND，不同 `group_id` 之间为 OR。**

#### 部长属性条件说明

当前部长属性条件采用**区间检测**：指定当前已指派的对应角色部长，其某项属性值是否落在 `[attr_min, attr_max]` 范围内。

- 仅设置 `attr_min`：属性值 >= attr_min
- 仅设置 `attr_max`：属性值 <= attr_max
- 同时设置：属性值在 `[attr_min, attr_max]` 之间

> **注意**：部长属性体系尚未完全设计。同一角色类型的部长（如所有农业部长）共享相同的属性类型，不同角色类型的部长可能具有不同的属性集合。属性名称（`minister_attr` 字段）在部长系统设计完成后再统一确认，此处以 `ability` / `loyalty` / `ambition` 作为占位示例。

```sql
CREATE TABLE policy_prerequisites (
    id               BIGSERIAL   PRIMARY KEY,
    policy_id        VARCHAR(64) NOT NULL REFERENCES policy_nodes(id),
    group_id         INT         NOT NULL DEFAULT 0,  -- 同 group AND，不同 group OR

    condition_type   VARCHAR(64) NOT NULL,

    -- condition_type = node_unlocked
    target_policy_id VARCHAR(64) REFERENCES policy_nodes(id),

    -- condition_type = minister_attr
    -- 检测当前已指派的 minister_role 角色部长，其 minister_attr 属性是否在区间内
    minister_role    VARCHAR(32),   -- military | agriculture | diplomacy | ...（随部长系统扩展）
    minister_attr    VARCHAR(64),   -- 属性名称，待部长系统设计完成后确认
    attr_min         NUMERIC,       -- 区间下限（含），为 NULL 则不限下限
    attr_max         NUMERIC,       -- 区间上限（含），为 NULL 则不限上限

    -- condition_type = resource_ge
    resource_type    VARCHAR(32),   -- food | ore | wood | refined_ore | engineering_material | ...
    numeric_threshold NUMERIC,

    -- 用于 UI 展示的人类可读描述
    description      TEXT
);

CREATE INDEX idx_prereq_policy ON policy_prerequisites(policy_id);
```

#### `condition_type` 枚举

**已确定：**

| 值 | 含义 |
|---|---|
| `node_unlocked` | 指定政策节点已解锁（使用 `target_policy_id`） |
| `minister_attr` | 当前指派的指定角色部长，其属性值在 `[attr_min, attr_max]` 区间内（使用 `minister_role` / `minister_attr` / `attr_min` / `attr_max`） |

**待讨论（见第 8 节）：**

| 值 | 含义 |
|---|---|
| `resource_ge` | 当前某资源储量 >= 阈值（使用 `resource_type` + `numeric_threshold`） |
| `turn_ge` | 当前回合数 >= 阈值（使用 `numeric_threshold`） |
| `controlled_nodes_ge` | 己方控制节点数 >= 阈值（使用 `numeric_threshold`） |
| `player_stat_ge` | 玩家某统计量 >= 阈值（扩展预留） |

---

### 3.3 `policy_effects` — 政策效果表

```sql
CREATE TABLE policy_effects (
    id              BIGSERIAL   PRIMARY KEY,
    policy_id       VARCHAR(64) NOT NULL REFERENCES policy_nodes(id),
    policy_level    INT         NOT NULL DEFAULT 1,  -- 多级政策按等级分别生效
    effect_type     VARCHAR(64) NOT NULL,
    target_resource VARCHAR(32),   -- food | ore | wood | refined_ore | ...
    target_stat     VARCHAR(64),   -- 全局统计量，如 trade_efficiency
    minister_role   VARCHAR(32),   -- 影响的部长角色
    minister_attr   VARCHAR(64),   -- 影响的部长属性
    modifier_type   VARCHAR(16) NOT NULL DEFAULT 'flat',  -- flat | percent | multiplier
    modifier_value  NUMERIC     NOT NULL,
    description     TEXT
);

CREATE INDEX idx_effect_policy ON policy_effects(policy_id);
```

#### `effect_type` 枚举

**已确定：**

| 值 | 含义 |
|---|---|
| `resource_production` | 资源每回合产量修正 |
| `resource_capacity` | 资源储量上限修正 |
| `unit_combat_power` | 单位战斗力修正 |
| `unit_limit` | 可用兵力上限修正 |
| `building_cost` | 建筑建造费用修正 |
| `building_hp` | 建筑生命值修正 |
| `token_action_unlock` | 解锁特殊令牌行动 |
| `action_free_use` | 某行动无需消耗令牌 |

**待讨论（见第 8 节）：**

| 值 | 含义 |
|---|---|
| `minister_attr_mod` | 部长属性修正（具体影响机制待定，见第 8.4 节） |

#### `modifier_type` 说明

| 值 | 计算方式 | 示例 |
|---|---|---|
| `flat` | 直接加减固定值 | food 产量 +2/回合 |
| `percent` | 基础值 × (1 + value) | food 产量 +20%，value = 0.2 |
| `multiplier` | 基础值 × value | 所有效果 ×1.1，value = 1.1 |

---

### 3.4 `player_policy_unlocks` — 玩家已解锁政策

解锁即生效，每回合结算时直接遍历此表计算所有政策效果叠加。

```sql
CREATE TABLE player_policy_unlocks (
    game_id          VARCHAR(64) NOT NULL,
    player_id        VARCHAR(64) NOT NULL,
    policy_id        VARCHAR(64) NOT NULL REFERENCES policy_nodes(id),
    level            INT         NOT NULL DEFAULT 1,
    unlocked_at_turn INT         NOT NULL,  -- 见第 6.5 节待讨论
    PRIMARY KEY (game_id, player_id, policy_id)
);
```

---

### 3.5 ER 关系图

```
policy_nodes ──< policy_prerequisites  (policy_id)
policy_nodes ──< policy_effects        (policy_id)
policy_nodes ──< player_policy_unlocks (policy_id)

policy_prerequisites.target_policy_id ──> policy_nodes  (自引用，node_unlocked 条件)
```

---

## 4. 科技树详细设计

> 以下前置条件中，部长属性名称（ability / loyalty / ambition）为占位符，
> 待部长系统设计完成后统一替换为正式属性名称。

### 4.1 农业政策树（Agriculture）

> 核心方向：解锁基础农业建筑，沿四条独立分支深入——增产、降低兵种维持消耗、减少运输损耗、强化仓储建筑效果。
>
> **注意**：农场和粮仓在未研究对应政策前**无法建造**，T1 两个节点是整条农业树的入口。兵种维持粮食消耗的具体数值（每兵种/回合）待数值设计阶段确认，此处以"−1/回合"为占位符。

#### T1：基础建筑解锁（两个并列起始节点）

| ID | Tier | 名称 | 前置条件 | 效果 |
|---|---|---|---|---|
| `agri_unlock_farm` | 1 | 开垦令 | 农业部长 [属性] ∈ [区间] | 解锁**农场**建造资格 |
| `agri_unlock_granary` | 1 | 仓储令 | 农业部长 [属性] ∈ [区间] | 解锁**粮仓**建造资格 |

#### T2–T4：四条分支

> 增产线、消耗线、运输线均以 `agri_unlock_farm` 为前置；仓储线以 `agri_unlock_granary` 为前置。

**— 增产线（Farm Production）**

| ID | Tier | 名称 | 前置条件 | 效果 |
|---|---|---|---|---|
| `agri_prod_1` | 2 | 精耕细作 | `agri_unlock_farm` 已解锁 AND 农业部长 [属性] ∈ [区间] | 每座农场产出 +1 food/回合（×2 → ×3） |
| `agri_prod_2` | 3 | 深耕厚植 | `agri_prod_1` 已解锁 AND 农业部长 [属性] ∈ [更高区间] | 每座农场产出再 +1 food/回合（×3 → ×4） |

**— 兵种维持消耗减少线（Consumption）**

按兵种类型逐级降低其每回合粮食维持消耗。

| ID | Tier | 名称 | 前置条件 | 效果 |
|---|---|---|---|---|
| `agri_cons_1` | 2 | 步卒节粮 | `agri_unlock_farm` 已解锁 AND 农业部长 [属性] ∈ [区间] | **步兵** / **弓手** 维持粮食消耗 −1/回合 |
| `agri_cons_2` | 3 | 骑兵精简 | `agri_cons_1` 已解锁 AND 农业部长 [属性] ∈ [更高区间] | **骑兵** 维持粮食消耗 −1/回合 |
| `agri_cons_3` | 4 | 辎重自给 | `agri_cons_2` 已解锁 AND 农业部长 [属性] ∈ [更高区间] | **攻城兵** / **破坏兵** 维持粮食消耗 −1/回合 |

**— 运输损耗减少线（Transport）**

| ID | Tier | 名称 | 前置条件 | 效果 |
|---|---|---|---|---|
| `agri_trans_1` | 2 | 粮道修缮 | `agri_unlock_farm` 已解锁 AND 农业部长 [属性] ∈ [区间] | 道路传输粮食损耗 −X%（待数值确认） |
| `agri_trans_2` | 3 | 粮道保障 | `agri_trans_1` 已解锁 AND 农业部长 [属性] ∈ [更高区间] | 道路传输粮食损耗再 −X% |

**— 仓储建筑效果增强线（Granary Enhancement）**

| ID | Tier | 名称 | 前置条件 | 效果 |
|---|---|---|---|---|
| `agri_stor_1` | 2 | 仓储精良 | `agri_unlock_granary` 已解锁 AND 农业部长 [属性] ∈ [区间] | 每座粮仓存储上限 +10（+20 → +30） |
| `agri_stor_2` | 3 | 广积粮 | `agri_stor_1` 已解锁 AND 农业部长 [属性] ∈ [更高区间] | 每座粮仓存储上限再 +20（+30 → +50） |

#### 树形结构

```
[T1] agri_unlock_farm 开垦令          [T1] agri_unlock_granary 仓储令
      │                                        │
      ├─[T2] agri_prod_1 精耕细作              └─[T2] agri_stor_1 仓储精良
      │        └─[T3] agri_prod_2 深耕厚植              └─[T3] agri_stor_2 广积粮
      │
      ├─[T2] agri_cons_1 步卒节粮（步兵/弓手）
      │        └─[T3] agri_cons_2 骑兵精简
      │                  └─[T4] agri_cons_3 辎重自给（攻城兵/破坏兵）
      │
      └─[T2] agri_trans_1 粮道修缮
               └─[T3] agri_trans_2 粮道保障
```

---

### 4.2 军事政策树（Military）

> 核心方向：T1 按兵种分组解锁，后续沿各兵种能力强化方向延伸。士气与 HP 回复方向见第 6 节待讨论。
>
> **注意**：所有兵种在未研究对应政策前**无法建造**。兵种攻击力/生命值/克制倍率的具体加成数值待数值设计阶段确认，此处以 +X 为占位符。

#### T1：解锁兵种（三个并列起始节点）

| ID | Tier | 名称 | 前置条件 | 效果 |
|---|---|---|---|---|
| `mil_unlock_basic` | 1 | 基础兵役令 | 军事部长 [属性] ∈ [低区间] | 解锁**步兵** + **弓手**建造资格 |
| `mil_unlock_elite` | 1 | 精锐兵役令 | 军事部长 [属性] ∈ [中高区间] | 解锁**骑兵** + **攻城兵**建造资格 |
| `mil_unlock_special` | 1 | 特种令 | 军事部长 [属性] ∈ [某区间] | 解锁**破坏兵**建造资格 |

#### T2–T4：各兵种能力强化分支

**— A线：步兵强化（来自基础兵役令）**

| ID | Tier | 名称 | 前置条件 | 效果 |
|---|---|---|---|---|
| `mil_inf_1` | 2 | 步卒精锐 | `mil_unlock_basic` 已解锁 AND 军事部长 [属性] ∈ [区间] | 步兵攻击力 +X |
| `mil_inf_2` | 3 | 步卒压制 | `mil_inf_1` 已解锁 AND 军事部长 [属性] ∈ [更高区间] | 步兵对破坏兵克制倍率 ×1.5 → ×2 |

**— B线：弓手强化（来自基础兵役令）**

| ID | Tier | 名称 | 前置条件 | 效果 |
|---|---|---|---|---|
| `mil_arc_1` | 2 | 远程精准 | `mil_unlock_basic` 已解锁 AND 军事部长 [属性] ∈ [区间] | 弓手攻击力 +X |
| `mil_arc_2` | 3 | 远程拦截 | `mil_arc_1` 已解锁 AND 军事部长 [属性] ∈ [更高区间] | 弓手对骑兵克制倍率 ×1.5 → ×2 |

**— C线：骑兵强化（来自精锐兵役令）**

| ID | Tier | 名称 | 前置条件 | 效果 |
|---|---|---|---|---|
| `mil_cav_1` | 2 | 铁骑冲锋 | `mil_unlock_elite` 已解锁 AND 军事部长 [属性] ∈ [区间] | 骑兵首回合冲锋加成 ×1.5 → ×2 |
| `mil_cav_2` | 3 | 疾风骑兵 | `mil_cav_1` 已解锁 AND 军事部长 [属性] ∈ [更高区间] | 骑兵移动力 +1/回合 |

**— D线：攻城兵强化（来自精锐兵役令）**

| ID | Tier | 名称 | 前置条件 | 效果 |
|---|---|---|---|---|
| `mil_siege_1` | 2 | 攻城利器 | `mil_unlock_elite` 已解锁 AND 军事部长 [属性] ∈ [区间] | 攻城兵攻城伤害倍率 ×3 → ×4 |
| `mil_siege_2` | 3 | 攻城野战 | `mil_siege_1` 已解锁 AND 军事部长 [属性] ∈ [更高区间] | 攻城兵野战攻击力 +X（弥补野战弱点） |

**— E线：破坏兵强化（来自特种令）**

| ID | Tier | 名称 | 前置条件 | 效果 |
|---|---|---|---|---|
| `mil_sab_1` | 2 | 穿插突袭 | `mil_unlock_special` 已解锁 AND 军事部长 [属性] ∈ [区间] | 破坏兵移动力 +1/回合 |
| `mil_sab_2` | 3 | 精准破坏 | `mil_sab_1` 已解锁 AND 军事部长 [属性] ∈ [更高区间] | 破坏兵对道路/建筑破坏效率 ×3 → ×4 |

#### 树形结构

```
[T1] mil_unlock_basic 基础兵役令        [T1] mil_unlock_elite 精锐兵役令        [T1] mil_unlock_special 特种令
      │                                        │                                        │
      ├─[T2] mil_inf_1 步卒精锐                ├─[T2] mil_cav_1 铁骑冲锋                └─[T2] mil_sab_1 穿插突袭
      │        └─[T3] mil_inf_2 步卒压制       │        └─[T3] mil_cav_2 疾风骑兵                └─[T3] mil_sab_2 精准破坏
      │                                        │
      └─[T2] mil_arc_1 远程精准                └─[T2] mil_siege_1 攻城利器
               └─[T3] mil_arc_2 远程拦截                └─[T3] mil_siege_2 攻城野战
```

---

### 4.3 外交政策树（Diplomacy）

> 待设计。

---

### 4.4 工业政策树（Industry）

> 待设计。

---

## 5. Seed Data 示例

以农业分支 T1 节点为例：

```sql
-- 节点定义（消耗 1 科技点）
INSERT INTO policy_nodes (id, name, description, branch, tier, tech_point_cost, max_level)
VALUES (
    'agri_basic_farming',
    '精耕细作',
    '推广精细化农业技术，提升全国粮食产量。',
    'agriculture', 1, 1, 1
);

-- 前置条件：农业部长 ability >= 3（仅设 attr_min，group_id = 0）
INSERT INTO policy_prerequisites
    (policy_id, group_id, condition_type, minister_role, minister_attr, attr_min, description)
VALUES
    ('agri_basic_farming', 0, 'minister_attr', 'agriculture', 'ability', 3, '农业部长 ability ≥ 3');

-- 效果：food 产量 +10%
INSERT INTO policy_effects
    (policy_id, policy_level, effect_type, target_resource, modifier_type, modifier_value, description)
VALUES
    ('agri_basic_farming', 1, 'resource_production', 'food', 'percent', 0.10, '粮食产量提升 10%');
```

区间条件示例（某假设政策要求部长属性在 [3, 7] 之间）：

```sql
INSERT INTO policy_prerequisites
    (policy_id, group_id, condition_type, minister_role, minister_attr, attr_min, attr_max, description)
VALUES
    ('some_policy', 0, 'minister_attr', 'agriculture', 'ability', 3, 7, '农业部长 ability 在 [3, 7] 之间');
```

OR 前置条件示例（两个节点满足其一即可）：

```sql
-- group_id = 0：policy_a 已解锁（OR 第一组）
INSERT INTO policy_prerequisites
    (policy_id, group_id, condition_type, target_policy_id, description)
VALUES
    ('some_policy', 0, 'node_unlocked', 'policy_a', 'policy_a 已解锁');

-- group_id = 1：policy_b 已解锁（OR 第二组）
INSERT INTO policy_prerequisites
    (policy_id, group_id, condition_type, target_policy_id, description)
VALUES
    ('some_policy', 1, 'node_unlocked', 'policy_b', 'policy_b 已解锁');
```

---

## 6. 待讨论

### 6.1 科技点数上限机制

玩家拥有最大科技点数上限（`max_tech_points`），决定了能同时解锁的政策总量。以下三种方案待决策：

#### 方案 A：科技建筑驱动

新增专属科技建筑（如"学宫""议事厅"），建造后全局增加科技点数上限，类似现有的建造点数（`build_points`）机制。玩家需要在地图上规划和保护这类建筑。

- 优点：与现有建筑体系统一，有空间策略深度
- 缺点：需要新增建筑类型；早期游戏可研政策数量严格受限

#### 方案 B：城邦控制驱动

按玩家当前控制的**城邦节点**数量计算科技点数上限，占领更多城邦即获得更多科技点数。

- 优点：与地图扩张直接绑定，强化领土争夺的意义
- 缺点：扩张快的玩家科技点数滚雪球优势明显；需要明确"城邦节点"的定义

#### 方案 C：回合数增长

科技点数上限随游戏回合数自动增长（如每 N 回合 +1），所有玩家共享相同增速。

- 优点：实现简单，节奏可控
- 缺点：玩家行为对科技上限无影响，策略深度较浅

> **当前状态**：未决策，三种方案可组合（如：基础回合增长 + 科技建筑额外加成）。

---

### 6.2 非部长类前置条件类型

`condition_type` 中除 `node_unlocked` 和 `minister_attr` 外，以下条件类型尚未确定是否引入，待决策后再写入 SQL 定义：

| 值 | 含义 | 待确认问题 |
|---|---|---|
| `resource_ge` | 当前某资源储量 >= 阈值 | 是否希望资源量影响政策解锁？还是资源只影响建造/行动？ |
| `turn_ge` | 当前回合数 >= 阈值 | 是否希望科技树有时间门控？还是完全由部长属性和上级节点决定节奏？ |
| `controlled_nodes_ge` | 己方控制节点数 >= 阈值 | 是否希望领土规模直接影响政策解锁？（与 6.1 方案 B 有关联） |
| `player_stat_ge` | 玩家某统计量 >= 阈值 | 扩展预留，统计量定义未确定 |

> **当前状态**：`policy_prerequisites` 表保留 `resource_type` 和 `numeric_threshold` 字段以备使用，但上述 condition_type 值在部分决策完成前不写入实际数据。

---

### 6.3 `max_level`（政策多级升级）

`policy_nodes.max_level` 字段当前默认为 1（即所有政策只能解锁一次）。保留字段以备扩展，待决策是否引入可升级政策机制。

若引入：
- `player_policy_unlocks.level` 记录当前等级
- `policy_effects.policy_level` 按等级分别定义效果值
- 升级需重新满足（更高的）前置条件并消耗额外科技点数

> **当前状态**：搁置，默认 max_level = 1，不影响现有设计。

---

### 6.4 `minister_attr_mod`（政策对部长属性的修正效果）

政策效果直接修改部长的属性值（如 ambition +1）在数值上容易实现，但**对游戏体验的影响难以直接量化**——玩家感知不到"ambition 从 4 变成 5"带来的变化，属性修正的意义需要通过部长的行为差异来体现。

核心问题：**政策应当如何影响部长对资源的调动能力？**

以军事部长为例，其自主行动可能涉及调动粮食、矿石等资源来维持军队数量或提升士气，政策能否以及如何扩大这种调动能力？

#### 方案 A：发言顺序优先权

政策赋予指定部长更早的发言权，发言靠前的部长在本回合内可以优先占用资源配额，后发言的部长只能使用剩余资源。

- 优点：机制直观，能体现部长之间的博弈
- 缺点：与现有内政→军事的固定阶段顺序不兼容；部长发言顺序本身尚未设计

#### 方案 B：资源调动比例上限

将资源按比例分成若干份额，政策效果体现为某角色部长最多可调动的资源百分比上限。政策等级（科技投入）越高，该部长可调动的资源比例越大。

- 优点：量化清晰，与现有资源体系直接对接；可独立于阶段顺序运行
- 缺点：需要设计"资源调动配额"这一新子系统；多个部长竞争同一资源时的分配规则需明确

> **当前状态**：未决策。`minister_attr_mod` 字段在表结构中保留，但在部长行为机制确定前，相关政策节点的效果数据暂不填写。

---

### 6.5 `unlocked_at_turn` 字段的用途

当前该字段仅作为历史记录（第几回合解锁了该政策）。其实际用途取决于科技点数上限的方案选择（见第 8.1 节）：

**若采用方案 A（科技建筑）或方案 B（城邦控制）：**

科技建筑被摧毁或城邦节点被夺回时，`max_tech_points` 可能下降。若玩家已用点数超出新上限，需要按解锁时间从晚到早**依次强制回退政策**，直到已用点数重新满足约束：

```
当 max_tech_points 下降时：
  超出量 = 已用点数 - 新 max_tech_points
  按 unlocked_at_turn DESC 排序玩家已解锁政策
  依次移除最晚解锁的政策，直到超出量 <= 0
```

此机制需要进一步讨论的问题：
- 强制回退对玩家体验冲击较大，是否需要给予一个"宽限回合"？
- 被回退的政策是否退还科技点数（通常应该退还）？
- 回退是否会触发连锁（被回退的政策本身是其他政策的前置节点）？

**若采用方案 C（回合数增长）：**

上限只增不减，该字段退化为纯历史记录，不参与任何游戏逻辑。

> **当前状态**：待第 6.1 节科技点上限方案确定后再决策。

---

### 6.6 军事政策扩展方向：士气（Morale）

士气作为一个全军共享的全局状态值，影响所有兵种的战斗表现：

- 士气高于阈值：攻击/防御 +X%
- 士气低（如断粮、连续败仗）：攻击/防御 -X%

军事政策树可沿此方向增加节点，例如：

| 方向 | 示例效果 |
|---|---|
| 基础士气提升 | 士气初始值更高 |
| 士气衰减抑制 | 断粮/败仗后士气下降速度更慢 |
| 高士气触发奖励 | 士气超过某阈值时所有兵种冲锋加成翻倍 |

> **待确认**：士气作为独立数值系统目前不在 GDD 中，引入前需先在 GDD 里定义士气的产生、衰减和影响规则。确认引入后，在军事树中以 `mil_morale_x` 系列节点追加。

---

### 6.7 军事政策扩展方向：HP 回复速度（Recovery）

GDD 中提到断粮会导致兵种每回合 -5% HP，但目前没有 HP 自然回复机制。政策可以引入此方向：

| 方向 | 示例效果 |
|---|---|
| 基础回复 | 己方控制节点内单位每回合回复 HP +X |
| 粮仓加速 | 相邻粮仓节点的单位回复速度翻倍 |
| 城镇庇护 | 城镇内单位 HP 回复速度大幅提升（强化守城优势） |

> **待确认**：HP 回复机制目前不在 GDD 中，引入前需先明确回复触发条件（驻扎？道路连通？）及与断粮减员机制的优先级关系。确认引入后，在军事树中以 `mil_recovery_x` 系列节点追加。

---

## 7. 待办事项

- [ ] 完善情报政策树（Intelligence 分支）内容
- [ ] 确认各分支 Tier 4/5 政策的数值平衡
- [ ] 部长属性体系设计完成后，将 `minister_attr` 占位名称替换为正式属性名
- [ ] 决策科技点数上限机制（见第 6.1 节）
- [ ] 决策非部长类前置条件类型是否引入（见第 6.2 节：resource_ge / turn_ge / controlled_nodes_ge）
- [ ] 决策政策对部长属性的影响机制（见第 6.4 节：发言顺序 vs 资源调动比例）
- [ ] 决策是否引入士气机制，确认后在军事树追加节点（见第 6.6 节）
- [ ] 决策是否引入 HP 回复机制，确认后在军事树追加节点（见第 6.7 节）
- [ ] 决策科技点上限方案确定后，明确 unlocked_at_turn 的强制回退逻辑（见第 6.5 节）
- [ ] 编写对应的 `policy.proto` 协议定义
- [ ] 实现服务端前置条件评估与效果叠加计算逻辑
