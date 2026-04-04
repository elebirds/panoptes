.PHONY: gen server lint db-migrate-up db-migrate-down db-reset db-sqlc

# Generate code for both server (Go) and client (C#)
gen: proto-gen db-sqlc

proto-gen:
	cd protocol && buf generate

# Run the Go backend
server:
	cd server && go run ./cmd/server

# Lint & vet
lint:
	cd server && go vet ./...
	cd protocol && buf lint

# Database migrations
db-migrate-up:
	docker-compose up -d postgres
	sleep 2
	cd server && go run github.com/pressly/goose/v3/cmd/goose@latest -dir db/migrations postgres "postgres://panoptes:panoptes_dev@localhost:5432/panoptes?sslmode=disable" up

db-migrate-down:
	docker-compose up -d postgres
	sleep 2
	cd server && go run github.com/pressly/goose/v3/cmd/goose@latest -dir db/migrations postgres "postgres://panoptes:panoptes_dev@localhost:5432/panoptes?sslmode=disable" down

db-reset: db-migrate-down db-migrate-up

# Generate sqlc code
db-sqlc:
	cd server && sqlc generate -f ./db/sqlc.yaml
