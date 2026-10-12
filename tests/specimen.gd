extends SceneTree
const LAB = preload("res://examples/laboratory/laboratory.tscn")
const SAMPLE = preload("res://examples/laboratory/sample.gd")
var failures: Array[String] = []
var capture_dir := ""
var cases := 0

func _initialize() -> void:
	for argument in OS.get_cmdline_user_args():
		if argument.begins_with("--capture-dir="): capture_dir = argument.trim_prefix("--capture-dir=")
	if not capture_dir.is_empty(): DirAccess.make_dir_recursive_absolute(capture_dir)
	verify.call_deferred()

func check(condition: bool, message: String) -> void:
	if not condition:
		failures.append(message)
		push_error(message)

func settle() -> void:
	for frame in range(12): await process_frame

func capture(name: String) -> void:
	if capture_dir.is_empty() or DisplayServer.get_name() == "headless": return
	await RenderingServer.frame_post_draw
	check(root.get_texture().get_image().save_png(capture_dir.path_join(name + ".png")) == OK, "Capture failed: " + name)

func add_sample(recipe: int, reduced := true, long_text := false, locale := "en", bright := false) -> Control:
	var sample := Control.new()
	sample.set_script(SAMPLE)
	sample.recipe = recipe
	sample.reduced = reduced
	sample.long_content = long_text
	sample.extra_rows = 12 if long_text and recipe != 1 else 0
	sample.locale = locale
	sample.bright = bright
	root.add_child(sample)
	sample.set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
	return sample

func remove_sample(sample: Control) -> void:
	root.remove_child(sample)
	sample.queue_free()
	await process_frame

func check_bounds(sample: Control, canvas: Vector2i) -> void:
	var rect: Rect2 = sample.surface.get_global_rect()
	check(rect.position.x >= 0 and rect.end.x <= canvas.x, "Surface exceeds %s, recipe %d" % [canvas, sample.recipe])
	var scroll: ScrollContainer = sample.get_child(1)
	var bar := scroll.get_v_scroll_bar()
	if bar.max_value > bar.page:
		check(bar.visible and bar.size.x >= 16, "Overflow must have a visible, usable scrollbar")
		var before := scroll.scroll_vertical
		scroll.scroll_vertical = int(bar.max_value)
		check(scroll.scroll_vertical > before, "Overflow cannot be scrolled")
		scroll.scroll_vertical = 0

func verify() -> void:
	var lab := LAB.instantiate()
	root.add_child(lab)
	await settle()
	check(lab.sample.recipe == 0, "Default must open Silkbound action leaves")
	check(lab.sample.surface.scene_file_path == "res://rookframe/ui/components/surfaces/action_panel/action_panel.tscn", "Default must instance production Action Panel")
	if DisplayServer.get_name() != "headless":
		await create_timer(1.6).timeout
		await capture("launcher")
	lab.options.recipe = 1
	lab.refresh()
	await settle()
	check(lab.sample.hinged.scene_file_path == "res://rookframe/ui/components/surfaces/hinged_leaf/hinged_leaf.tscn", "HUD must instance production hinged leaf")
	var pause: CheckButton
	var replay: Button
	for button in lab.find_children("*", "Button", true, false):
		if button.text == "Pause": pause = button
		if button.text == "Replay": replay = button
	pause.button_pressed = true
	check(Engine.time_scale == 0.0, "Pause must pause native animation")
	replay.pressed.emit()
	check(not pause.button_pressed and Engine.time_scale == 1.0, "Replay must synchronize Pause state")
	pause.button_pressed = true
	lab.options.reduced = true
	lab.refresh()
	await settle()
	check(not pause.button_pressed, "Refreshing the sample must reset Pause")
	check(lab.sample.hinged.is_open and not lab.sample.hinged.animating, "Reduced motion must settle immediately")
	root.remove_child(lab)
	lab.queue_free()
	await settle()
	for canvas in [Vector2i(1920, 1080), Vector2i(1024, 768), Vector2i(844, 390)]:
		root.size = canvas
		for recipe in range(4):
			for long_text in [false, true]:
				cases += 1
				var sample := add_sample(recipe, true, long_text, "ja" if long_text else "en", long_text)
				await settle()
				check_bounds(sample, canvas)
				if recipe == 1: check(sample.hinged.is_open and not sample.hinged.animating, "Reduced leaf did not settle")
				await capture("%dx%d-recipe-%d%s" % [canvas.x, canvas.y, recipe, "-long-ja-bright" if long_text else ""])
				await remove_sample(sample)
		# Graphical rendering is necessary for native SubViewport entrance animation.
		if DisplayServer.get_name() != "headless": await verify_motion(canvas)
	root.size = Vector2i(844, 390)
	var russian := add_sample(1, true, true, "ru")
	await settle()
	check_bounds(russian, root.size)
	await capture("hud-phone-long-ru")
	await remove_sample(russian)
	print("SPECIMEN: %d native-size cases; %d failures" % [cases, failures.size()])
	quit(0 if failures.is_empty() else 1)

func verify_motion(canvas: Vector2i) -> void:
	for recipe in [0, 1]:
		var sample := add_sample(recipe, false)
		await settle()
		await create_timer(0.24).timeout
		Engine.time_scale = 0.0
		await capture("%dx%d-recipe-%d-paused-motion" % [canvas.x, canvas.y, recipe])
		Engine.time_scale = 1.0
		# Interrupt the entrance by replaying its public presentation operation.
		sample.replay()
		await create_timer(1.7).timeout
		if recipe == 1:
			check(sample.hinged.is_open and not sample.hinged.animating, "Interrupted HUD opening did not settle")
			sample.hinged.set_open(false)
			await create_timer(0.4).timeout
			check(not sample.hinged.is_open and not sample.hinged.animating, "HUD closing did not settle")
			await capture("%dx%d-hud-folded" % [canvas.x, canvas.y])
		else:
			for leaf in sample.surface.linked_leaves():
				check(leaf.mouse_behavior_recursive != Control.MOUSE_BEHAVIOR_DISABLED, "Action entrance did not restore input")
				for label in leaf.find_children("*", "Label", true, false):
					check(label.material == null, "Action entrance left ink material on readable content")
			var field: LineEdit = sample.surface.find_children("*", "LineEdit", true, false)[0]
			field.grab_focus()
			check(field.has_focus(), "Action input cannot take native focus after entrance")
			await capture("%dx%d-action-focused" % [canvas.x, canvas.y])
			var roll: Button = sample.surface.find_children("*", "Button", true, false)[0]
			roll.disabled = true
			await capture("%dx%d-action-disabled" % [canvas.x, canvas.y])
		await remove_sample(sample)
