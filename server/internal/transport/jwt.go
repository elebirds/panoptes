package transport

import (
	"errors"
	"strings"

	"github.com/golang-jwt/jwt/v5"
)

var ErrUnauthorized = errors.New("unauthorized")

// ParseBearerToken extracts a bearer token from Authorization header.
func ParseBearerToken(header string) (string, error) {
	token, ok := strings.CutPrefix(header, "Bearer ")
	if !ok || token == "" {
		return "", ErrUnauthorized
	}
	return token, nil
}

// ParseAndValidateJWT validates JWT token and returns player_id claim.
func ParseAndValidateJWT(jwtSecret string, tokenString string) (string, error) {
	if tokenString == "" {
		return "", ErrUnauthorized
	}

	token, err := jwt.Parse(tokenString, func(token *jwt.Token) (interface{}, error) {
		return []byte(jwtSecret), nil
	})
	if err != nil || !token.Valid {
		return "", ErrUnauthorized
	}

	claims, ok := token.Claims.(jwt.MapClaims)
	if !ok {
		return "", ErrUnauthorized
	}

	playerID, ok := claims["player_id"].(string)
	if !ok || playerID == "" {
		return "", ErrUnauthorized
	}

	return playerID, nil
}
