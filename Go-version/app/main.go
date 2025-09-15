package main

import (
	"image"
	"image/color"
	"image/draw"
	"log"

	"github.com/lxn/walk"
	"github.com/lxn/walk/declarative"
)

// Main application struct
type App struct {
	mainWindow *walk.MainWindow

	// Wizard steps
	steps       []*walk.Composite
	stepIndex   int
	btnBack     *walk.PushButton
	btnNext     *walk.PushButton

	// Step 1: Setup
	basePathBox     *walk.LineEdit
	outputFolderBox *walk.LineEdit
	fontPathBox     *walk.LineEdit
	fontSizeUp      *walk.NumberEdit
	colorPreview    *walk.Composite
	baseImagesList  *walk.ListBox
	baseCountLabel  *walk.Label
	chkRandomBase   *walk.CheckBox
	chkProcessAllImages *walk.CheckBox
	chosenColor     color.Color // To store the chosen color

	// Step 2: Affirmations
	lstAffirmations   *walk.ListBox
	txtNewAffirmation *walk.LineEdit
	btnAddAff         *walk.PushButton
	btnRemoveAff      *walk.PushButton
	btnLoadList       *walk.PushButton
	btnSaveList       *walk.PushButton

	// Step 3: Preview & Generate
	previewBox *walk.ImageView
	btnPreview *walk.PushButton
	btnGenerate *walk.PushButton

	settingsPath string
	settings     *Settings
}

func (app *App) browseForFontFile() {
	dlg := new(walk.FileDialog)
	dlg.Title = "Select Font File"
	dlg.Filter = "Font Files (*.ttf;*.otf)|*.ttf;*.otf|All Files (*.*)|*.*"

	if ok, err := dlg.ShowOpen(app.mainWindow); err != nil {
		log.Println("Error showing file dialog:", err)
	} else if ok {
		app.fontPathBox.SetText(dlg.FilePath)
	}
}

func (app *App) browseForOutputFolder() {
	dlg := new(walk.FileDialog)
	dlg.Title = "Select Output Folder"
	if ok, err := dlg.ShowBrowseFolder(app.mainWindow); err != nil {
		log.Println("Error showing folder dialog:", err)
	} else if ok {
		app.outputFolderBox.SetText(dlg.FilePath)
	}
}

func (app *App) browseForBasePath() {
	dlg := new(walk.FileDialog)
	dlg.Title = "Select base image (or cancel to pick folder)"
	dlg.Filter = "Images (*.png;*.jpg;*.jpeg;*.bmp)|*.png;*.jpg;*.jpeg;*.bmp|All files (*.*)|*.*"

	if ok, err := dlg.ShowOpen(app.mainWindow); err != nil {
		log.Println("Error showing file dialog:", err)
	} else if ok {
		app.basePathBox.SetText(dlg.FilePath)
		// TODO: Populate base images list
	} else {
		// User cancelled file dialog, try folder dialog
		dlg.Title = "Select base image folder"
		if ok, err := dlg.ShowBrowseFolder(app.mainWindow); err != nil {
			log.Println("Error showing folder dialog:", err)
		} else if ok {
			app.basePathBox.SetText(dlg.FilePath)
			// TODO: Populate base images list
		}
	}
}

func (app *App) updateStep() {
	for i, step := range app.steps {
		step.SetVisible(i == app.stepIndex)
	}
	app.btnBack.SetEnabled(app.stepIndex > 0)
	app.btnNext.SetEnabled(app.stepIndex < len(app.steps)-1)
}

func (app *App) generateImage() {
	// Create a dummy base image
	baseImg := image.NewRGBA(image.Rect(0, 0, 800, 600))
	draw.Draw(baseImg, baseImg.Bounds(), image.Black, image.Point{}, draw.Src)

	// Get the font size
	fontSize := app.fontSizeUp.Value()

	// Render the affirmation
	img, err := renderAffirmation(baseImg, "Hello, Affirmations!", 50, 300, color.White, fontSize)
	if err != nil {
		log.Println("Error generating image:", err)
		return
	}

	// Set the image to the preview box
	bitmap, err := walk.NewBitmapFromImage(img)
	if err != nil {
		log.Println("Error creating bitmap:", err)
		return
	}
	app.previewBox.SetImage(bitmap)
}

