package codec

import (
	"time"

	pb "github.com/elebirds/panoptes/internal/gen/proto"
	transportproblem "github.com/elebirds/panoptes/internal/transport/problem"
	"google.golang.org/protobuf/encoding/protojson"
	"google.golang.org/protobuf/proto"
)

var marshalOptions = protojson.MarshalOptions{}
var unmarshalOptions = protojson.UnmarshalOptions{
	DiscardUnknown: true,
}

func EncodeClientFrame(frame *pb.ClientFrame) ([]byte, error) {
	if frame == nil {
		return nil, transportproblem.InvalidRequest("client frame is nil")
	}
	return marshalOptions.Marshal(frame)
}

func DecodeClientFrame(raw []byte) (*pb.ClientFrame, error) {
	frame := &pb.ClientFrame{}
	if err := unmarshalOptions.Unmarshal(raw, frame); err != nil {
		return nil, transportproblem.InvalidRequest("invalid client frame")
	}
	if frame.GetMeta().GetRequestId() == "" {
		return nil, transportproblem.InvalidRequest("request_id is required")
	}
	if frame.Target == nil {
		return nil, transportproblem.InvalidRequest("client frame target is required")
	}
	return frame, nil
}

func EncodeServerFrame(frame *pb.ServerFrame) ([]byte, error) {
	if frame == nil {
		return nil, transportproblem.InvalidRequest("server frame is nil")
	}
	return marshalOptions.Marshal(frame)
}

func DecodeServerFrame(raw []byte) (*pb.ServerFrame, error) {
	frame := &pb.ServerFrame{}
	if err := unmarshalOptions.Unmarshal(raw, frame); err != nil {
		return nil, transportproblem.InvalidRequest("invalid server frame")
	}
	return frame, nil
}

