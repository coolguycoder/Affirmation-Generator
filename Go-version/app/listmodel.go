package main

import "github.com/lxn/walk"

// StringListModel is a simple walk ListModel backed by a []string.
type StringListModel struct{
    items []string
    onItemChanged *walk.IntEvent
}

func NewStringListModel(items []string) *StringListModel { return &StringListModel{items: items} }

func (m *StringListModel) ItemCount() int { return len(m.items) }

func (m *StringListModel) Value(index int) interface{} { return m.items[index] }

func (m *StringListModel) Items() []string { return m.items }

func (m *StringListModel) SetItems(items []string) {
    m.items = items
}

// ItemChanged returns an event that callers can attach to.
func (m *StringListModel) ItemChanged() *walk.IntEvent {
    if m.onItemChanged == nil {
        m.onItemChanged = new(walk.IntEvent)
    }
    return m.onItemChanged
}

// (No interface assertion; this is a lightweight helper used by the GUI.)
