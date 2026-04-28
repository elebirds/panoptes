.PHONY: data-gen data-validate gen proto-gen server lint db-migrate-up db-migrate-down db-reset db-sqlc

PROTO_GEN_PATHS = \
	--path panoptes/proto/v1/common.proto \
	--path panoptes/proto/v1/config.proto \
	--path panoptes/proto/v1/data_types.proto \
	--path panoptes/proto/v1/data_catalog.proto \
	--path panoptes/proto/v1/map_catalog.proto \
	--path panoptes/proto/v1/auth.proto \
	--path panoptes/proto/v1/chat.proto \
	--path panoptes/proto/v1/lobby.proto \
	--path panoptes/proto/v1/game_state.proto \
	--path panoptes/proto/v1/minister.proto \
	--path panoptes/proto/v1/orders.proto \
	--path panoptes/proto/v1/transport.proto \
	--path panoptes/proto/v1/turn.proto \
	--path panoptes/proto/v1/settlement.proto

data-gen:
	cd server && go run ./cmd/datagen

data-validate:
	cd server && go run ./cmd/datagen -validate

# Generate code for both server (Go) and client (C#)
gen: data-gen proto-gen db-sqlc

proto-gen:
	cd protocol && buf generate $(PROTO_GEN_PATHS)
	cd server && go run ./cmd/transportdispatchgen

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
