package geometry

import (
	"testing"

	"github.com/elebirds/panoptes/internal/domain"
)

func TestOffsetAxialOddRConversionsRoundTrip(t *testing.T) {
	tests := []struct {
		name string
		col  int
		row  int
		want domain.Position
	}{
		{name: "origin even row", col: 0, row: 0, want: domain.Position{Q: 0, R: 0}},
		{name: "odd row keeps leading column", col: 0, row: 1, want: domain.Position{Q: 0, R: 1}},
		{name: "even row shifts left", col: 2, row: 2, want: domain.Position{Q: 1, R: 2}},
		{name: "default map spawn", col: 2, row: 10, want: domain.Position{Q: -3, R: 10}},
		{name: "negative axial from wide row", col: 1, row: 5, want: domain.Position{Q: -1, R: 5}},
	}

	for _, tt := range tests {
		t.Run(tt.name, func(t *testing.T) {
			got := OffsetToAxial(tt.col, tt.row)
			if got != tt.want {
				t.Fatalf("OffsetToAxial(%d,%d) = %#v, want %#v", tt.col, tt.row, got, tt.want)
			}

			col, row := AxialToOffset(got)
			if col != tt.col || row != tt.row {
				t.Fatalf("AxialToOffset(%#v) = (%d,%d), want (%d,%d)", got, col, row, tt.col, tt.row)
			}
		})
	}
}

func TestAxialDistance(t *testing.T) {
	tests := []struct {
		name string
		a    domain.Position
		b    domain.Position
		want int
	}{
		{name: "same", a: domain.Position{Q: 0, R: 0}, b: domain.Position{Q: 0, R: 0}, want: 0},
		{name: "single neighbor", a: domain.Position{Q: 0, R: 0}, b: domain.Position{Q: 1, R: -1}, want: 1},
		{name: "diagonal axial line", a: domain.Position{Q: 0, R: 0}, b: domain.Position{Q: 2, R: -2}, want: 2},
		{name: "mixed axes", a: domain.Position{Q: -3, R: 10}, b: domain.Position{Q: 12, R: 10}, want: 15},
		{name: "cube z dominates", a: domain.Position{Q: 3, R: 4}, b: domain.Position{Q: -1, R: 8}, want: 4},
	}

	for _, tt := range tests {
		t.Run(tt.name, func(t *testing.T) {
			if got := AxialDistance(tt.a, tt.b); got != tt.want {
				t.Fatalf("AxialDistance(%#v,%#v) = %d, want %d", tt.a, tt.b, got, tt.want)
			}
			if got := AxialDistance(tt.b, tt.a); got != tt.want {
				t.Fatalf("AxialDistance symmetry = %d, want %d", got, tt.want)
			}
		})
	}
}
