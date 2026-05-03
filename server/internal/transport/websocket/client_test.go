// Copyright (c) 2026 Panoptes Project Authors.
// Project: Panoptes
// Author: elebirds <hhmcn@outlook.com>
// Updated: 2026-04-30 00:00:00 +0800
// Description: 承载 WebSocket 入站日志或协议问题处理测试逻辑。

package websocket

import (
	"testing"

	pb "github.com/elebirds/panoptes/internal/gen/proto"
	"github.com/elebirds/panoptes/internal/transport/codec"
	transportproblem "github.com/elebirds/panoptes/internal/transport/problem"
)

func TestInboundMessageNameUsesDeepestCommandBody(t *testing.T) {
	tests := []struct {
		name  string
		frame *pb.ClientFrame
		want  string
	}{
		{
			name: "lobby create room",
			frame: &pb.ClientFrame{
				Target: &pb.ClientFrame_Lobby{
					Lobby: &pb.LobbyCommand{
						Body: &pb.LobbyCommand_CreateRoom{
							CreateRoom: &pb.MsgCreateRoom{},
						},
					},
				},
			},
			want: "MsgCreateRoom",
		},
		{
			name: "planning preview",
			frame: &pb.ClientFrame{
				Target: &pb.ClientFrame_Game{
					Game: &pb.GameCommand{
						Body: &pb.GameCommand_Planning{
							Planning: &pb.PlanningCommand{
								Body: &pb.PlanningCommand_BuildStructurePreview{
									BuildStructurePreview: &pb.MsgBuildStructurePreviewRequest{},
								},
							},
						},
					},
				},
			},
			want: "MsgBuildStructurePreviewRequest",
		},
		{
			name: "game chat",
			frame: &pb.ClientFrame{
				Target: &pb.ClientFrame_Game{
					Game: &pb.GameCommand{
						Body: &pb.GameCommand_Chat{
							Chat: &pb.ChatCommand{
								Body: &pb.ChatCommand_SendGameChat{
									SendGameChat: &pb.MsgSendGameChat{},
								},
							},
						},
					},
				},
			},
			want: "MsgSendGameChat",
		},
		{
			name: "command batch",
			frame: &pb.ClientFrame{
				Target: &pb.ClientFrame_Game{
					Game: &pb.GameCommand{
						Body: &pb.GameCommand_CommandBatch{
							CommandBatch: &pb.MsgGameCommandBatch{},
						},
					},
				},
			},
			want: "MsgGameCommandBatch",
		},
		{
			name: "empty planning command",
			frame: &pb.ClientFrame{
				Target: &pb.ClientFrame_Game{
					Game: &pb.GameCommand{
						Body: &pb.GameCommand_Planning{
							Planning: &pb.PlanningCommand{},
						},
					},
				},
			},
			want: "PlanningCommand",
		},
		{
			name:  "nil frame",
			frame: nil,
			want:  "unknown",
		},
	}

	for _, tt := range tests {
		t.Run(tt.name, func(t *testing.T) {
			if got := inboundMessageName(tt.frame); got != tt.want {
				t.Fatalf("inboundMessageName() = %q, want %q", got, tt.want)
			}
		})
	}
}

func TestSendProblemPreservesRequestAndTraceMetadata(t *testing.T) {
	client := &Client{
		playerID: "player-1",
		send:     make(chan []byte, 1),
	}

	client.sendProblem(&pb.CommandMeta{
		RequestId: "req-1",
		TraceId:   "trace-1",
	}, transportproblem.InvalidRequest("bad command"))

	raw := <-client.send
	frame, err := codec.DecodeServerFrame(raw)
	if err != nil {
		t.Fatalf("DecodeServerFrame() error = %v", err)
	}
	if frame.GetProblem().GetCode() != "invalid_request" {
		t.Fatalf("problem.code = %q", frame.GetProblem().GetCode())
	}
	if frame.GetMeta().GetRequestId() != "req-1" || frame.GetMeta().GetTraceId() != "trace-1" {
		t.Fatalf("meta = (%q, %q), want (req-1, trace-1)", frame.GetMeta().GetRequestId(), frame.GetMeta().GetTraceId())
	}
	if frame.GetMeta().GetServerUnixMillis() == 0 {
		t.Fatalf("server_unix_millis = 0, want non-zero")
	}
}
