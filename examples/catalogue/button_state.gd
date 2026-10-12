extends TextureRect
## Catalogue-only native states, with focused appearances captured in sequence.
signal focus_preview_requested
enum State { NORMAL, HOVERED, PRESSED, DISABLED, FOCUSED, HOVER_PRESSED }
const LABELS := ["Normal", "Hovered", "Pressed", "Disabled", "Focused", "Hovered + pressed"]
@export var state := State.NORMAL
@export var usage: StringName = &"menu.button.standard"
var _mouse_entered := false
var focus_preview_ready := false
@onready var button: Button = $Viewport/Layout/Buttons/Button


func _ready() -> void:
	button.usage = usage
	button.text = LABELS[state]
	resized.connect(_resize_viewport)
	visibility_changed.connect(_apply_state)
	button.resized.connect(_apply_state, CONNECT_DEFERRED)
	_resize_viewport()
	_apply_state.call_deferred()


func configure(active_theme: Theme, compact: bool) -> void:
	button.theme = active_theme
	button.set_compact(compact)
	$Viewport/Layout.theme = active_theme
	$Viewport/Layout/Buttons.theme_type_variation = &"MarginContainer" if usage == &"menu.button.primary" else &"menu_buttons__compact" if compact else &"menu_buttons"
	# Equal sample rows keep all button states aligned for comparison.
	var primary_variant := &"menu_button_primary__compact" if compact else &"menu_button_primary"
	var height := active_theme.get_constant("minimum_height", primary_variant)
	custom_minimum_size = Vector2(button.get_combined_minimum_size().x + 80, height + 36)
	_apply_state.call_deferred()


func _apply_state() -> void:
	button.release_focus()
	if not is_visible_in_tree():
		return
	button.disabled = state == State.DISABLED
	button.toggle_mode = state in [State.PRESSED, State.HOVER_PRESSED]
	button.set_pressed_no_signal(button.toggle_mode)
	if state == State.FOCUSED:
		focus_preview_ready = false
		focus_preview_requested.emit()
	elif state in [State.HOVERED, State.HOVER_PRESSED]:
		if not _mouse_entered:
			$Viewport.notify_mouse_entered()
			_mouse_entered = true
		var motion := InputEventMouseMotion.new()
		motion.position = button.get_global_rect().get_center()
		$Viewport.push_input(motion, true)


func capture_focus() -> void:
	# Native focus is shared across a Window, even between SubViewports.
	# The catalogue captures each focused appearance in turn, then releases input.
	button.grab_focus()
	if DisplayServer.get_name() != "headless":
		$Viewport.render_target_update_mode = SubViewport.UPDATE_ALWAYS
		await get_tree().create_timer(button.get_theme_constant("ornament_ms") / 1000.0 + 0.05).timeout
		if is_visible_in_tree() and button.has_focus():
			RenderingServer.force_draw(false)
			texture = ImageTexture.create_from_image($Viewport.get_texture().get_image())
		$Viewport.render_target_update_mode = SubViewport.UPDATE_DISABLED
	focus_preview_ready = button.has_focus()
	button.release_focus()


func _resize_viewport() -> void:
	$Viewport.size = Vector2i(size.max(Vector2.ONE))
	if state == State.FOCUSED and is_visible_in_tree():
		focus_preview_ready = false
		focus_preview_requested.emit()
