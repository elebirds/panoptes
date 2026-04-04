package http

import (
	"encoding/json"
	"net/http"

	"github.com/elebirds/panoptes/internal/auth"
)

type AuthHandler struct {
	authSvc   *auth.Service
	jwtSecret string
}

func NewAuthHandler(authSvc *auth.Service, jwtSecret string) *AuthHandler {
	return &AuthHandler{
		authSvc:   authSvc,
		jwtSecret: jwtSecret,
	}
}

type registerRequest struct {
	Username string `json:"username"`
	Password string `json:"password"`
}

type registerResponse struct {
	PlayerID string `json:"player_id"`
	Username string `json:"username"`
}

type loginRequest struct {
	Username string `json:"username"`
	Password string `json:"password"`
}

type loginResponse struct {
	Token    string `json:"token"`
	PlayerID string `json:"player_id"`
	Username string `json:"username"`
}

type userResponse struct {
	PlayerID string `json:"player_id"`
	Username string `json:"username"`
}

func (h *AuthHandler) Register(w http.ResponseWriter, r *http.Request) {
	var req registerRequest
	if err := json.NewDecoder(r.Body).Decode(&req); err != nil {
		writeError(w, http.StatusBadRequest, "invalid_request")
		return
	}

	user, err := h.authSvc.Register(r.Context(), req.Username, req.Password)
	if err != nil {
		switch err {
		case auth.ErrUserExists:
			writeError(w, http.StatusConflict, "user_exists")
		default:
			writeError(w, http.StatusInternalServerError, "internal_error")
		}
		return
	}

	writeJSON(w, http.StatusCreated, registerResponse{
		PlayerID: user.ID,
		Username: user.Username,
	})
}

func (h *AuthHandler) Login(w http.ResponseWriter, r *http.Request) {
	var req loginRequest
	if err := json.NewDecoder(r.Body).Decode(&req); err != nil {
		writeError(w, http.StatusBadRequest, "invalid_request")
		return
	}

	user, token, err := h.authSvc.Login(r.Context(), req.Username, req.Password)
	if err != nil {
		switch err {
		case auth.ErrUserNotFound, auth.ErrInvalidCredentials:
			writeError(w, http.StatusUnauthorized, "invalid_credentials")
		default:
			writeError(w, http.StatusInternalServerError, "internal_error")
		}
		return
	}

	writeJSON(w, http.StatusOK, loginResponse{
		Token:    token,
		PlayerID: user.ID,
		Username: user.Username,
	})
}

func (h *AuthHandler) GetUser(w http.ResponseWriter, r *http.Request) {
	username := r.PathValue("username")
	if username == "" {
		writeError(w, http.StatusBadRequest, "invalid_request")
		return
	}

	user, err := h.authSvc.GetUserByUsername(r.Context(), username)
	if err != nil {
		switch err {
		case auth.ErrUserNotFound:
			writeError(w, http.StatusNotFound, "user_not_found")
		default:
			writeError(w, http.StatusInternalServerError, "internal_error")
		}
		return
	}

	writeJSON(w, http.StatusOK, userResponse{
		PlayerID: user.ID,
		Username: user.Username,
	})
}
