package main

import "github.com/lxn/walk"

// StringListModel is a walk ListModel backed by a []string. It implements
// the events required by walk.ListModel so it can be used as a model for ListBox.
type StringListModel struct {
	items           []string
	onItemChanged   *walk.IntEvent
	onItemsReset    *walk.Event
	onItemsInserted *walk.IntEvent
	onItemsRemoved  *walk.IntEvent
}

func NewStringListModel(items []string) *StringListModel { return &StringListModel{items: items} }

func (m *StringListModel) ItemCount() int { return len(m.items) }

func (m *StringListModel) Value(index int) interface{} { return m.items[index] }

func (m *StringListModel) Items() []string { return m.items }

func (m *StringListModel) SetItems(items []string) {
	// notify reset
	m.items = items
}

func (m *StringListModel) ItemChanged() *walk.IntEvent {
	if m.onItemChanged == nil {
		m.onItemChanged = new(walk.IntEvent)
	}
	return m.onItemChanged
}

func (m *StringListModel) ItemsReset() *walk.Event {
	if m.onItemsReset == nil {
		m.onItemsReset = new(walk.Event)
	}
	return m.onItemsReset
}

func (m *StringListModel) ItemsInserted() *walk.IntEvent {
	if m.onItemsInserted == nil {
		m.onItemsInserted = new(walk.IntEvent)
	}
	return m.onItemsInserted
}

func (m *StringListModel) ItemsRemoved() *walk.IntEvent {
	if m.onItemsRemoved == nil {
		m.onItemsRemoved = new(walk.IntEvent)
	}
	return m.onItemsRemoved
}
