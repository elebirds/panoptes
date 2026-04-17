package participant

type Kind string

const (
	KindHuman Kind = "human"
	KindBot   Kind = "bot"
	KindAI    Kind = "ai"
)

type Participant struct {
	ID       string
	Username string
	Kind     Kind
}

func (p Participant) IsHuman() bool {
	return p.Kind == KindHuman
}

func (p Participant) IsAutonomous() bool {
	return p.Kind == KindBot || p.Kind == KindAI
}

type Spec struct {
	ID       string
	Username string
	Kind     Kind
}
