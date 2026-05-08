package staticdata

type EmoteSeriesDefinition struct {
	ID          string `json:"id"`
	DisplayName string `json:"display_name"`
	IconKey     string `json:"icon_key"`
	SortOrder   int    `json:"sort_order"`
}

type EmoteDefinition struct {
	ID          string   `json:"id"`
	SeriesID    string   `json:"series_id"`
	DisplayName string   `json:"display_name"`
	AssetKey    string   `json:"asset_key"`
	SortOrder   int      `json:"sort_order"`
	Tags        []string `json:"tags,omitempty"`
}

type EmoteCatalogFile struct {
	Series []EmoteSeriesDefinition `json:"series"`
	Emotes []EmoteDefinition       `json:"emotes"`
}
