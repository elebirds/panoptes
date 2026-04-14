.PHONY: data-gen data-validate gen proto-gen server lint db-migrate-up db-migrate-down db-reset db-sqlc

PROTO_GEN_PATHS = \
	--path common.proto \
	--path data_types.proto \
	--path data_catalog.proto \
	--path map_catalog.proto \
	--path auth.proto \
	--path lobby.proto \
	--path game_state.proto \
	--path minister.proto \
	--path orders.proto \
	--path turn.proto \
	--path settlement.proto

data-gen:
	cd server && go run ./cmd/datagen

data-validate:
	cd server && go run ./cmd/datagen -validate

# Generate code for both server (Go) and client (C#)
gen: data-gen proto-gen db-sqlc

proto-gen:
	cd protocol && buf generate $(PROTO_GEN_PATHS)

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
