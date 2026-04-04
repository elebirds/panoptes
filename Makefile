.PHONY: gen server lint

# Generate protocol code for both server (Go) and client (C#)
gen:
	cd protocol && buf generate
	cp -r protocol/gen/csharp/* client/Assets/Generated/Protocol/

# Run the Go backend
server:
	cd server && go run ./cmd/server

# Lint & vet
lint:
	cd server && go vet ./...
	cd protocol && buf lint
