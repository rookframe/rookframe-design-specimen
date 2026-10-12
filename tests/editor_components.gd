extends SceneTree
## Native-input integration and rendered evidence for the editor foundation.
const Catalogue = preload("res://examples/catalogue/visual_catalogue.tscn")
var failures: Array[String] = []
var capture_dir := ""
var presses := 0


func _initialize() -> void:
	call_deferred("verify")


func check(condition: bool, message: String) -> void:
	if not condition:
		failures.append(message)
		push_error(message)


func settle() -> void:
	for frame in range(8):
		await process_frame


func key(code: Key) -> void:
	var event := InputEventKey.new()
	event.keycode = code
	event.pressed = true
	root.push_input(event)
	event = InputEventKey.new()
	event.keycode = code
	root.push_input(event)
	await settle()


func click(button: Button) -> void:
	var event := InputEventMouseButton.new()
	event.button_index = MOUSE_BUTTON_LEFT
	event.position = button.get_global_rect().get_center()
	event.pressed = true
	root.push_input(event)
	event = event.duplicate()
	event.pressed = false
	root.push_input(event)
	await settle()


func capture(name: String) -> void:
	if capture_dir.is_empty() or DisplayServer.get_name() == "headless":
		return
	await create_timer(0.1).timeout
	RenderingServer.force_draw(false)
	check(root.get_texture().get_image().save_png(capture_dir.path_join(name + ".png")) == OK, "Capture failed: " + name)


func verify() -> void:
	for argument in OS.get_cmdline_user_args():
		if argument.begins_with("--capture-dir="):
			capture_dir = argument.trim_prefix("--capture-dir=")
	if not capture_dir.is_empty():
		DirAccess.make_dir_recursive_absolute(capture_dir)
		root.unfocusable = true
		root.mouse_passthrough = true
	root.size = Vector2i(1680, 1240)
	root.content_scale_size = root.size
	var screen := Catalogue.instantiate()
	root.add_child(screen)
	screen.select_page(3)
	await settle()
	var editor: Control = screen.get_node("%Editor")
	var rail: PanelContainer = editor.get_node("%RightRail")
	var build: Button = rail.get_node("Tools/Build")
	var layers: Button = rail.get_node("Tools/Layers")
	var action: Button = editor.get_node("%AddObject")
	var disabled: Button = action.get_parent().get_node("Delete")
	disabled.pressed.connect(func() -> void: presses += 1)
	# The same instances retain native selection while theme/density round-trip.
	for theme_index in [1, 0, 1]:
		screen.select_theme(theme_index)
		for compact in [false, true, false]:
			screen.set_compact(compact)
			await settle()
			var height := 52 if compact else 64
			check(build.size.is_equal_approx(Vector2(height, height)), "Tool hit target must be square at the chosen density")
			check(action.size.y >= height, "Action target is too short")
			check(build.button_pressed, "Selection was lost during theme/density change")
			check(build.get_theme_stylebox("normal") == screen.theme.get_stylebox("normal", build.theme_type_variation), "Button retained stale theme overrides")
			check(rail.get_theme_stylebox("panel") == screen.theme.get_stylebox("panel", rail.theme_type_variation), "Rail did not inherit current material")
			check(rail.mouse_filter == Control.MOUSE_FILTER_STOP, "Rail gaps must not pass clicks to the world")
			for kind in ["tool", "action", "standard"]:
				var variant: String = "editor_button_" + kind + ("__compact" if compact else "")
				for state in ["normal", "hover", "pressed", "hover_pressed", "disabled", "focus"]:
					check(screen.theme.has_stylebox(state, variant), "Missing button state: " + variant + "/" + state)
				if theme_index == 1:
					var frame: StyleBox = screen.theme.get_stylebox("normal", variant)
					check(frame.get_script() == preload("res://rookframe/ui/_internal/menu/choice_frame.gd"), "Fantasy must reuse the new material frame")
					var expected_texture: Texture2D = preload("res://rookframe/ui/menu/materials/walnut_brass_cap.tres") if kind == "action" else preload("res://rookframe/ui/menu/materials/walnut_plain_cap.tres")
					check(frame.texture == expected_texture, "Only primary actions should use the ornate brass cap")
					check((frame.finial != null) == (kind == "action"), "Non-primary buttons retained decorative end fittings")
					check(screen.theme.get_stylebox("focus", variant) is StyleBoxEmpty, "Fantasy focus must light the frame without a flat overlay")
			for cell in editor.get_node("%States").get_children():
				var preview: Button = cell.get_node("Preview/Viewport/Button")
				var expected: int = {"Normal": BaseButton.DRAW_NORMAL, "Hovered": BaseButton.DRAW_HOVER, "Pressed": BaseButton.DRAW_PRESSED, "Selected": BaseButton.DRAW_PRESSED, "Disabled": BaseButton.DRAW_DISABLED, "Focused": BaseButton.DRAW_NORMAL}[str(cell.name)]
				check(preview.get_draw_mode() == expected, "Preview is not in its native state: " + str(cell.name))
				check(preview.has_focus() == (cell.name == &"Focused"), "Preview focus mismatch: " + str(cell.name))
			await capture(("fantasy" if theme_index == 1 else "cyberpunk") + ("-compact" if compact else "-regular"))
	# Real pointer input drives ButtonGroup and the live catalogue feedback.
	await click(layers)
	check(layers.button_pressed and not build.button_pressed, "Tool group failed exclusive selection: layers=%s build=%s same_group=%s" % [layers.button_pressed, build.button_pressed, layers.button_group == build.button_group])
	check(editor.get_node("%ToolStatus").text == "Layers active", "Tool activation did not update preview")
	check(screen.get_node("%Feedback").text == "Layers selected.", "Catalogue feedback lost the tool's accessible name")
	layers.grab_focus()
	await key(KEY_ENTER)
	check(layers.button_pressed, "Activating a selected exclusive tool must retain selection")
	await key(KEY_TAB)
	check(rail.get_node("Tools/Objects").has_focus(), "Tab did not follow source-order tools")
	action.grab_focus()
	await key(KEY_SPACE)
	check(editor.get_node("%LogTitle").text == "Object placed · 1", "Keyboard action did not update the activity surface")
	await click(disabled)
	check(presses == 0, "Disabled action emitted pressed")
	# Reflow + RTL keep rails, surfaces and their contents within page bounds.
	for width in [800, 1440]:
		root.size = Vector2i(width, 1000)
		root.content_scale_size = root.size
		screen.size = root.size
		for rtl in [false, true]:
			editor.layout_direction = Control.LAYOUT_DIRECTION_RTL if rtl else Control.LAYOUT_DIRECTION_LTR
			await settle()
			check(editor.get_node("%Workspace").vertical == (width == 800), "Editor layout did not reflow")
			for control: Control in [rail, editor.get_node("%LeftRail"), editor.get_node("%Inspector"), action]:
				check(control.get_global_rect().position.x >= editor.global_position.x - 1, "Editor content overflows start edge")
				check(control.get_global_rect().end.x <= editor.get_global_rect().end.x + 1, "Editor content overflows end edge")
			await capture("fantasy-%d%s" % [width, "-rtl" if rtl else ""])
	screen.queue_free()
	await process_frame
	print("EDITOR_COMPONENTS ", "PASS" if failures.is_empty() else failures)
	quit(0 if failures.is_empty() else 1)
