extends TextureRect
## Catalogue-only viewports hold real native states without stealing page focus.
enum State { NORMAL, HOVERED, PRESSED, SELECTED, DISABLED, FOCUSED }
@export var state := State.NORMAL
var _mouse_entered := false
@onready var button: Button = $Viewport/Button


func _ready() -> void:
	resized.connect(_layout)
	visibility_changed.connect(_layout)
	_layout()


func configure(active_theme: Theme, compact: bool) -> void:
	button.theme = active_theme
	button.set_compact(compact)
	_layout.call_deferred()


func _layout() -> void:
	$Viewport.size = Vector2i(size.max(Vector2.ONE))
	button.size = button.get_combined_minimum_size()
	button.position = (size - button.size) / 2
	button.release_focus()
	if not is_visible_in_tree():
		return
	button.disabled = state == State.DISABLED
	button.toggle_mode = state in [State.PRESSED, State.SELECTED]
	button.set_pressed_no_signal(state in [State.PRESSED, State.SELECTED])
	if not _mouse_entered:
		$Viewport.notify_mouse_entered()
		_mouse_entered = true
	var motion := InputEventMouseMotion.new()
	motion.position = button.get_rect().get_center() if state == State.HOVERED else Vector2(-20, -20)
	$Viewport.push_input(motion, true)
	button.tooltip_text = ""
	if state == State.FOCUSED:
		button.grab_focus()
