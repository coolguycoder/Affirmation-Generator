
package main

import (
	"fmt"
	"image"
	"image/color"
	"image/draw"
	"image/jpeg"
	"image/png"
	"io"
	"math/rand"
	"os"
	"path/filepath"
	"strings"
	"time"

	_ "embed"

	"github.com/golang/freetype"
	"github.com/golang/freetype/truetype"
	"golang.org/x/image/font"
	"golang.org/x/image/math/fixed"
)

//go:embed LilitaOne-Regular.ttf
var fontData []byte

// Options for rendering text on an image
type RenderOptions struct {
	FontPath   string
	FontSize   float64
	TextColor  color.Color
	OutlineColor color.Color
	X          int
	Y          int
	Width      int
	Height     int
}

// loadImage loads an image from the specified file path.
func loadImage(filePath string) (image.Image, error) {
	file, err := os.Open(filePath)
	if err != nil {
		return nil, err
	}
	defer file.Close()

	img, _, err := image.Decode(file)
	if err != nil {
		return nil, err
	}
	return img, nil
}

// saveImage saves the given image to the specified file path.
func saveImage(filePath string, img image.Image) error {
	outFile, err := os.Create(filePath)
	if err != nil {
		return err
	}
	defer outFile.Close()

	switch strings.ToLower(filepath.Ext(filePath)) {
	case ".png":
		return png.Encode(outFile, img)
	case ".jpg", ".jpeg":
		return jpeg.Encode(outFile, img, &jpeg.Options{Quality: 90})
	default:
		return fmt.Errorf("unsupported image format: %s", filepath.Ext(filePath))
	}
}

// getRandomImage gets a random image file path from a directory.
func getRandomImage(directoryPath string) (string, error) {
	files, err := os.ReadDir(directoryPath)
	if err != nil {
		return "", err
	}

	var imageFiles []string
	extensions := map[string]bool{".png": true, ".jpg": true, ".jpeg": true, ".bmp": true}
	for _, file := range files {
		if !file.IsDir() {
			ext := filepath.Ext(file.Name())
			if extensions[strings.ToLower(ext)] {
				imageFiles = append(imageFiles, filepath.Join(directoryPath, file.Name()))
			}
		}
	}

	if len(imageFiles) == 0 {
		return "", fmt.Errorf("no image files found in %s", directoryPath)
	}

	rand.Seed(time.Now().UnixNano())
	randomIndex := rand.Intn(len(imageFiles))
	return imageFiles[randomIndex], nil
}

// renderAffirmation renders the given text onto the source image with options.
func renderAffirmation(src image.Image, text string, opts RenderOptions) (image.Image, error) {
	// Load font
	var parsedFont *truetype.Font
	var err error

	if opts.FontPath != "" {
		fontBytes, err := os.ReadFile(opts.FontPath)
		if err != nil {
			return nil, fmt.Errorf("failed to read font file: %w", err)
		}
		parsedFont, err = truetype.Parse(fontBytes)
		if err != nil {
			return nil, fmt.Errorf("failed to parse font: %w", err)
		}
	} else {
		parsedFont, err = truetype.Parse(fontData)
		if err != nil {
			return nil, fmt.Errorf("failed to parse embedded font: %w", err)
		}
	}

	dst := image.NewRGBA(src.Bounds())
	draw.Draw(dst, dst.Bounds(), src, image.Point{}, draw.Src)

	c := freetype.NewContext()
	c.SetDPI(72)
	c.SetFont(parsedFont)
	c.SetFontSize(opts.FontSize)
	c.SetClip(dst.Bounds())
	c.SetDst(dst)

	// Calculate text position for centering and wrapping
	imgWidth := src.Bounds().Dx()
	imgHeight := src.Bounds().Dy()

	// Use a bounding box for text rendering
	textRect := image.Rect(opts.X, opts.Y, imgWidth-opts.X, imgHeight-opts.Y)

	// Create a drawer for measuring text
	drawer := &font.Drawer{
		Face:    truetype.NewFace(parsedFont, &truetype.Options{Size: opts.FontSize, DPI: 72}),
		Src:     image.Transparent,
		Dst:     image.Transparent,
		Dot:     fixed.Point{},
	}

	// Split text into lines for wrapping
	var lines []string
	words := strings.Fields(text)
	currentLine := ""
	for _, word := range words {
		if currentLine == "" {
			currentLine = word
		} else {
			testLine := currentLine + " " + word
			if drawer.MeasureString(testLine).Ceil() <= textRect.Dx() {
				currentLine = testLine
			} else {
				lines = append(lines, currentLine)
				currentLine = word
			}
		}
	}
	if currentLine != "" {
		lines = append(lines, currentLine)
	}

	lineHeight := int(opts.FontSize * 1.2) // Approximate line height
	totalTextHeight := len(lines) * lineHeight
	startY := opts.Y + (textRect.Dy()-totalTextHeight)/2

	for i, line := range lines {
		lineWidth := drawer.MeasureString(line).Ceil()
		startX := opts.X + (textRect.Dx()-lineWidth)/2
		yPos := startY + i*lineHeight

		// Draw outline
		c.SetSrc(image.NewUniform(opts.OutlineColor))
		for ox := -2; ox <= 2; ox++ {
			for oy := -2; oy <= 2; oy++ {
				if ox == 0 && oy == 0 {
					continue
				}
				pt := freetype.Pt(startX+ox, yPos+int(c.PointToFixed(opts.FontSize)>>6))
				_, err = c.DrawString(line, pt)
				if err != nil {
					return nil, err
				}
			}
		}

		// Draw main text
		c.SetSrc(image.NewUniform(opts.TextColor))
		pt := freetype.Pt(startX, yPos+int(c.PointToFixed(opts.FontSize)>>6))
		_, err = c.DrawString(line, pt)
		if err != nil {
			return nil, err
		}
	}

	return dst, nil
}