func (app *App) browseForColor() {
	cd := new(walk.ColorDialog)
	cd.Color = app.chosenColor
	if ok, err := cd.ShowCustom(app.mainWindow); err != nil {
		log.Println("Error showing color dialog:", err)
	} else if ok {
		app.chosenColor = cd.Color
		app.updateColorPreview()
	}
}

func (app *App) updateColorPreview() {
	// Create a 1x1 image of the chosen color
	img := image.NewRGBA(image.Rect(0, 0, 1, 1))
	draw.Draw(img, img.Bounds(), &image.Uniform{C: app.chosenColor}, image.Point{}, draw.Src)

	// Convert to Walk.Bitmap and set to colorPreview
	bitmap, err := walk.NewBitmapFromImage(img)
	if err != nil {
		log.Println("Error creating color preview bitmap:", err)
		return
	}
	app.colorPreview.SetBackground(bitmap)
}

func (app *App) populateBaseList() {
	app.baseImagesList.SetItems([]string{}) // Clear existing items
	path := app.basePathBox.Text()
	if path == "" {
		app.baseCountLabel.SetText("No images selected")
		return
	}

	images := app.resolveBaseImages(path)
	app.baseImagesList.SetItems(images)
	app.baseCountLabel.SetText(fmt.Sprintf("%d image(s)", len(images)))

	// auto-select first so preview uses it when available
	if len(images) > 0 && app.baseImagesList.SelectedIndex() < 0 {
		app.baseImagesList.SetSelectedIndex(0)
	}
}

func (app *App) resolveBaseImages(path string) []string {
	var list []string
	if path == "" {
		return list
	}

	info, err := os.Stat(path)
	if err != nil {
		log.Println("Error getting file info:", err)
		return list
	}

	if !info.IsDir() {
		// It's a file
		list = append(list, path)
		return list
	}

	// It's a directory
	expandedPath, err := filepath.Abs(path)
	if err != nil {
		log.Println("Error getting absolute path:", err)
		return list
	}

	files, err := os.ReadDir(expandedPath)
	if err != nil {
		log.Println("Error reading directory:", err)
		return list
	}

	extensions := map[string]bool{".png": true, ".jpg": true, ".jpeg": true, ".bmp": true}
	for _, file := range files {
		if !file.IsDir() {
			ext := filepath.Ext(file.Name())
			if extensions[strings.ToLower(ext)] {
				list = append(list, filepath.Join(expandedPath, file.Name()))
			}
		}
	}
	return list
}

func (app *App) showPromptDialog(title, message, defaultValue string) (string, bool) {
	var inputLE *walk.LineEdit
	var dlg *walk.Dialog
	var acceptPB *walk.PushButton

	if _, err := declarative.NewBuilder(declarative.Dialog{
		AssignTo:      &dlg,
		Title:         title,
		DefaultButton: &acceptPB,
		MinSize:       declarative.Size{Width: 300, Height: 150},
		Layout:        declarative.VBox{Margins: declarative.Margins{10, 10, 10, 10}},
		Children: []declarative.Widget{
			declarative.Label{Text: message},
			declarative.LineEdit{AssignTo: &inputLE, Text: defaultValue},
			declarative.Composite{
				Layout: declarative.HBox{MarginsZero: true},
				Children: []declarative.Widget{
					declarative.HSpacer{},
					declarative.PushButton{AssignTo: &acceptPB, Text: "OK", OnClicked: func() { dlg.Accept() }},
					declarative.PushButton{Text: "Cancel", OnClicked: func() { dlg.Cancel() }},
				},
			},
		},
	}).Create(app.mainWindow); err != nil {
		log.Printf("Error creating prompt dialog: %v\n", err)
		return "", false
	}

	if dlg.Run() == walk.DlgOK {
		return inputLE.Text(), true
	}
	return "", false
}

func (app *App) addAffirmation() {
	t := app.txtNewAffirmation.Text()
	if t == "" {
		return
	}
	app.lstAffirmations.Items().Add(t)
	app.txtNewAffirmation.SetText("")
}

func (app *App) removeSelectedAffirmation() {
	idx := app.lstAffirmations.SelectedIndex()
	if idx >= 0 {
		app.lstAffirmations.Items().RemoveAt(idx)
	}
}

