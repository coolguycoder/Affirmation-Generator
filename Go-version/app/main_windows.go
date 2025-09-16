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
	var lbBases *walk.ListBox
	var lbAff *walk.ListBox
	var leNewAff *walk.LineEdit
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
					if lbBases != nil {
						lbBases.SetModel(NewStringListModel(basesSlice))
					}
				}},
				declarative.PushButton{Text: "Preview Selected", OnClicked: func() {
					// read selection from listbox
					idx := -1
					if lbAff != nil { idx = lbAff.CurrentIndex() }
					if idx < 0 { walk.MsgBox(mw, "No selection", "No affirmation selected", walk.MsgBoxIconWarning); return }
					text := affSlice[idx]
					// choose base
					bidx := -1
					if lbBases != nil { bidx = lbBases.CurrentIndex() }
					if bidx < 0 {
						if len(basesSlice) == 0 { basesSlice = resolveBaseImages(leBase.Text()) }
						if len(basesSlice) == 0 { walk.MsgBox(mw, "No base", "No base images", walk.MsgBoxIconWarning); return }
						bidx = 0
					}
					base := basesSlice[bidx]
					img, err := loadImage(base)
					if err != nil { walk.MsgBox(mw, "Error", fmt.Sprintf("load base: %v", err), walk.MsgBoxIconError); return }
					out, err := renderAffirmation(img, text, RenderOptions{FontPath: leFont.Text(), FontSize: float64(nbSize.Value()), TextColor: appSettings.TextColor, OutlineColor: color.Black, X: 20, Y: 20})
					if err != nil { walk.MsgBox(mw, "Error", fmt.Sprintf("render: %v", err), walk.MsgBoxIconError); return }
					bmp, err := walk.NewBitmapFromImage(out)
					if err == nil && imgView != nil { imgView.SetImage(bmp) }
				}},
				declarative.PushButton{Text: "Generate", OnClicked: func() {
					outDir := leOut.Text()
					if err := os.MkdirAll(outDir, 0o755); err != nil { walk.MsgBox(mw, "Error", fmt.Sprintf("out: %v", err), walk.MsgBoxIconError); return }
					if len(affSlice) == 0 { walk.MsgBox(mw, "No affirmations", "Add at least one affirmation", walk.MsgBoxIconWarning); return }
					if len(basesSlice) == 0 { basesSlice = resolveBaseImages(leBase.Text()) }
					if len(basesSlice) == 0 { walk.MsgBox(mw, "No base", "No base images", walk.MsgBoxIconWarning); return }
					for i, a := range affSlice {
						b := basesSlice[rand.Intn(len(basesSlice))]
						if err := generateToFile(b, a, leFont.Text(), float64(nbSize.Value()), color.RGBA{255, 255, 255, 255}, outDir, fmt.Sprintf("affirmation_%d.png", i)); err != nil {
							log.Println("gen err:", err)
						}
					}
				}},
			}},
				declarative.Composite{Layout: declarative.VBox{}, Children: []declarative.Widget{
					declarative.Composite{Layout: declarative.HBox{}, Children: []declarative.Widget{
						declarative.LineEdit{AssignTo: &leNewAff, MinSize: declarative.Size{Width: 220}, OnKeyDown: func(key walk.Key) { if key == walk.KeyReturn { /* handled in button */ } }},
						declarative.PushButton{Text: "Add", OnClicked: func() {
							txt := strings.TrimSpace(leNewAff.Text())
							if txt == "" { return }
							affSlice = append(affSlice, txt)
							if lbAff != nil {
								lbAff.SetModel(NewStringListModel(affSlice))
							}
							leNewAff.SetText("")
						}},
						declarative.PushButton{Text: "Remove Selected", OnClicked: func() {
							idx := -1
							if lbAff != nil { idx = lbAff.CurrentIndex() }
							if idx >= 0 && idx < len(affSlice) {
								affSlice = append(affSlice[:idx], affSlice[idx+1:]...)
								if lbAff != nil { lbAff.SetModel(NewStringListModel(affSlice)) }
							}
						}},
					}},
					declarative.Composite{Layout: declarative.HBox{}, Children: []declarative.Widget{
						declarative.ListBox{AssignTo: &lbBases, MinSize: declarative.Size{Width: 300, Height: 140}},
						declarative.ListBox{AssignTo: &lbAff, MinSize: declarative.Size{Width: 300, Height: 140}},
						declarative.ImageView{AssignTo: &imgView, MinSize: declarative.Size{Width: 260, Height: 180}},
					}},
				}},
		},
	}.Run()); err != nil {
		log.Fatal(err)
	}
}
