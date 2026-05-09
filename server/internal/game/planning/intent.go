package planning

type Intent interface {
	isPlanningIntent()
}

type IntentEnvelope struct {
	ParticipantID string
	RequestID     string
	TraceID       string
	Intent        Intent
}

type SetPolicyIntent struct {
	NationalPolicyID string
}

func (SetPolicyIntent) isPlanningIntent() {}

type SetInstitutionLoadoutIntent struct {
	InstitutionIDs []string
}

func (SetInstitutionLoadoutIntent) isPlanningIntent() {}

type BuildStructureIntent struct {
	NodeID         string
	BuildingTypeID string
	CityID         string
}

func (BuildStructureIntent) isPlanningIntent() {}

type DemolishBuildingIntent struct {
	NodeID string
}

func (DemolishBuildingIntent) isPlanningIntent() {}

type RevealNodeIntent struct {
	NodeID string
}

func (RevealNodeIntent) isPlanningIntent() {}

type SetResearchTargetIntent struct {
	TechnologyID string
}

func (SetResearchTargetIntent) isPlanningIntent() {}

type SetBuildingRecipeIntent struct {
	NodeID   string
	RecipeID string
}

func (SetBuildingRecipeIntent) isPlanningIntent() {}

type CancelBuildingRecipeIntent struct {
	NodeID string
}

func (CancelBuildingRecipeIntent) isPlanningIntent() {}

type SetMinisterDirectiveIntent struct {
	MinisterRole  string
	DirectiveType string
	DraftID       string
	SkillCardID   string
	CandidateID   string
}

func (SetMinisterDirectiveIntent) isPlanningIntent() {}

type IssueUnitOrderIntent struct {
	UnitID          string
	Action          string
	TargetNodeID    string
	TargetUnitID    string
	SecondaryNodeID string
	Params          map[string]string
}

func (IssueUnitOrderIntent) isPlanningIntent() {}

type CancelUnitOrderIntent struct {
	UnitID string
}

func (CancelUnitOrderIntent) isPlanningIntent() {}

type SubmitTurnIntent struct{}

func (SubmitTurnIntent) isPlanningIntent() {}
