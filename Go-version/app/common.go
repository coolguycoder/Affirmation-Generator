package main

import (
	"image/color"
	"os"
	"path/filepath"
	"strings"
)

// resolveBaseImages returns a list of image files for a given path (file or directory)
func resolveBaseImages(path string) []string {
	var list []string
	if path == "" {
		return list
	}
	st, err := os.Stat(path)
	if err != nil {
		return list
	}
	if !st.IsDir() {
		return []string{path}
	}
	ents, err := os.ReadDir(path)
	if err != nil {
		return list
	}
	ext := map[string]bool{".png": true, ".jpg": true, ".jpeg": true, ".bmp": true}
	for _, e := range ents {
		if e.IsDir() {
			continue
		}
		en := strings.ToLower(filepath.Ext(e.Name()))
		if ext[en] {
			list = append(list, filepath.Join(path, e.Name()))
		}
	}
	return list
}

// generateToFile loads basePath, renders the text using renderAffirmation, and saves a file
func generateToFile(basePath, text, fontPath string, fontSize float64, col color.RGBA, outDir, fname string) error {
	img, err := loadImage(basePath)
	if err != nil {
		return err
	}
	opts := RenderOptions{
		FontPath:     fontPath,
		FontSize:     fontSize,
		TextColor:    col,
		OutlineColor: color.Black,
		X:            20,
		Y:            20,
	}
	out, err := renderAffirmation(img, text, opts)
	if err != nil {
		return err
	}
	return saveImage(filepath.Join(outDir, fname), out)
}
