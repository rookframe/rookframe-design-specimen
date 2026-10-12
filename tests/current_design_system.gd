extends SceneTree
## Standalone smoke check: native themes, recipes, live switching and layout.
const Catalogue = preload("res://examples/catalogue/visual_catalogue.tscn")
var failures: Array[String] = []


func _initialize() -> void:
	call_deferred("verify")


func check(condition: bool, message: String) -> void:
	if not condition:
		failures.append(message)
		push_error(message)


func settle() -> void:
	for frame in range(4):
		await process_frame
	# Focused previews render sequentially because native focus is Window-wide.
	var screen := root.get_node_or_null("VisualCatalogue")
	var deadline := Time.get_ticks_msec() + 3000
	while screen != null and (screen._capturing_focus or screen._focus_preview_pending) and Time.get_ticks_msec() < deadline:
		await process_frame


func verify() -> void:
	var screen := Catalogue.instantiate()
	root.add_child(screen)
	await settle()
	var menu: Control = screen.get_node("%Menu")
	check(menu.get_children().map(func(section: Node) -> String: return section.name) == ["Surfaces", "Buttons", "Switch"], "Menu sections must place Switch immediately after Buttons")
	check(menu.get_node("Switch").scene_file_path == "res://examples/catalogue/showcase_section.tscn", "Switch must use the shared showcase section")
	check(screen.get_node("%Switches").get_parent() == menu.get_node("Switch/Inset/Content"), "Switch samples must live inside their section frame")
	check(screen.get_node("%Pages").get_child_count() == screen.get_node("%PageChoice").item_count, "Page selector does not match the showcase pages")
	var primary: Button = screen.get_node("%PrimaryStates").get_node("Normal/Viewport/Layout/Buttons/Button")
	var surface: Panel = screen.get_node("%SurfaceSamples").get_node("Record")
	var choice: OptionButton = screen.get_node("%Settings").get_node("Language/Choice")
	# Round-trip on the same instances catches stale texture/override state.
	for theme_index in [0, 1, 0]:
		screen.select_page(0)
		screen.select_theme(theme_index)
		for compact in [false, true, false]:
			screen.set_compact(compact)
			await settle()
			_check_button_states(screen)
			var variant: StringName = &"menu_button_primary__compact" if compact else &"menu_button_primary"
			check(primary.theme_type_variation == variant, "Button density did not update")
			check(primary.get_node("Surface").visible == (theme_index == 1), "Theme retained the other button material")
			check(primary.get_node("Surface").texture == screen.theme.get_icon("cap", variant), "Button did not inherit theme artwork")
			check(primary.get_theme_font("font") == screen.theme.get_font("font", variant), "Button font does not match the theme")
			var destructive: Button = screen.get_node("%ConfirmationActions/LeaveWorld")
			var secondary: Button = screen.get_node("%ConfirmationActions/Cancel")
			var destructive_variant := &"components_button_destructive__compact" if compact else &"components_button_destructive"
			check(destructive.theme_type_variation == destructive_variant, "Destructive usage lost its density")
			check(destructive.size.y == secondary.size.y, "Destructive and cancel buttons must have equal heights")
			check(destructive.get_theme_color("font_color") != secondary.get_theme_color("font_color"), "Destructive ink lost its semantic treatment")
			check(destructive.get_node("Surface").visible == (theme_index == 1), "Destructive button retained the other theme's material")
			check(destructive.get_node("Surface").texture == screen.theme.get_icon("cap", destructive_variant), "Destructive texture did not refresh")
			if theme_index == 1:
				check(destructive.get_node("Surface").texture != secondary.get_node("Surface").texture, "Destructive button lost its oxblood material")
			check(surface.get_node("Grain").texture == screen.theme.get_icon("grain", &"menu_world_record"), "Surface did not refresh its theme")
			check(choice.get_popup().theme_type_variation == (&"settings_popup__compact" if compact else &"settings_popup"), "Popup lost its theme or density")
			for usage in [&"menu_button_primary", &"menu_button_standard", &"components_button_destructive", &"settings_choice", &"settings_action_primary", &"settings_action_secondary", &"settings_back", &"settings_navigation"]:
				for state in [&"normal", &"hover", &"pressed", &"hover_pressed", &"disabled", &"focus"]:
					check(screen.theme.has_stylebox(state, usage), "Missing %s/%s" % [usage, state])
		for page in range(screen.get_node("%Pages").get_child_count()):
			screen.select_page(page)
			await settle()
			for candidate: Control in screen.get_node("%Pages").get_children():
				check(candidate.visible == (candidate.get_index() == page), "Catalogue page visibility is wrong")
		screen.select_page(1)
		choice.show_popup()
		await settle()
		check(choice.get_popup().visible, "Native choice popup did not open")
		check(choice.get_popup().get_theme_stylebox("panel") == screen.theme.get_stylebox("panel", &"settings_popup"), "Popup panel used fallback styling")
		choice.get_popup().hide()
	# Exercise real native keyboard input through the viewport.
	screen.select_page(0)
	await settle()
	primary.grab_focus()
	check(primary.has_focus(), "Native button could not take keyboard focus")
	var key := InputEventKey.new()
	key.keycode = KEY_ENTER
	key.pressed = true
	primary.get_viewport().push_input(key)
	key = InputEventKey.new()
	key.keycode = KEY_ENTER
	primary.get_viewport().push_input(key)
	await settle()
	check(screen.get_node("%Feedback").text == "Normal pressed.", "Native keyboard activation failed")
	primary.release_focus()
	await _check_destructive_input(screen)
	for width in [800, 1440]:
		screen.size = Vector2(width, 900)
		await settle()
		check(screen.get_node("%Menu").vertical, "Showcase sections must remain a vertical stack")
		check(screen.get_node("%SurfaceSamples").vertical == (width < 1000), "Surface samples did not reflow")
		check(screen.get_node("%ButtonKinds").vertical == (width < 1000), "Button samples did not reflow")
		check(screen.get_node("%DestructiveExamples").vertical == (width < 1000), "Destructive samples did not reflow")
		var actions: Control = screen.get_node("%ConfirmationActions")
		for action: Control in actions.get_children():
			check(actions.get_global_rect().encloses(action.get_global_rect()), "Confirmation action overflowed its container")
		check(screen.get_node("%Header").get_global_rect().end.x <= screen.get_global_rect().end.x, "Catalogue choices overflow the viewport")
		_check_button_states(screen)
	var capture_dir := ""
	for argument in OS.get_cmdline_user_args():
		if argument.begins_with("--capture-dir="):
			capture_dir = argument.trim_prefix("--capture-dir=")
	if not capture_dir.is_empty() and DisplayServer.get_name() != "headless":
		DirAccess.make_dir_recursive_absolute(capture_dir)
		for theme_index in [0, 1]:
			screen.select_theme(theme_index)
			for page in range(screen.get_node("%Pages").get_child_count()):
				screen.select_page(page)
				await settle()
				await create_timer(0.4).timeout
				RenderingServer.force_draw(false)
				var path := "%s/%s-%s.png" % [capture_dir, "cyberpunk" if theme_index == 0 else "fantasy", page]
				check(root.get_texture().get_image().save_png(path) == OK, "Could not save " + path)
	screen.queue_free()
	await process_frame
	print("CURRENT_DESIGN_SYSTEM ", "PASS" if failures.is_empty() else failures)
	quit(0 if failures.is_empty() else 1)


