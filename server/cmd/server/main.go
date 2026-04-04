package main

import (
	"encoding/json"
	"log"
	"net/http"

	"panoptes-server/internal/config"
)

func main() {
	cfg, err := config.Load()
	if err != nil {
		log.Fatalf("load config error: %v", err)
	}

	port := cfg.Port

	mux := http.NewServeMux()
	mux.HandleFunc("/health", healthHandler)

	log.Printf("panoptes server listening on :%s", port)
	if err := http.ListenAndServe(":"+port, mux); err != nil {
		log.Fatalf("server error: %v", err)
	}
}

func healthHandler(w http.ResponseWriter, r *http.Request) {
	w.Header().Set("Content-Type", "application/json")
	_ = json.NewEncoder(w).Encode(map[string]string{"status": "ok"})
}
