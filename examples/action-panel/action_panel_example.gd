extends Control
## F6 this scene: the first section is authored; additional sections use the public slots.
const SECTION = preload("res://rookframe/ui/components/surfaces/action_panel/action_section.tscn")
const LINE_HEIGHT = preload("res://rookframe/ui/theme/silkbound_line_height.gd")
@onready var panel = %Panel

func _ready() -> void:
	%AddSection.pressed.connect(_add_section)
	%RemoveSection.pressed.connect(_remove_section)
	%Compact.toggled.connect(_compact)
	%Footer.toggled.connect(func(value: bool) -> void: panel.show_footer = value)
	for label in [%Title, %Detail, %RollLabel, %Formula, %Target]:
		LINE_HEIGHT.apply(label, 1.2)

func _add_section() -> void:
	var section = SECTION.instantiate()
	var row := HBoxContainer.new()
	section.add_child(row)
	var label := Label.new()
	label.text = "Additional system control"
	label.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	row.add_child(label)
	var amount := OptionButton.new()
	for value in [0, 3, 5, 10]:
		amount.add_item(str(value))
	amount.select(1)
	row.add_child(amount)
	panel.get_sections_slot().add_child(section)
	LINE_HEIGHT.apply(label, 1.2)
	%RemoveSection.disabled = false

func _remove_section() -> void:
	var slot: VBoxContainer = panel.get_sections_slot()
	if slot.get_child_count() <= 1:
		return
	var last := slot.get_child(slot.get_child_count() - 1)
	slot.remove_child(last)
	last.queue_free()
	%RemoveSection.disabled = slot.get_child_count() <= 1

func _compact(value: bool) -> void:
	panel.compact = value
	panel.custom_minimum_size.x = 380 if value else 520
	panel.reset_size()