func (app *App) loadAffirmationsFromFile() {
	dlg := new(walk.FileDialog)
	dlg.Title = "Load Affirmations from Text File"
	dlg.Filter = "Text Files (*.txt)|*.txt|All Files (*.*)|*.*"

	if ok, err := dlg.ShowOpen(app.mainWindow); err != nil {
		log.Println("Error showing file dialog:", err)
	} else if ok {
		content, err := os.ReadFile(dlg.FilePath)
		if err != nil {
			walk.MsgBox(app.mainWindow, "Error", fmt.Sprintf("Failed to read file: %v", err), walk.MsgBoxIconError)
			return
		}
		lines := strings.Split(string(content), "\n")
		var affirmations []string
		for _, line := range lines {
			trimmedLine := strings.TrimSpace(line)
			if trimmedLine != "" {
				affirmations = append(affirmations, trimmedLine)
			}
		}
		app.lstAffirmations.SetItems(affirmations)
	}
}

func (app *App) saveAffirmationsToFile() {
	dlg := new(walk.FileDialog)
	dlg.Title = "Save Affirmations to Text File"
	dlg.Filter = "Text Files (*.txt)|*.txt|All Files (*.*)|*.*"

	if ok, err := dlg.ShowSave(app.mainWindow); err != nil {
		log.Println("Error showing save dialog:", err)
	} else if ok {
		items := app.lstAffirmations.Items().([]string)
		content := strings.Join(items, "\n")
		err := os.WriteFile(dlg.FilePath, []byte(content), 0644)
		if err != nil {
			walk.MsgBox(app.mainWindow, "Error", fmt.Sprintf("Failed to save file: %v", err), walk.MsgBoxIconError)
			return
		}
	}
}

func (app *App) editSelectedAffirmation() {
	idx := app.lstAffirmations.SelectedIndex()
	if idx >= 0 {
		currentAffirmation := app.lstAffirmations.Items().At(idx).(string)
		if result, ok := app.showPromptDialog("Edit Affirmation", "Edit affirmation:", currentAffirmation); ok {
			app.lstAffirmations.Items().SetAt(idx, result)
		}
	}
}

func (app *App) saveSettings() {
	app.settings.OutputFolder = app.outputFolderBox.Text()
	app.settings.FontPath = app.fontPathBox.Text()
	app.settings.FontSize = app.fontSizeUp.Value()
	app.settings.TextColor = app.chosenColor
	app.settings.RandomBase = app.chkRandomBase.Checked()
	// TODO: Save processAllImages
	app.settings.Affirmations = app.lstAffirmations.Items().([]string)


	if err := saveSettings(app.settingsPath, app.settings); err != nil {
		log.Println("Error saving settings:", err)
	}
}

