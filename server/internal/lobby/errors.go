package lobby

import "errors"

var (
	ErrRoomFull       = errors.New("room_full")
	ErrAlreadyInRoom  = errors.New("already_in_room")
	ErrRoomNotFound   = errors.New("room_not_found")
	ErrPlayerNotFound = errors.New("player_not_found")
	ErrNotHost        = errors.New("not_host")
	ErrInvalidStatus  = errors.New("invalid_status")
	ErrInvalidPlayers = errors.New("invalid_player_count")
)
