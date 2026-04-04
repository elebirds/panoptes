package auth

import (
	"context"
	"errors"
	"time"

	"github.com/golang-jwt/jwt/v5"
	"github.com/google/uuid"
	"golang.org/x/crypto/bcrypt"
)

type UserStore interface {
	Create(ctx context.Context, user *User) error
	GetByUsername(ctx context.Context, username string) (*User, error)
	GetByID(ctx context.Context, id string) (*User, error)
}

type Service struct {
	userStore     UserStore
	jwtSecret     string
	jwtExpiration int // 秒
}

func NewService(userStore UserStore, jwtSecret string, jwtExpiration int) *Service {
	return &Service{
		userStore:     userStore,
		jwtSecret:     jwtSecret,
		jwtExpiration: jwtExpiration,
	}
}

func (s *Service) Register(ctx context.Context, username, password string) (*User, error) {
	// Check if user already exists
	_, err := s.userStore.GetByUsername(ctx, username)
	if err == nil {
		return nil, ErrUserExists
	}

	// Hash password
	hash, err := bcrypt.GenerateFromPassword([]byte(password), bcrypt.DefaultCost)
	if err != nil {
		return nil, err
	}

	user := &User{
		ID:           uuid.New().String(),
		Username:     username,
		PasswordHash: string(hash),
		CreatedAt:    time.Now(),
		UpdatedAt:    time.Now(),
	}

	if err := s.userStore.Create(ctx, user); err != nil {
		return nil, err
	}

	return user, nil
}

func (s *Service) Login(ctx context.Context, username, password string) (*User, string, error) {
	user, err := s.userStore.GetByUsername(ctx, username)
	if err != nil {
		return nil, "", ErrInvalidCredentials
	}

	if err := bcrypt.CompareHashAndPassword([]byte(user.PasswordHash), []byte(password)); err != nil {
		return nil, "", ErrInvalidCredentials
	}

	token, err := s.generateToken(user)
	if err != nil {
		return nil, "", err
	}

	return user, token, nil
}

func (s *Service) GetUserByID(ctx context.Context, id string) (*User, error) {
	return s.userStore.GetByID(ctx, id)
}

func (s *Service) GetUserByUsername(ctx context.Context, username string) (*User, error) {
	return s.userStore.GetByUsername(ctx, username)
}

func (s *Service) generateToken(user *User) (string, error) {
	claims := jwt.MapClaims{
		"player_id": user.ID,
		"username":  user.Username,
		"exp":       time.Now().Add(time.Duration(s.jwtExpiration) * time.Second).Unix(),
	}

	token := jwt.NewWithClaims(jwt.SigningMethodHS256, claims)
	return token.SignedString([]byte(s.jwtSecret))
}

var (
	ErrInvalidCredentials = errors.New("invalid credentials")
	ErrUserNotFound      = errors.New("user not found")
	ErrUserExists        = errors.New("user already exists")
)
