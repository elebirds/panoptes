package staticdata

type MinisterSkillCard struct {
	ID            string   `json:"id"`
	Name          string   `json:"name"`
	Description   string   `json:"description"`
	IconKey       string   `json:"icon_key"`
	RoleTags      []string `json:"role_tags"`
	Rarity        string   `json:"rarity"`
	EffectKey     string   `json:"effect_key"`
	TriggerTiming string   `json:"trigger_timing"`
	DelayTurns    int      `json:"delay_turns"`
	DurationTurns int      `json:"duration_turns"`
	SortOrder     int      `json:"sort_order"`
	Tags          []string `json:"tags,omitempty"`
}
