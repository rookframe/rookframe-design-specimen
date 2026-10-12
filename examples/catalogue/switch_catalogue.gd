extends VBoxContainer
## Hosts own exclusive choices; each tray adapts to its number of cards.
signal feedback(message: String)


func _ready() -> void:
	_bind_group($Group, $Selection)
	_bind_group($AudienceGroup, $AudienceSelection)


func _bind_group(tray: Control, status: Label) -> void:
	var group := ButtonGroup.new()
	for card: Button in tray.get_options().get_children():
		card.button_group = group
		card.toggled.connect(func(selected: bool) -> void:
			if selected:
				status.text = card.title + " selected."
				feedback.emit(status.text)
		)
