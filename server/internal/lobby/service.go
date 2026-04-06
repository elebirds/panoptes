package lobby

import (
	"context"
	"crypto/rand"
	"errors"
	"fmt"
	"log/slog"
	"sync"
	"time"

	"github.com/elebirds/panoptes/internal/auth"
	pb "github.com/elebirds/panoptes/internal/gen/proto"
	"github.com/elebirds/panoptes/internal/transport"
	"github.com/google/uuid"
	"google.golang.org/protobuf/proto"
)

const (
	minPlayers          = 2
	maxPlayers          = 8
	roomCodeLength      = 6
	countdownSeconds    = 3
	roomCodeCharset     = "23456789ABCDEFGHJKLMNPQRSTUVWXYZ"
	maxCodeGenerateTrys = 32
)

type LobbyService struct {
	store         LobbyStore
	gameTransport transport.GameTransport
	authSvc       *auth.Service
	defaultMax    int
	devMode       bool

	mu             sync.RWMutex
	onGameStart    func(room *Room)
	countdownDelay time.Duration
}

func NewService(store LobbyStore, gameTransport transport.GameTransport, authSvc *auth.Service, defaultMax int, devMode bool) *LobbyService {
	if defaultMax < minPlayers || defaultMax > maxPlayers {
		defaultMax = minPlayers
	}

	return &LobbyService{
		store:          store,
		gameTransport:  gameTransport,
		authSvc:        authSvc,
		defaultMax:     defaultMax,
		devMode:        devMode,
		countdownDelay: countdownSeconds * time.Second,
	}
}

func (s *LobbyService) CreateRoom(ctx context.Context, hostID string, name string, maxPlayers int) error {
	if maxPlayers == 0 {
		maxPlayers = s.defaultMax
	}
	if !isValidPlayerCount(maxPlayers) {
		return ErrInvalidPlayers
	}
	if _, err := s.store.GetRoomByPlayerID(ctx, hostID); err == nil {
		return ErrAlreadyInRoom
	} else if err != nil && !errors.Is(err, ErrRoomNotFound) {
		return err
	}

	user, err := s.authSvc.GetUserByID(ctx, hostID)
	if err != nil {
		return err
	}

	code, err := s.generateRoomCode(ctx)
	if err != nil {
		return err
	}

	room := NewRoom(uuid.NewString(), code, name, hostID, user.Username, maxPlayers, s.devMode)

	if err := s.store.CreateRoom(ctx, room); err != nil {
		return err
	}

	if err := s.sendToPlayer(hostID, &pb.MsgRoomCreated{
		RoomId:   room.ID,
		RoomCode: room.Code,
	}); err != nil {
		return err
	}

	return s.sendRoomState(room)
}

func (s *LobbyService) JoinRoom(ctx context.Context, playerID string, code string) error {
	if _, err := s.store.GetRoomByPlayerID(ctx, playerID); err == nil {
		return ErrAlreadyInRoom
	} else if err != nil && !errors.Is(err, ErrRoomNotFound) {
		return err
	}

	room, err := s.store.GetRoomByCode(ctx, code)
	if err != nil {
		return err
	}
	room.SetDevMode(s.devMode)
	if room.Status != RoomStatusWaiting {
		return ErrInvalidStatus
	}

	user, err := s.authSvc.GetUserByID(ctx, playerID)
	if err != nil {
		return err
	}
	if err := room.AddPlayer(playerID, user.Username); err != nil {
		return err
	}
	room.Status = RoomStatusWaiting

	if err := s.store.UpdateRoom(ctx, room); err != nil {
		return err
	}

	return s.sendRoomState(room)
}

func (s *LobbyService) LeaveRoom(ctx context.Context, playerID string) error {
	room, err := s.store.GetRoomByPlayerID(ctx, playerID)
	if err != nil {
		return err
	}
	room.SetDevMode(s.devMode)

	if room.HostID == playerID {
		if err := s.store.DeleteRoom(ctx, room.ID); err != nil {
			return err
		}
		s.sendToRoomPlayers(room, &pb.MsgLobbyError{
			Code:    "room_dissolved",
			Message: "room dissolved",
		})
		return nil
	}

	room.RemovePlayer(playerID)
	if room.IsAllReady() {
		room.Status = RoomStatusReady
	} else {
		room.Status = RoomStatusWaiting
	}

	if err := s.store.UpdateRoom(ctx, room); err != nil {
		return err
	}

	return s.sendRoomState(room)
}

func (s *LobbyService) ReadyUp(ctx context.Context, playerID string) error {
	room, err := s.store.GetRoomByPlayerID(ctx, playerID)
	if err != nil {
		return err
	}
	room.SetDevMode(s.devMode)
	if room.Status != RoomStatusWaiting && room.Status != RoomStatusReady {
		return ErrInvalidStatus
	}

	player, ok := room.GetPlayer(playerID)
	if !ok {
		return ErrPlayerNotFound
	}
	if err := room.SetReady(playerID, !player.IsReady); err != nil {
		return err
	}

	if room.IsAllReady() {
		room.Status = RoomStatusReady
	} else {
		room.Status = RoomStatusWaiting
	}

	if err := s.store.UpdateRoom(ctx, room); err != nil {
		return err
	}
	if err := s.sendRoomState(room); err != nil {
		return err
	}

	if !room.IsAllReady() {
		return nil
	}

	room.Status = RoomStatusStarting
	if err := s.store.UpdateRoom(ctx, room); err != nil {
		return err
	}
	if err := s.sendRoomState(room); err != nil {
		return err
	}

	s.startCountdown(ctx, room)
	return nil
}