func _check_button_states(screen: Control) -> void:
	var samples := screen.get_node("%PrimaryStates").get_children() + screen.get_node("%SecondaryStates").get_children() + screen.get_node("%DestructiveStates").get_children()
	for sample in samples:
		var button: Button = sample.get_node("Viewport/Layout/Buttons/Button")
		check(button.text == ("Hovered + pressed" if sample.name == &"HoverPressed" else str(sample.name)), "Button label does not describe its state")
		var expected: int = {
			"Normal": BaseButton.DRAW_NORMAL, "Hovered": BaseButton.DRAW_HOVER,
			"Pressed": BaseButton.DRAW_PRESSED, "Disabled": BaseButton.DRAW_DISABLED,
			"HoverPressed": BaseButton.DRAW_HOVER_PRESSED,
			"Focused": BaseButton.DRAW_NORMAL,
		}[str(sample.name)]
		check(button.get_draw_mode() == expected, "Wrong native draw state: " + str(sample.name))
		if sample.name == &"Focused":
			check(sample.focus_preview_ready, "Native focus preview was not captured")
		else:
			check(not button.has_focus(), "Unexpected native focus: " + str(sample.name))
		# The fully extended ornament and drop shadow must fit inside the preview.
		var travel := button.get_theme_constant("ornament_travel")
		var rect := button.get_global_rect()
		check(rect.position.x >= travel + 8 and sample.size.x - rect.end.x >= travel + 8, "Ornament can overflow: %s at %s in %s; travel=%s" % [sample.name, rect, sample.size, travel])
		check(rect.position.y >= 12 and sample.size.y - rect.end.y >= button.get_theme_constant("shadow_drop") + 4, "Depth can overflow the surface")


func _check_destructive_input(screen: Control) -> void:
	var cancel: Button = screen.get_node("%ConfirmationActions/Cancel")
	var destructive: Button = screen.get_node("%ConfirmationActions/LeaveWorld")
	var activations := [0]
	destructive.pressed.connect(func() -> void: activations[0] += 1)
	cancel.grab_focus()
	var tab := InputEventKey.new()
	tab.keycode = KEY_TAB
	tab.pressed = true
	root.push_input(tab)
	await settle()
	check(destructive.has_focus(), "Tab did not move from Cancel to the destructive action")
	for pressed in [true, false]:
		var key := InputEventKey.new()
		key.keycode = KEY_ENTER
		key.pressed = pressed
		root.push_input(key)
	await settle()
	check(activations[0] == 1, "Destructive action did not emit one native keyboard activation")
	destructive.disabled = true
	await settle()
	for pressed in [true, false]:
		var key := InputEventKey.new()
		key.keycode = KEY_ENTER
		key.pressed = pressed
		root.push_input(key)
		var click := InputEventMouseButton.new()
		click.button_index = MOUSE_BUTTON_LEFT
		click.position = destructive.get_global_rect().get_center()
		click.pressed = pressed
		root.push_input(click)
	await settle()
	check(activations[0] == 1, "Disabled destructive action accepted input")
	destructive.disabled = false
	destructive.release_focus()