func WrapServerMessage(msg proto.Message, meta *pb.EventMeta) (*pb.ServerFrame, error) {
	if msg == nil {
		return nil, transportproblem.InvalidRequest("server message is nil")
	}
	if frame, ok := msg.(*pb.ServerFrame); ok {
		return frame, nil
	}

	frame := &pb.ServerFrame{Meta: cloneEventMeta(meta)}

	switch typed := msg.(type) {
	case *pb.Problem:
		frame.Target = &pb.ServerFrame_Problem{Problem: typed}
	case *pb.MsgLoginSuccess:
		frame.Target = &pb.ServerFrame_Auth{Auth: &pb.AuthEvent{Body: &pb.AuthEvent_LoginSuccess{LoginSuccess: typed}}}
	case *pb.MsgAuthError:
		frame.Target = &pb.ServerFrame_Auth{Auth: &pb.AuthEvent{Body: &pb.AuthEvent_AuthError{AuthError: typed}}}
	case *pb.MsgClientRuntimeConfig:
		frame.Target = &pb.ServerFrame_Auth{Auth: &pb.AuthEvent{Body: &pb.AuthEvent_ClientRuntimeConfig{ClientRuntimeConfig: typed}}}
	case *pb.MsgRoomCreated:
		frame.Target = &pb.ServerFrame_Lobby{Lobby: &pb.LobbyEvent{Body: &pb.LobbyEvent_RoomCreated{RoomCreated: typed}}}
	case *pb.MsgRoomState:
		frame.Target = &pb.ServerFrame_Lobby{Lobby: &pb.LobbyEvent{Body: &pb.LobbyEvent_RoomState{RoomState: typed}}}
	case *pb.MsgGameStarting:
		frame.Target = &pb.ServerFrame_Lobby{Lobby: &pb.LobbyEvent{Body: &pb.LobbyEvent_GameStarting{GameStarting: typed}}}
	case *pb.MsgPlayerKicked:
		frame.Target = &pb.ServerFrame_Lobby{Lobby: &pb.LobbyEvent{Body: &pb.LobbyEvent_PlayerKicked{PlayerKicked: typed}}}
	case *pb.MsgLobbyError:
		frame.Target = &pb.ServerFrame_Lobby{Lobby: &pb.LobbyEvent{Body: &pb.LobbyEvent_LobbyError{LobbyError: typed}}}
	case *pb.MsgStaticCatalogManifest:
		frame.Target = &pb.ServerFrame_Game{Game: &pb.GameEvent{Body: &pb.GameEvent_StaticCatalogManifest{StaticCatalogManifest: typed}}}
	case *pb.MsgStaticCatalogSnapshot:
		frame.Target = &pb.ServerFrame_Game{Game: &pb.GameEvent{Body: &pb.GameEvent_StaticCatalogSnapshot{StaticCatalogSnapshot: typed}}}
	case *pb.MsgGameInit:
		frame.Target = &pb.ServerFrame_Game{Game: &pb.GameEvent{Body: &pb.GameEvent_GameInit{GameInit: typed}}}
	case *pb.MsgPlanningStart:
		frame.Target = &pb.ServerFrame_Game{Game: &pb.GameEvent{Body: &pb.GameEvent_PlanningStart{PlanningStart: typed}}}
	case *pb.MsgPlanningSnapshot:
		frame.Target = &pb.ServerFrame_Game{Game: &pb.GameEvent{Body: &pb.GameEvent_PlanningSnapshot{PlanningSnapshot: typed}}}
	case *pb.MsgPlanningPathPreviewResponse:
		frame.Target = &pb.ServerFrame_Game{Game: &pb.GameEvent{Body: &pb.GameEvent_PlanningPathPreviewResponse{PlanningPathPreviewResponse: typed}}}
	case *pb.MsgTokenResult:
		frame.Target = &pb.ServerFrame_Game{Game: &pb.GameEvent{Body: &pb.GameEvent_TokenResult{TokenResult: typed}}}
	case *pb.MsgRevealResult:
		frame.Target = &pb.ServerFrame_Game{Game: &pb.GameEvent{Body: &pb.GameEvent_RevealResult{RevealResult: typed}}}
	case *pb.MsgResearchResult:
		frame.Target = &pb.ServerFrame_Game{Game: &pb.GameEvent{Body: &pb.GameEvent_ResearchResult{ResearchResult: typed}}}
	case *pb.MsgSetPolicyResult:
		frame.Target = &pb.ServerFrame_Game{Game: &pb.GameEvent{Body: &pb.GameEvent_SetPolicyResult{SetPolicyResult: typed}}}
	case *pb.MsgSetInstitutionLoadoutResult:
		frame.Target = &pb.ServerFrame_Game{Game: &pb.GameEvent{Body: &pb.GameEvent_SetInstitutionLoadoutResult{SetInstitutionLoadoutResult: typed}}}
	case *pb.MsgIssueUnitOrderResult:
		frame.Target = &pb.ServerFrame_Game{Game: &pb.GameEvent{Body: &pb.GameEvent_IssueUnitOrderResult{IssueUnitOrderResult: typed}}}
	case *pb.MsgSetBuildingRecipeResult:
		frame.Target = &pb.ServerFrame_Game{Game: &pb.GameEvent{Body: &pb.GameEvent_SetBuildingRecipeResult{SetBuildingRecipeResult: typed}}}
	case *pb.MsgBuildStructureResult:
		frame.Target = &pb.ServerFrame_Game{Game: &pb.GameEvent{Body: &pb.GameEvent_BuildStructureResult{BuildStructureResult: typed}}}
	case *pb.MsgTurnReport:
		frame.Target = &pb.ServerFrame_Game{Game: &pb.GameEvent{Body: &pb.GameEvent_TurnReport{TurnReport: typed}}}
	case *pb.MsgTurnSettlement:
		frame.Target = &pb.ServerFrame_Game{Game: &pb.GameEvent{Body: &pb.GameEvent_TurnSettlement{TurnSettlement: typed}}}
	case *pb.MsgGameOver:
		frame.Target = &pb.ServerFrame_Game{Game: &pb.GameEvent{Body: &pb.GameEvent_GameOver{GameOver: typed}}}
	case *pb.MsgMinisterReportChunk:
		frame.Target = &pb.ServerFrame_Game{Game: &pb.GameEvent{Body: &pb.GameEvent_MinisterReportChunk{MinisterReportChunk: typed}}}
	case *pb.MsgMinisterMetrics:
		frame.Target = &pb.ServerFrame_Game{Game: &pb.GameEvent{Body: &pb.GameEvent_MinisterMetrics{MinisterMetrics: typed}}}
	default:
		return nil, transportproblem.InternalError("unsupported outbound message type")
	}

	return frame, nil
}

func EncodeServerMessage(msg proto.Message, meta *pb.EventMeta) ([]byte, error) {
	frame, err := WrapServerMessage(msg, meta)
	if err != nil {
		return nil, err
	}
	return EncodeServerFrame(frame)
}

func AsProblem(err error) (*pb.Problem, bool) {
	return transportproblem.AsProblem(err)
}

func cloneEventMeta(meta *pb.EventMeta) *pb.EventMeta {
	var cloned *pb.EventMeta
	if meta == nil {
		cloned = &pb.EventMeta{}
	} else {
		cloned = proto.Clone(meta).(*pb.EventMeta)
	}
	if cloned.GetServerUnixMillis() == 0 {
		cloned.ServerUnixMillis = time.Now().UnixMilli()
	}
	return cloned
}