func main() {
	app := &App{
		steps:        make([]*walk.Composite, 4),
		settingsPath: "affirmation_settings.json", // Default settings file name
		chosenColor:  color.White,                 // Default color
	}

	// Load settings
	s, err := loadSettings(app.settingsPath)
	if err != nil {
		log.Println("Error loading settings:", err)
		app.settings = &Settings{} // Use default settings on error
	} else {
		app.settings = s
	}

	if _, err := (declarative.MainWindow{
		AssignTo: &app.mainWindow,
		Title:    "Affirmation Image Maker",
		MinSize:  declarative.Size{Width: 1100, Height: 760},
		Layout:   declarative.VBox{},
		Children: []declarative.Widget{
			// Header
			declarative.Composite{
				Layout: declarative.HBox{},
				Children: []declarative.Widget{
					declarative.Label{Text: "Affirmation Image Maker"},
				},
			},
			// Steps
			declarative.Composite{
				AssignTo: &app.steps[0],
				Layout:   declarative.VBox{},
				Visible:  true, // Start with the first step visible
				Children: []declarative.Widget{
					declarative.Label{Text: "Base image or folder:"},
					declarative.LineEdit{AssignTo: &app.basePathBox, OnKeyDown: func(key walk.Key) { if key == walk.KeyReturn { app.populateBaseList() } }},
					declarative.PushButton{Text: "Browse", OnClicked: app.browseForBasePath},
					declarative.Label{Text: "Output folder:"},
					declarative.LineEdit{AssignTo: &app.outputFolderBox},
					declarative.PushButton{Text: "Browse", OnClicked: app.browseForOutputFolder},
					declarative.Label{Text: "Font file:"},
					declarative.LineEdit{AssignTo: &app.fontPathBox},
					declarative.PushButton{Text: "Browse", OnClicked: app.browseForFontFile},
					declarative.Label{Text: "Font size:"},
					declarative.NumberEdit{AssignTo: &app.fontSizeUp, Min: 10, Max: 120},
					declarative.Label{Text: "Text color:"},
					declarative.Composite{AssignTo: &app.colorPreview, MinSize: declarative.Size{Width: 40, Height: 28}, Background: declarative.SolidColorBrush{Color: declarative.RGBA(app.chosenColor)}},
					declarative.PushButton{Text: "Choose Color", OnClicked: app.browseForColor},
					declarative.ListBox{AssignTo: &app.baseImagesList, MinSize: declarative.Size{Width: 320, Height: 120}},
					declarative.Label{AssignTo: &app.baseCountLabel, Text: "No images selected"},
					declarative.CheckBox{AssignTo: &app.chkRandomBase, Text: "Random Base Image", Checked: app.settings.RandomBase},
					declarative.CheckBox{AssignTo: &app.chkProcessAllImages, Text: "Process All Images (for each affirmation)"},
				},
			},
			declarative.Composite{
				AssignTo: &app.steps[1],
				Layout:   declarative.VBox{},
				Visible:  false,
				Children: []declarative.Widget{
					declarative.Label{Text: "Affirmations"},
					declarative.ListBox{AssignTo: &app.lstAffirmations, MinSize: declarative.Size{Width: 500, Height: 300}, OnItemActivated: app.editSelectedAffirmation},
					declarative.LineEdit{AssignTo: &app.txtNewAffirmation, OnKeyDown: func(key walk.Key) { if key == walk.KeyReturn { app.addAffirmation() } }},
					declarative.Composite{
						Layout: declarative.HBox{MarginsZero: true},
						Children: []declarative.Widget{
							declarative.PushButton{AssignTo: &app.btnAddAff, Text: "Add", OnClicked: app.addAffirmation},
							declarative.PushButton{AssignTo: &app.btnRemoveAff, Text: "Remove Selected", OnClicked: app.removeSelectedAffirmation},
							declarative.PushButton{AssignTo: &app.btnLoadList, Text: "Load .txt", OnClicked: app.loadAffirmationsFromFile},
							declarative.PushButton{AssignTo: &app.btnSaveList, Text: "Save .txt", OnClicked: app.saveAffirmationsToFile},
						},
					},
				},
			},
			declarative.Composite{
				AssignTo: &app.steps[2],
				Layout:   declarative.VBox{},
				Visible:  false,
				Children: []declarative.Widget{
					declarative.Label{Text: "Preview & Generate"},
					declarative.ImageView{AssignTo: &app.previewBox, MinSize: declarative.Size{Width: 560, Height: 420}},
					declarative.PushButton{AssignTo: &app.btnPreview, Text: "Preview Selected", OnClicked: app.previewSelectedAffirmation},
					declarative.PushButton{
						AssignTo: &app.btnGenerate,
						Text:     "Generate",
						OnClicked: app.generateAllImages,
					},
				},
			},
			declarative.Composite{
				AssignTo: &app.steps[3],
				Layout:   declarative.VBox{},
				Visible:  false,
				Children: []declarative.Widget{
					declarative.Label{Text: "About"},
				},
			},
			// Navigation
			declarative.Composite{
				Layout: declarative.HBox{},
				Children: []declarative.Widget{
					declarative.PushButton{
						AssignTo: &app.btnBack,
						Text:     "Back",
						OnClicked: func() {
							if app.stepIndex > 0 {
								app.stepIndex--
								app.updateStep()
							}
						},
					},
					declarative.PushButton{
						AssignTo: &app.btnNext,
						Text:     "Next",
						OnClicked: func() {
							if app.stepIndex < len(app.steps)-1 {
								app.stepIndex++
								app.updateStep()
							}
						},
					},
				},
			},
		},
	}.Run()); err != nil {
		log.Fatal(err)
	}

	// Apply loaded settings to UI controls after the window is created
	if app.settings != nil {
		app.basePathBox.SetText(app.settings.OutputFolder) // Assuming OutputFolder is used for basePathBox initially
		app.outputFolderBox.SetText(app.settings.OutputFolder)
		app.fontPathBox.SetText(app.settings.FontPath)
		app.fontSizeUp.SetValue(app.settings.FontSize)
		app.chosenColor = app.settings.TextColor
		app.updateColorPreview()
		app.chkRandomBase.SetChecked(app.settings.RandomBase)
		app.chkProcessAllImages.SetChecked(app.settings.ProcessAllImages)
	}

	app.populateBaseList() // Populate base images list initially

	// Set initial affirmations if any
	if len(app.settings.Affirmations) > 0 {
		app.lstAffirmations.SetItems(app.settings.Affirmations)
	}
}

		AssignTo: &app.mainWindow,
		Title:    "Affirmation Image Maker",
		MinSize:  declarative.Size{Width: 1100, Height: 760},
		Layout:   declarative.VBox{},
		Children: []declarative.Widget{
			// Header
			declarative.Composite{
				Layout: declarative.HBox{},
				Children: []declarative.Widget{
					declarative.Label{Text: "Affirmation Image Maker"},
				},
			},
			// Steps
			declarative.Composite{
				AssignTo: &app.steps[0],
				Layout:   declarative.VBox{},
                Visible: true, // Start with the first step visible
				Children: []declarative.Widget{
					declarative.Label{Text: "Base image or folder:"},
					declarative.LineEdit{AssignTo: &app.basePathBox},
					declarative.PushButton{Text: "Browse", OnClicked: app.browseForBasePath},
					declarative.Label{Text: "Output folder:"},
					declarative.LineEdit{AssignTo: &app.outputFolderBox},
					declarative.PushButton{Text: "Browse", OnClicked: app.browseForOutputFolder},
					declarative.Label{Text: "Font file:"},
					declarative.LineEdit{AssignTo: &app.fontPathBox},
					declarative.PushButton{Text: "Browse", OnClicked: app.browseForFontFile},
					declarative.Label{Text: "Font size:"},
					declarative.NumberEdit{AssignTo: &app.fontSizeUp},
					declarative.Label{Text: "Text color:"},
					declarative.Composite{AssignTo: &app.colorPreview, MinSize: declarative.Size{Width: 40, Height: 28}},
				},
			},
			declarative.Composite{
				AssignTo: &app.steps[1],
				Layout:   declarative.VBox{},
				Visible:  false,
				Children: []declarative.Widget{
					declarative.Label{Text: "Affirmations"},
					declarative.ListBox{AssignTo: &app.lstAffirmations},
					declarative.LineEdit{AssignTo: &app.txtNewAffirmation},
				},
			},
			declarative.Composite{
				AssignTo: &app.steps[2],
				Layout:   declarative.VBox{},
				Visible:  false,
				Children: []declarative.Widget{
					declarative.Label{Text: "Preview & Generate"},
					declarative.ImageView{AssignTo: &app.previewBox},
					declarative.PushButton{
						Text: "Generate",
						OnClicked: app.generateImage,
					},
				},
			},
			declarative.Composite{
				AssignTo: &app.steps[3],
				Layout:   declarative.VBox{},
				Visible:  false,
				Children: []declarative.Widget{
					declarative.Label{Text: "About"},
				},
			},
			// Navigation
			declarative.Composite{
				Layout: declarative.HBox{},
				Children: []declarative.Widget{
					declarative.PushButton{
						AssignTo: &app.btnBack,
						Text:     "Back",
						OnClicked: func() {
							if app.stepIndex > 0 {
								app.stepIndex--
								app.updateStep()
							}
						},
					},
					declarative.PushButton{
						AssignTo: &app.btnNext,
						Text:     "Next",
						OnClicked: func() {
							if app.stepIndex < len(app.steps)-1 {
								app.stepIndex++
								app.updateStep()
							}
						},
					},
				},
			},
		},
	}.Run()); err != nil {
		log.Fatal(err)
	}
}
