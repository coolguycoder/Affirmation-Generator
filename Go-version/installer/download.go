
package main

import (
	"fmt"
	"io"
	"net/http"
	"os"
)

// WriteCounter counts the number of bytes written to it.
type WriteCounter struct {
	Total uint64
}

func (wc *WriteCounter) Write(p []byte) (int, error) {
	n := len(p)
	wc.Total += uint64(n)
	wc.printProgress()
	return n, nil
}

func (wc WriteCounter) printProgress() {
	// Clear the line by using a carriage return to move the cursor back to the start and erasing the line
	fmt.Printf("\rDownloading... %d bytes complete", wc.Total)
}

// DownloadFile downloads a file from a URL to a local path.
func DownloadFile(url string, dest string) error {
	// Create the file
	out, err := os.Create(dest)
	if err != nil {
		return err
	}
	defer out.Close()

	// Get the data

resp, err := http.Get(url)
	if err != nil {
		return err
	}
	defer resp.Body.Close()

	// Create our progress reporter and pass it to be used alongside our writer
	counter := &WriteCounter{}
	_, err = io.Copy(out, io.TeeReader(resp.Body, counter))
	if err != nil {
		return err
	}

	fmt.Println()

	return nil
}
