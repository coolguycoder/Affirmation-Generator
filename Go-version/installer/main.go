
package main

import (
	"log"
	"os"
	"runtime"

	"github.com/lxn/walk"
	"github.com/lxn/walk/declarative"
)

// Installer application struct
type Installer struct {
	mainWindow      *walk.MainWindow
	installPath     *walk.LineEdit
	progressBar     *walk.ProgressBar
	statusLabel     *walk.Label
	chkCreateShortcut *walk.CheckBox
}

func main() {
	installer := new(Installer)

	if _, err := (declarative.MainWindow{
		AssignTo: &installer.mainWindow,
		Title:    "Affirmation Generator Installer",
		MinSize:  declarative.Size{Width: 640, Height: 320},
		Layout:   declarative.VBox{},
		Children: []declarative.Widget{
			declarative.Label{Text: "Installs the application and creates a desktop shortcut."},
			declarative.Composite{
				Layout: declarative.HBox{},
				Children: []declarative.Widget{
					declarative.Label{Text: "Install folder:"},
					declarative.LineEdit{AssignTo: &installer.installPath},
					declarative.PushButton{Text: "Browse...", OnClicked: installer.browseForInstallPath},
				},
			},
			declarative.CheckBox{AssignTo: &installer.chkCreateShortcut, Text: "Create desktop shortcut", Checked: true},
			declarative.ProgressBar{AssignTo: &installer.progressBar},
			declarative.Label{AssignTo: &installer.statusLabel, Text: "Status: Ready"},
			declarative.PushButton{
				Text: "Install",
				OnClicked: installer.install,
			},
		},
	}.Run()); err != nil {
		log.Fatal(err)
	}
}

func (i *Installer) browseForInstallPath() {
	dlg := new(walk.FileDialog)
	dlg.Title = "Select Installation Folder"
	if ok, err := dlg.ShowBrowseFolder(i.mainWindow); err != nil {
		log.Println("Error showing folder dialog:", err)
	} else if !ok {
		return
	}
	i.installPath.SetText(dlg.Path)
}

func (i *Installer) install() {
	go func() {
		// Determine the download URL
		url := ""
		switch runtime.GOARCH {
		case "amd64":
			url = "https://github.com/coolguycoder/Affirmation-Generator/releases/latest/download/app-x64.zip"
		case "arm64":
			url = "https://github.com/coolguycoder/Affirmation-Generator/releases/latest/download/app-arm64.zip"
		default:
			i.statusLabel.SetText("Error: Unsupported architecture")
			return
		}

		// Download the zip file
		i.statusLabel.SetText("Downloading...")
		tempFile := "C:\\Users\\Marcus\\AppData\\Local\\Temp\\app.zip"
		if err := DownloadFile(url, tempFile); err != nil {
			i.statusLabel.SetText("Error downloading file: " + err.Error())
			return
		}

		// Extract the zip file
		i.statusLabel.SetText("Extracting...")
		installPath := i.installPath.Text()
		if err := Unzip(tempFile, installPath); err != nil {
			i.statusLabel.SetText("Error extracting file: " + err.Error())
			return
		}

		// Clean up the temporary file
		os.Remove(tempFile)

		// Create a shortcut
		if i.chkCreateShortcut.Checked() {
			i.statusLabel.SetText("Creating shortcut...")
			exePath := installPath + "\\app.exe"
			shortcutPath := os.Getenv("USERPROFILE") + "\\Desktop\\Affirmation Generator.lnk"
			if err := createShortcut(exePath, shortcutPath); err != nil {
				i.statusLabel.SetText("Error creating shortcut: " + err.Error())
				return
			}
		}

		i.statusLabel.SetText("Installation complete!")
	}()
}
