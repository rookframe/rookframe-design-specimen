extends Control
const SAMPLE = preload("res://examples/laboratory/sample.gd")
const DEVICES = [Vector2i(1920, 1080), Vector2i(1024, 768), Vector2i(844, 390)]
var options := {"recipe": 0, "long_content": false, "locale": "en", "reduced": false, "extra_rows": 0, "bright": false}
var sample: Control
var preview: Window
var _stage: Control
var _device := 0
var _speed := 1.0
var _paused := false
var _pause_button: CheckButton
var _resource: Label

func _ready() -> void:
	var margin := MarginContainer.new()
	add_child(margin)
	margin.set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
	for edge in ["left", "right", "top", "bottom"]: margin.add_theme_constant_override("margin_" + edge, 12)
	var stack := VBoxContainer.new()
	margin.add_child(stack)
	var title := Label.new()
	title.text = "Silkbound Ledger · Native specimen"
	title.add_theme_font_size_override("font_size", 30)
	stack.add_child(title)
	var toolbar := HFlowContainer.new()
	stack.add_child(toolbar)
	_choice(toolbar, ["Action leaves", "HUD category leaf", "Explanation", "Ledger reading"], "Recipe", func(i: int) -> void: options.recipe = i; refresh())
	_choice(toolbar, ["Desktop · 1920 × 1080", "Tablet · 1024 × 768", "Phone · 844 × 390"], "Native canvas size", func(i: int) -> void: _device = i)
	_button(toolbar, "Open native size", open_native)
	_button(toolbar, "Controls", func() -> void: _catalogue("legacy_catalogue"))
	_button(toolbar, "Shell experiments", func() -> void: _catalogue("visual_catalogue"))
	var settings := HFlowContainer.new()
	stack.add_child(settings)
	_toggle(settings, "Long text", func(value: bool) -> void: options.long_content = value; refresh())
	_toggle(settings, "Reduced motion", func(value: bool) -> void: options.reduced = value; refresh())
	_toggle(settings, "Bright", func(value: bool) -> void: options.bright = value; refresh())
	_choice(settings, ["English", "Русский", "日本語"], "Sample language", func(i: int) -> void: options.locale = ["en", "ru", "ja"][i]; refresh())
	_choice(settings, ["Default rows", "+3 rows", "+12 rows"], "Content growth", func(i: int) -> void: options.extra_rows = [0, 3, 12][i]; refresh())
	_button(settings, "Replay", func() -> void: _resume(); sample.replay())
	_pause_button = _toggle(settings, "Pause", func(value: bool) -> void: _paused = value; Engine.time_scale = 0.0 if value else _speed)
	_choice(settings, ["1× motion", "½× motion", "¼× motion"], "Playback speed", func(i: int) -> void: _speed = [1.0, 0.5, 0.25][i]; Engine.time_scale = 0.0 if _paused else _speed)
	_resource = Label.new()
	_resource.autowrap_mode = TextServer.AUTOWRAP_WORD_SMART
	_resource.add_theme_font_size_override("font_size", 16)
	stack.add_child(_resource)
	_stage = Control.new()
	_stage.size_flags_vertical = Control.SIZE_EXPAND_FILL
	_stage.custom_minimum_size.y = 240
	stack.add_child(_stage)
	refresh()

func _button(parent: Node, copy: String, action: Callable) -> Button:
	var button := Button.new()
	button.text = copy
	button.custom_minimum_size.y = 44
	button.pressed.connect(action)
	parent.add_child(button)
	return button

func _choice(parent: Node, items: Array, accessible: String, action: Callable) -> void:
	var choice := OptionButton.new()
	choice.accessibility_name = accessible
	choice.tooltip_text = accessible
	choice.custom_minimum_size.y = 44
	for item in items: choice.add_item(item)
	choice.item_selected.connect(action)
	parent.add_child(choice)

func _toggle(parent: Node, copy: String, action: Callable) -> CheckButton:
	var button := CheckButton.new()
	button.text = copy
	button.custom_minimum_size.y = 44
	button.toggled.connect(action)
	parent.add_child(button)
	return button

func make_sample() -> Control:
	var instance := Control.new()
	instance.set_script(SAMPLE)
	for key in options: instance.set(key, options[key])
	return instance

func refresh() -> void:
	_resume()
	if is_instance_valid(sample):
		_stage.remove_child(sample)
		sample.queue_free()
	sample = make_sample()
	_stage.add_child(sample)
	sample.set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
	_resource.text = "res://rookframe/ui/" + ["components/surfaces/action_panel/action_panel.tscn", "components/surfaces/hinged_leaf/hinged_leaf.tscn", "components/surfaces/action_panel/action_panel.tscn", "theme/silkbound_theme.tres"][options.recipe]

func _resume() -> void:
	_paused = false
	if is_instance_valid(_pause_button): _pause_button.set_pressed_no_signal(false)
	Engine.time_scale = _speed

func open_native() -> void:
	_resume()
	if is_instance_valid(preview): preview.queue_free()
	preview = Window.new()
	preview.title = "Silkbound Ledger · %d × %d" % [DEVICES[_device].x, DEVICES[_device].y]
	preview.size = DEVICES[_device]
	preview.unresizable = true
	preview.close_requested.connect(preview.queue_free)
	add_child(preview)
	var native := make_sample()
	preview.add_child(native)
	native.set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
	preview.show()

func _catalogue(file: String) -> void:
	var window := Window.new()
	window.title = "Silkbound controls" if file == "legacy_catalogue" else "Auxiliary shell themes · selection API proposed"
	window.size = Vector2i(1440, 900)
	window.close_requested.connect(window.queue_free)
	add_child(window)
	var catalogue := load("res://examples/catalogue/%s.tscn" % file).instantiate() as Control
	window.add_child(catalogue)
	if file == "legacy_catalogue": catalogue.return_requested.connect(window.queue_free)
	window.show()

func _exit_tree() -> void:
	Engine.time_scale = 1.0
