package main

import (
	"encoding/json"
	"image/color"
	"io/ioutil"
	"os"
)

type Settings struct {
	OutputFolder     string      `json:"outputFolder"`
	FontPath         string      `json:"fontPath"`
	FontSize         float64     `json:"fontSize"`
	TextColor        color.Color `json:"textColor"`
	RandomBase       bool        `json:"randomBase"`
	ProcessAllImages bool        `json:"processAllImages"`
	Affirmations     []string    `json:"affirmations"`
}

func loadSettings(path string) (*Settings, error) {
	data, err := ioutil.ReadFile(path)
	if err != nil {
		if os.IsNotExist(err) {
			return &Settings{TextColor: color.White}, nil // Return default settings with white color
		}
		return nil, err
	}

	var settings Settings
	if err := json.Unmarshal(data, &settings); err != nil {
		return nil, err
	}

	return &settings, nil
}

func saveSettings(path string, settings *Settings) error {
	data, err := json.MarshalIndent(settings, "", "  ")
	if err != nil {
		return err
	}

	return ioutil.WriteFile(path, data, 0644)
}
