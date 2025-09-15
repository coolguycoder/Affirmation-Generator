
package main

import (
	"path/filepath"

	"github.com/go-ole/go-ole"
	"github.com/go-ole/go-ole/oleutil"
)

func createShortcut(target, shortcutPath string) error {
	ole.CoInitialize(0)
	defer ole.CoUninitialize()

	oleShellObject, err := oleutil.CreateObject("WScript.Shell")
	if err != nil {
		return err
	}
	defer oleShellObject.Release()

	wshell, err := oleShellObject.QueryInterface(ole.IID_IDispatch)
	if err != nil {
		return err
	}
	defer wshell.Release()

	cs, err := oleutil.CallMethod(wshell, "CreateShortcut", shortcutPath)
	if err != nil {
		return err
	}

	dispatch := cs.ToIDispatch()
	oleutil.PutProperty(dispatch, "TargetPath", target)
	oleutil.PutProperty(dispatch, "WorkingDirectory", filepath.Dir(target))
	oleutil.CallMethod(dispatch, "Save")

	return nil
}
