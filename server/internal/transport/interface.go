package transport

import (
	"net/http"

	httptransport "github.com/elebirds/panoptes/internal/transport/http"

	"github.com/elebirds/panoptes/internal/auth"
)

// Transport defines the interface for external connections
type Transport interface {
	Handler() http.Handler
}

// NewHTTPTransport creates a new HTTP transport
func NewHTTPTransport(authSvc *auth.Service, jwtSecret string, dbPinger httptransport.Pinger, redisPinger httptransport.Pinger) Transport {
	return httptransport.NewServer(authSvc, jwtSecret, dbPinger, redisPinger)
}
