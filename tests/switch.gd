extends SceneTree
## Exercise native selection and responsive copy against both new theme mappings.
const Catalogue = preload("res://examples/catalogue/visual_catalogue.tscn")
const Switch = preload("res://rookframe/ui/menu/Switch.tscn")
var failures: Array[String] = []
var capture_dir := ""
var toggles := 0
var disabled_presses := 0


func _initialize() -> void:
	verify.call_deferred()


func check(condition: bool, message: String) -> void:
	if not condition:
		failures.append(message)
		push_error(message)


func settle() -> void:
	for frame in range(8):
		await process_frame
	var screen := root.get_node_or_null("VisualCatalogue")
	var deadline := Time.get_ticks_msec() + 3000
	while screen != null and (screen._capturing_focus or screen._focus_preview_pending) and Time.get_ticks_msec() < deadline:
		await process_frame


func click(button: Button) -> void:
	root.get_node("VisualCatalogue/%Scroll").ensure_control_visible(button)
	await settle()
	var event := InputEventMouseButton.new()
	event.button_index = MOUSE_BUTTON_LEFT
	event.position = button.get_global_rect().get_center()
	event.pressed = true
	root.push_input(event)
	event = event.duplicate()
	event.pressed = false
	root.push_input(event)
	await settle()


func key(code: Key) -> void:
	var event := InputEventKey.new()
	event.keycode = code
	event.pressed = true
	root.push_input(event)
	event = event.duplicate()
	event.pressed = false
	root.push_input(event)
	await settle()


func capture(name: String) -> void:
	if capture_dir.is_empty() or DisplayServer.get_name() == "headless":
		return
	await show_switch_section()
	await create_timer(0.1).timeout
	RenderingServer.force_draw(false)
	check(root.get_texture().get_image().save_png(capture_dir.path_join(name + ".png")) == OK, "Capture failed: " + name)


func show_switch_section() -> void:
	var screen := root.get_node("VisualCatalogue")
	var scroll: ScrollContainer = screen.get_node("%Scroll")
	var section: Control = screen.get_node("%Menu/Switch")
	scroll.scroll_vertical += roundi(section.global_position.y - scroll.global_position.y)
	await settle()


