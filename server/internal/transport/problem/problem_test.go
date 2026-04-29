package problem

import (
	"errors"
	"testing"
)

func TestFromErrorMapsCodeLikeSentinelErrors(t *testing.T) {
	cases := []struct {
		name string
		err  error
		code string
	}{
		{name: "room full", err: errors.New("room_full"), code: "room_full"},
		{name: "not host", err: errors.New("not_host"), code: "unauthorized"},
		{name: "phase mismatch", err: errors.New("phase_mismatch"), code: "phase_mismatch"},
		{name: "auth user not found", err: errors.New("user not found"), code: "user_not_found"},
	}

	for _, tc := range cases {
		t.Run(tc.name, func(t *testing.T) {
			problem := FromError(tc.err)
			if problem.GetCode() != tc.code {
				t.Fatalf("code = %q, want %q", problem.GetCode(), tc.code)
			}
		})
	}
}

func TestFromErrorPreservesTransportProblem(t *testing.T) {
	problem := FromError(New("invalid_directive", "bad directive"))
	if problem.GetCode() != "invalid_directive" {
		t.Fatalf("code = %q, want invalid_directive", problem.GetCode())
	}
}
