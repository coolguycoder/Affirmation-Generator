//go:build !windows
// +build !windows

package main

import (
	"flag"
	"fmt"
	"image"
	"image/color"
	"image/draw"
	"image/png"
	"log"
	"math/rand"
	"os"
	"path/filepath"
	"strings"
	"time"

	"golang.org/x/image/font"
	"golang.org/x/image/font/basicfont"
	"golang.org/x/image/math/fixed"

	"github.com/golang/freetype"
	"github.com/golang/freetype/truetype"
)

// App holds runtime state for non-GUI test runner
type App struct {
	settings     *Settings
	settingsPath string
	chosenColor  color.RGBA
}

// persist helpers
// settings helpers are defined in settings.go

// affirmations file I/O
func loadAffirmations(path string) ([]string, error) {
	b, err := os.ReadFile(path)
	if err != nil {
		return nil, err
	}
	lines := strings.Split(string(b), "\n")
	var out []string
	for _, l := range lines {
		if t := strings.TrimSpace(l); t != "" {
			out = append(out, t)
		}
	}
	return out, nil
}

func saveAffirmations(path string, items []string) error {
	return os.WriteFile(path, []byte(strings.Join(items, "\n")), 0644)
}

// font
func loadFont(fontPath string) (*truetype.Font, error) {
	b, err := os.ReadFile(fontPath)
	if err != nil {
		return nil, err
	}
	f, err := freetype.ParseFont(b)
	if err != nil {
		return nil, err
	}
	return f, nil
}

// render centered single-line text onto base image
func renderAffirmationToImage(base image.Image, text string, fontPath string, fontSize float64, col color.RGBA) (image.Image, error) {
	out := image.NewRGBA(base.Bounds())
	draw.Draw(out, out.Bounds(), base, image.Point{}, draw.Src)

	var face font.Face
	if fontPath != "" {
		if f, err := loadFont(fontPath); err == nil {
			face = truetype.NewFace(f, &truetype.Options{Size: fontSize})
		} else {
			face = basicfont.Face7x13
		}
	} else {
		face = basicfont.Face7x13
	}

	d := &font.Drawer{Dst: out, Src: image.NewUniform(col), Face: face}
	b := out.Bounds()
	x := b.Min.X + b.Dx()/2
	y := b.Min.Y + b.Dy()/2
	w := d.MeasureString(text).Round()
	d.Dot = fixed.Point26_6{X: fixed.I(x - w/2), Y: fixed.I(y)}
	d.DrawString(text)
	return out, nil
}

// resolve base images from file or directory
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

// App methods
func main() {
	// Provide a minimal CLI to test core features without GUI dependencies.
	var (
		bases      = flag.String("bases", "", "Base image file or directory")
		outDir     = flag.String("out", "out", "Output directory for generated images")
		font       = flag.String("font", "", "Path to TTF font (optional)")
		size       = flag.Float64("size", 48, "Font size")
		sample     = flag.String("sample", "I am worthy.", "Sample affirmation text")
		processAll = flag.Bool("all", false, "Process all base images (if directory)")
	)
	flag.Parse()

	// Prepare app state
	app := &App{settingsPath: "affirmation_settings.json", chosenColor: color.RGBA{255, 255, 255, 255}}

	// resolve bases
	list := resolveBaseImages(*bases)
	if len(list) == 0 {
		log.Fatalf("no base images found in %q", *bases)
	}

	if err := os.MkdirAll(*outDir, 0o755); err != nil {
		log.Fatalf("failed create out dir: %v", err)
	}

	rand.Seed(time.Now().UnixNano())

	if *processAll {
		for i, b := range list {
			if err := generateToFile(b, *sample, *font, *size, app.chosenColor, *outDir, fmt.Sprintf("sample_%d.png", i)); err != nil {
				log.Println("generate error:", err)
			}
		}
	} else {
		// pick random base
		b := list[rand.Intn(len(list))]
		if err := generateToFile(b, *sample, *font, *size, app.chosenColor, *outDir, "sample.png"); err != nil {
			log.Fatalf("generate error: %v", err)
		}
	}

	fmt.Println("Done. Outputs in:", *outDir)
}

func generateToFile(basePath, text, fontPath string, fontSize float64, col color.RGBA, outDir, fname string) error {
	f, err := os.Open(basePath)
	if err != nil {
		return err
	}
	defer f.Close()
	img, _, err := image.Decode(f)
	if err != nil {
		return err
	}
	outImg, err := renderAffirmationToImage(img, text, fontPath, fontSize, col)
	if err != nil {
		return err
	}
	outPath := filepath.Join(outDir, fname)
	of, err := os.Create(outPath)
	if err != nil {
		return err
	}
	defer of.Close()
	return png.Encode(of, outImg)
}