func verify() -> void:
	for argument in OS.get_cmdline_user_args():
		if argument.begins_with("--capture-dir="):
			capture_dir = argument.trim_prefix("--capture-dir=")
	if not capture_dir.is_empty():
		DirAccess.make_dir_recursive_absolute(capture_dir)
		root.unfocusable = true
		root.mouse_passthrough = true
	root.size = Vector2i(1440, 1400)
	root.content_scale_size = root.size
	var screen := Catalogue.instantiate()
	root.add_child(screen)
	screen.select_page(0)
	await settle()
	await show_switch_section()
	var page: Control = screen.get_node("%Switches")
	var device: Button = page.get_node("Group/Inset/Options/Device")
	var server: Button = page.get_node("Group/Inset/Options/Server")
	var locked: Button = page.get_node("DisabledGroup/Inset/Options/Selected")
	var unavailable: Button = page.get_node("DisabledGroup/Inset/Options/Unselected")
	var audience: BoxContainer = page.get_node("AudienceGroup").get_options()
	server.toggled.connect(func(_value: bool) -> void: toggles += 1)
	locked.pressed.connect(func() -> void: disabled_presses += 1)
	unavailable.pressed.connect(func() -> void: disabled_presses += 1)
	for theme_index in [1, 0, 1]:
		var before_theme := toggles
		screen.select_theme(theme_index)
		for compact in [false, true]:
			screen.set_compact(compact)
			await settle()
			await show_switch_section()
			var variant := "components_switch" + ("__compact" if compact else "")
			check(device.theme_type_variation == variant, "Switch did not apply density")
			check(device.button_pressed and not server.button_pressed, "Theme/density lost the host's selection")
			check(toggles == before_theme, "Theme/density emitted a selection signal")
			for group_name in ["Group", "AudienceGroup", "DisabledGroup"]:
				var group := page.get_node(group_name)
				var surface: Panel = group.get_node("Background")
				check(surface.get_theme_stylebox("panel") == screen.theme.get_stylebox("panel", &"menu_group"), "Switch group lost its grouping material")
				check(surface.get_node("Grain").texture == screen.theme.get_icon("grain", &"menu_group"), "Switch group retained stale material grain")
				check(group.get_node("Inset").get_theme_constant("margin_left") == (16 if compact else 24), "Switch group density did not update")
			check(device.size.y >= (88 if compact else 116), "Switch hit target is too short")
			check(device.accessibility_name == device.title and device.accessibility_description == device.description, "Switch copy is not exposed to accessibility")
			check(device.get_node("Content/Row/Copy/Title").get_theme_font("font") == screen.theme.get_font("title_font", variant), "Switch retained a stale font")
			check(device.get_node("Content/Row/SelectionMark").texture == screen.theme.get_icon("selection_mark", "settings_choice"), "Switch must reuse the dropdown checkmark")
			check(locked.get_node("Content/Row/SelectionMark").self_modulate == device.get_theme_color("selection_disabled"), "Disabled checkmark must retain the dropdown's disabled treatment")
			check(not audience.vertical, "Three options should fit in one row at desktop width")
			check(absf(audience.get_child(0).size.x - audience.get_child(2).size.x) <= 1, "Three-option cards must share the available width, allowing native pixel rounding")
			for state in ["normal", "hover", "pressed", "hover_pressed", "disabled", "focus"]:
				check(screen.theme.has_stylebox(state, variant), "Missing Switch state: " + state)
				check(device.get_theme_stylebox(state) == screen.theme.get_stylebox(state, variant), "Switch retained a stale material")
			var style: StyleBox = device.get_theme_stylebox("disabled")
			check(style.selected != style.unselected, "Disabled selection lost its distinct surface")
			if theme_index == 1:
				check(style.selected.get_script() == preload("res://rookframe/ui/_internal/menu/choice_frame.gd"), "Fantasy Switch must use the new button's material frame")
				check(style.selected.texture == preload("res://rookframe/ui/menu/materials/walnut_plain_cap.tres"), "Fantasy Switch lost its walnut/brass texture")
				check(device.get_theme_stylebox("focus") is StyleBoxEmpty, "Fantasy focus must light the material itself")
			else:
				check(style.selected is StyleBoxFlat and style.selected.corner_detail == 1 and style.selected.shadow_size > 0, "Cyberpunk Switch lost the alloy button's cut corners and depth")
			await capture(("fantasy" if theme_index == 1 else "cyberpunk") + ("-compact" if compact else "-regular"))
			var previous_size := server.size
			await click(server)
			check(server.button_pressed and not device.button_pressed, "Pointer selection is not exclusive")
			check(server.get_node("Content/Row/SelectionMark").self_modulate.a > 0, "Checkmark did not follow native selection")
			check(device.get_node("Content/Row/SelectionMark").self_modulate.a == 0, "Unselected checkmark did not follow ButtonGroup")
			check(server.size.is_equal_approx(previous_size), "Selection caused a layout jump")
			server.grab_focus()
			await key(KEY_ENTER)
			check(server.button_pressed and not device.button_pressed, "Reactivating an exclusive choice cleared selection")
			device.grab_focus()
			await key(KEY_SPACE)
			check(device.button_pressed and not server.button_pressed, "Keyboard selection failed")
			await key(KEY_TAB)
			check(server.has_focus(), "Tab did not reach the next choice")
			await capture(("fantasy" if theme_index == 1 else "cyberpunk") + ("-compact-focus" if compact else "-regular-focus"))
			await click(locked)
			await click(unavailable)
			check(disabled_presses == 0 and locked.button_pressed and not unavailable.button_pressed, "Disabled choices accepted input or lost selection")
			server.release_focus()
			before_theme = toggles
	# Three-way selection is exclusive within its own group, independent of hosting.
	await click(audience.get_child(2))
	check(audience.get_child(2).button_pressed and not audience.get_child(0).button_pressed and not audience.get_child(1).button_pressed, "Third option did not participate in exclusive selection")
	check(device.button_pressed, "One Switch group changed another group's selection")
	audience.get_child(0).grab_focus()
	await key(KEY_TAB)
	check(audience.get_child(1).has_focus(), "Tab skipped the middle option")
	await key(KEY_SPACE)
	check(audience.get_child(1).button_pressed and not audience.get_child(2).button_pressed, "Keyboard selection failed in a three-option group")
	audience.get_child(1).release_focus()
	# Independent toggles and silent host updates use the same native state.
	var independent := Switch.instantiate() as Button
	independent.theme = screen.theme
	independent.title = "Allow guests"
	independent.position = Vector2(24, 800)
	independent.size.x = 350
	root.add_child(independent)
	await settle()
	independent.grab_focus()
	await key(KEY_SPACE)
	check(independent.button_pressed, "Independent Switch did not toggle on")
	await key(KEY_SPACE)
	check(not independent.button_pressed, "Independent Switch did not toggle off")
	independent.set_pressed_no_signal(true)
	await settle()
	if DisplayServer.get_name() != "headless":
		check(independent.get_node("Content/Row/SelectionMark").self_modulate.a > 0, "Silent native selection left stale visible copy")
	check(not independent.get_node("Content/Row/Copy/Description").visible, "An empty description retained a blank row")
	independent.description = "Long copy can wrap over several lines at a narrow width without being clipped by the native button."
	independent.size.x = 320
	await settle()
	var wrapped_height := independent.get_combined_minimum_size().y
	check(wrapped_height > independent.get_theme_constant("minimum_height"), "Long copy did not grow the Switch")
	independent.description = ""
	await settle()
	check(independent.get_combined_minimum_size().y < wrapped_height, "Switch minimum height did not shrink when copy was removed")
	independent.queue_free()
	await settle()
	# Wrapped copy grows the card; RTL moves the checkmark and selected edge together.
	device.title = "Run this World on a dedicated device with a longer translated title"
	device.description = "The World remains available while this device is running. Other players can join the same session and continue playing together."
	for theme_index in [0, 1]:
		screen.select_theme(theme_index)
		for width in [600, 900, 1440]:
			root.size = Vector2i(width, 1100)
			root.content_scale_size = root.size
			screen.size = root.size
			for rtl in [false, true]:
				page.layout_direction = Control.LAYOUT_DIRECTION_RTL if rtl else Control.LAYOUT_DIRECTION_LTR
				await settle()
				check(page.get_node("Group/Inset/Options").vertical == (width == 600), "Switch example did not reflow")
				check(audience.vertical == (width < 1440), "Three options did not stack according to their available width")
				check(audience.get_child(1).button_pressed, "Reflow lost the three-option selection")
				for card in [device, server]:
					var bounds: Rect2 = card.get_global_rect()
					check(bounds.position.x >= page.global_position.x - 1 and bounds.end.x <= page.get_global_rect().end.x + 1, "Switch overflows the page")
					for path in ["Content/Row/Copy/Title", "Content/Row/Copy/Description", "Content/Row/SelectionMark"]:
						check(bounds.encloses(card.get_node(path).get_global_rect()), "Switch copy overflows: %s theme=%d width=%d rtl=%s card=%s child=%s" % [path, theme_index, width, rtl, bounds, card.get_node(path).get_global_rect()])
					var copy: Control = card.get_node("Content/Row/Copy")
					var mark: Control = card.get_node("Content/Row/SelectionMark")
					check((mark.global_position.x < copy.global_position.x) == rtl, "Switch checkmark is not at the trailing edge")
				await capture(("fantasy" if theme_index == 1 else "cyberpunk") + "-%d%s" % [width, "-rtl" if rtl else ""])
	# More than three options and live additions use the same measured layout.
	for index in range(2):
		var extra := Switch.instantiate() as Button
		extra.title = "Extra choice %d" % index
		extra.button_group = audience.get_child(0).button_group
		audience.add_child(extra)
	await settle()
	check(audience.vertical and audience.get_child_count() == 5, "Five choices should stack when a readable row will not fit")
	check(audience.get_child(4).compact, "A dynamically added option did not inherit group density")
	for index in [3, 4]:
		audience.get_child(index).hide()
	await settle()
	check(not audience.vertical, "Hidden options still affected the group layout")
	for index in [3, 4]:
		audience.get_child(index).show()
	await settle()
	check(audience.vertical, "Showing options did not restore the stacked layout")
	for index in [4, 3]:
		audience.get_child(index).queue_free()
	await settle()
	check(not audience.vertical and audience.get_child_count() == 3, "Removing options did not restore the row")
	screen.queue_free()
	await process_frame
	print("SWITCH ", "PASS" if failures.is_empty() else failures)
	quit(0 if failures.is_empty() else 1)
