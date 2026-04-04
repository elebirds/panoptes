package auth

import "time"

// User represents a user in the system
type User struct {
	ID           string
	Username     string
	PasswordHash string
	CreatedAt    time.Time
	UpdatedAt    time.Time
}
