//go:build windows
// +build windows

package main

import (
	"fmt"
	"image/color"
	"log"
	"math/rand"
	"os"
	"strings"
	"time"

	"github.com/lxn/walk"
	"github.com/lxn/walk/declarative"
)

// This file provides a minimal Windows GUI entrypoint using walk/declarative.
// It wires a few controls to the core functions implemented elsewhere.

func main() {
	// basic app state
	// try load settings
	var appSettings *Settings
	if s, err := loadSettings("affirmation_settings.json"); err == nil {
		appSettings = s
	} else {
		appSettings = &Settings{}
	}

	rand.Seed(time.Now().UnixNano())

	var mw *walk.MainWindow
	var leBase *walk.LineEdit
	var leOut *walk.LineEdit
	var leFont *walk.LineEdit
	var nbSize *walk.NumberEdit
	var teBases *walk.TextEdit
	var teAff *walk.TextEdit
	var imgView *walk.ImageView
	// local slices to hold list content
	var basesSlice []string
	var affSlice []string

	if _, err := (declarative.MainWindow{
		AssignTo: &mw,
		Title:    "Affirmation Image Maker (Go)",
		MinSize:  declarative.Size{Width: 900, Height: 640},
		Layout:   declarative.VBox{},
		Children: []declarative.Widget{
			declarative.Composite{Layout: declarative.HBox{}, Children: []declarative.Widget{
				declarative.Label{Text: "Base image or folder:"},
				declarative.LineEdit{AssignTo: &leBase, Text: ""},
				declarative.PushButton{Text: "Browse", OnClicked: func() {
					dlg := new(walk.FileDialog)
					dlg.Title = "Select Base Image or Folder"
					if _, err := dlg.ShowOpen(mw); err == nil {
						leBase.SetText(dlg.FilePath)
					}
				}},
			}},
			declarative.Composite{Layout: declarative.HBox{}, Children: []declarative.Widget{
				declarative.Label{Text: "Output folder:"},
				declarative.LineEdit{AssignTo: &leOut, Text: "out"},
				declarative.PushButton{Text: "Browse", OnClicked: func() {
					dlg := new(walk.FileDialog)
					dlg.Title = "Select Output Folder"
					if _, err := dlg.ShowBrowseFolder(mw); err == nil {
						leOut.SetText(dlg.FilePath)
					}
				}},
			}},
			declarative.Composite{Layout: declarative.HBox{}, Children: []declarative.Widget{
				declarative.Label{Text: "Font (ttf):"},
				declarative.LineEdit{AssignTo: &leFont, Text: "LilitaOne-Regular.ttf"},
				declarative.Label{Text: "Size:"},
				declarative.NumberEdit{AssignTo: &nbSize, Value: 48},
			}},
			declarative.Composite{Layout: declarative.HBox{}, Children: []declarative.Widget{
				declarative.PushButton{Text: "Populate Bases", OnClicked: func() {
					basesSlice = resolveBaseImages(leBase.Text())
					if teBases != nil {
						teBases.SetText(strings.Join(basesSlice, "\n"))
					}
				}},
				declarative.PushButton{Text: "Preview Selected", OnClicked: func() {
					// read affirmations from text edit
					if teAff != nil {
						affSlice = strings.Split(strings.ReplaceAll(teAff.Text(), "\r\n", "\n"), "\n")
					}
					if len(affSlice) == 0 {
						walk.MsgBox(mw, "No selection", "No affirmation entered", walk.MsgBoxIconWarning)
						return
					}
					text := affSlice[0]
					// choose base
					if len(basesSlice) == 0 {
						basesSlice = resolveBaseImages(leBase.Text())
					}
					if len(basesSlice) == 0 {
						walk.MsgBox(mw, "No base", "No base images", walk.MsgBoxIconWarning)
						return
					}
					base := basesSlice[0]
					img, err := loadImage(base)
					if err != nil {
						walk.MsgBox(mw, "Error", fmt.Sprintf("load base: %v", err), walk.MsgBoxIconError)
						return
					}
					out, err := renderAffirmation(img, text, RenderOptions{FontPath: leFont.Text(), FontSize: float64(nbSize.Value()), TextColor: appSettings.TextColor, OutlineColor: color.Black, X: 20, Y: 20})
					if err != nil {
						walk.MsgBox(mw, "Error", fmt.Sprintf("render: %v", err), walk.MsgBoxIconError)
						return
					}
					bmp, err := walk.NewBitmapFromImage(out)
					if err == nil && imgView != nil {
						imgView.SetImage(bmp)
					}
				}},
				declarative.PushButton{Text: "Generate", OnClicked: func() {
					outDir := leOut.Text()
					if err := os.MkdirAll(outDir, 0o755); err != nil {
						walk.MsgBox(mw, "Error", fmt.Sprintf("out: %v", err), walk.MsgBoxIconError)
						return
					}
					if teAff != nil {
						affSlice = strings.Split(strings.ReplaceAll(teAff.Text(), "\r\n", "\n"), "\n")
					}
					if len(affSlice) == 0 {
						walk.MsgBox(mw, "No affirmations", "Add at least one affirmation", walk.MsgBoxIconWarning)
						return
					}
					if len(basesSlice) == 0 {
						basesSlice = resolveBaseImages(leBase.Text())
					}
					if len(basesSlice) == 0 {
						walk.MsgBox(mw, "No base", "No base images", walk.MsgBoxIconWarning)
						return
					}
					for i, a := range affSlice {
						b := basesSlice[rand.Intn(len(basesSlice))]
						if err := generateToFile(b, a, leFont.Text(), float64(nbSize.Value()), color.RGBA{255, 255, 255, 255}, outDir, fmt.Sprintf("affirmation_%d.png", i)); err != nil {
							log.Println("gen err:", err)
						}
					}
				}},
			}},
			declarative.Composite{Layout: declarative.HBox{}, Children: []declarative.Widget{
				declarative.TextEdit{AssignTo: &teBases, MinSize: declarative.Size{Width: 300, Height: 180}, ReadOnly: true},
				declarative.TextEdit{AssignTo: &teAff, MinSize: declarative.Size{Width: 300, Height: 180}},
				declarative.ImageView{AssignTo: &imgView, MinSize: declarative.Size{Width: 260, Height: 180}},
			}},
		},
	}.Run()); err != nil {
		log.Fatal(err)
	}
}
