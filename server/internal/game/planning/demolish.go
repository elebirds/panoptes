package planning

import (
	"strings"

	"github.com/elebirds/panoptes/internal/domain"
	"github.com/elebirds/panoptes/internal/engine/economy"
	pb "github.com/elebirds/panoptes/internal/gen/proto"
)

func (s *Service) handleDemolishBuilding(delivery commandDelivery, room Session, playerID string, nodeID string) (handleIntentResult, error) {
	eval := evaluateDemolishCommand(room.State(), playerID, nodeID)
	if !eval.OK {
		delivery.send(&pb.MsgDemolishBuildingResult{
			Success:         false,
			NodeId:          strings.TrimSpace(nodeID),
			BuildingTypeId:  eval.BuildingTypeID,
			ErrorCode:       eval.ErrorCode,
			FeedbackMessage: eval.FeedbackMessage,
			FeedbackDetails: eval.FeedbackDetails,
		})
		return rejectedHandleIntentResult(eval.ErrorCode), nil
	}

	room.QueueDemolishOrder(domain.DemolishOrder{
		PlayerID:     playerID,
		NodeID:       strings.TrimSpace(nodeID),
		BuildingType: eval.BuildingTypeID,
	})
	delivery.sendWithSnapshot(&pb.MsgDemolishBuildingResult{
		Success:        true,
		NodeId:         strings.TrimSpace(nodeID),
		BuildingTypeId: eval.BuildingTypeID,
	})
	return acceptedHandleIntentResult(), nil
}

type demolishCommandEvaluation struct {
	OK              bool
	ErrorCode       string
	BuildingTypeID  string
	FeedbackMessage string
	FeedbackDetails []*pb.FeedbackDetail
}

func evaluateDemolishCommand(state *domain.GameState, playerID string, nodeID string) demolishCommandEvaluation {
	nodeID = strings.TrimSpace(nodeID)
	ctx := feedbackContext{NodeID: nodeID}
	eval := demolishCommandEvaluation{}
	validation := economy.ValidateDemolishOrder(state, playerID, nodeID)
	if !validation.OK {
		eval.ErrorCode = validation.ErrorCode
		if validation.NodeEntry != nil && validation.NodeEntry.HasComponent(domain.BuildingC) {
			eval.BuildingTypeID = strings.TrimSpace(string(domain.BuildingC.Get(validation.NodeEntry).Type))
		}
		eval.FeedbackMessage, eval.FeedbackDetails = demolishFeedback(state, playerID, ctx, eval.ErrorCode)
		return eval
	}
	eval.OK = true
	eval.BuildingTypeID = strings.TrimSpace(string(validation.Building.Type))
	return eval
}