func (s *LobbyService) AddBot(ctx context.Context, operatorID string) error {
	room, err := s.store.GetRoomByPlayerID(ctx, operatorID)
	if err != nil {
		return err
	}
	room.SetDevMode(s.devMode)
	if room.HostID != operatorID {
		return ErrNotHost
	}

	if _, err := room.AddBot(); err != nil {
		return err
	}
	if room.IsAllReady() {
		room.Status = RoomStatusReady
	} else {
		room.Status = RoomStatusWaiting
	}
	if err := s.store.UpdateRoom(ctx, room); err != nil {
		return err
	}

	return s.sendRoomState(room)
}

func (s *LobbyService) KickPlayer(ctx context.Context, operatorID, targetID string) error {
	room, err := s.store.GetRoomByPlayerID(ctx, operatorID)
	if err != nil {
		return err
	}
	room.SetDevMode(s.devMode)

	kicked, err := room.KickPlayer(operatorID, targetID)
	if err != nil {
		return err
	}
	if room.IsAllReady() {
		room.Status = RoomStatusReady
	} else {
		room.Status = RoomStatusWaiting
	}
	if err := s.store.UpdateRoom(ctx, room); err != nil {
		return err
	}
	if kicked != nil && !kicked.IsBot {
		if err := s.sendToPlayer(kicked.PlayerID, &pb.MsgPlayerKicked{
			PlayerId: kicked.PlayerID,
			Username: kicked.Username,
		}); err != nil {
			return err
		}
	}

	return s.sendRoomState(room)
}

func (s *LobbyService) SetGameStartCallback(fn func(room *Room)) {
	s.mu.Lock()
	defer s.mu.Unlock()
	s.onGameStart = fn
}

func (s *LobbyService) startCountdown(ctx context.Context, room *Room) {
	roomCopy := cloneRoomValue(room)

	go func() {
		s.sendToRoomPlayers(roomCopy, &pb.MsgGameStarting{Countdown: countdownSeconds})

		timer := time.NewTimer(s.countdownDelay)
		defer timer.Stop()

		select {
		case <-ctx.Done():
			return
		case <-timer.C:
		}

		currentRoom, err := s.store.GetRoom(context.Background(), roomCopy.ID)
		if err != nil {
			if !errors.Is(err, ErrRoomNotFound) {
				slog.Warn("读取倒计时房间失败", "room_id", roomCopy.ID, "error", err)
			}
			return
		}
		if currentRoom.Status != RoomStatusStarting {
			return
		}
		currentRoom.SetDevMode(s.devMode)

		currentRoom.Status = RoomStatusInGame
		if err := s.store.UpdateRoom(context.Background(), currentRoom); err != nil {
			slog.Warn("更新房间游戏状态失败", "room_id", currentRoom.ID, "error", err)
			return
		}

		s.mu.RLock()
		onGameStart := s.onGameStart
		s.mu.RUnlock()

		if onGameStart != nil {
			onGameStart(cloneRoomValue(currentRoom))
		}
	}()
}

func (s *LobbyService) sendRoomState(room *Room) error {
	return s.sendToRoomPlayers(room, room.ToProto())
}

func (s *LobbyService) sendToRoomPlayers(room *Room, msg proto.Message) error {
	var firstErr error
	for _, player := range room.Players {
		if player.IsBot {
			continue
		}
		if err := s.sendToPlayer(player.PlayerID, msg); err != nil && firstErr == nil {
			firstErr = err
		}
	}
	return firstErr
}

func (s *LobbyService) sendToPlayer(playerID string, msg proto.Message) error {
	if err := s.gameTransport.Send(playerID, msg); err != nil {
		return fmt.Errorf("send to player %s: %w", playerID, err)
	}
	return nil
}

func (s *LobbyService) generateRoomCode(ctx context.Context) (string, error) {
	for range maxCodeGenerateTrys {
		code, err := randomRoomCode()
		if err != nil {
			return "", err
		}
		if _, err := s.store.GetRoomByCode(ctx, code); errors.Is(err, ErrRoomNotFound) {
			return code, nil
		} else if err != nil {
			return "", err
		}
	}
	return "", errors.New("failed to generate unique room code")
}

func randomRoomCode() (string, error) {
	buf := make([]byte, roomCodeLength)
	random := make([]byte, roomCodeLength)
	if _, err := rand.Read(random); err != nil {
		return "", fmt.Errorf("generate room code: %w", err)
	}
	for idx := range buf {
		buf[idx] = roomCodeCharset[int(random[idx])%len(roomCodeCharset)]
	}
	return string(buf), nil
}

func isValidPlayerCount(count int) bool {
	return count >= minPlayers && count <= maxPlayers
}

func cloneRoomValue(room *Room) *Room {
	if room == nil {
		return nil
	}

	cloned := *room
	cloned.Players = make([]*RoomPlayer, 0, len(room.Players))
	for _, player := range room.Players {
		playerCopy := *player
		cloned.Players = append(cloned.Players, &playerCopy)
	}
	return &cloned
}
