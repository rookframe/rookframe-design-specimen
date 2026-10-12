extends Control

signal return_requested

const TASK_HEADER_SCENE := preload("res://rookframe/ui/components/layout/task_header.tscn")
const ACTION_BAR_SCENE := preload("res://rookframe/ui/components/layout/action_bar.tscn")
const SEARCH_FIELD_SCENE := preload("res://rookframe/ui/components/forms/search_field.tscn")
const METRIC_SCENE := preload("res://rookframe/ui/components/data/metric.tscn")


func _ready() -> void:
	%CurrentCatalogue.pressed.connect(_return_to_current)
	_select_requested_tab()
	_populate_native_controls()
	_populate_layout_relationships()
	_populate_data_relationships()
	_populate_icon_gallery()
	_populate_managed_surface()
	_connect_overlays()
	resized.connect(_fit_catalogue)
	%Tabs.tab_changed.connect(func(_index: int) -> void: _fit_catalogue())
	_fit_catalogue()
	_add_locale_samples()


func _fit_catalogue() -> void:
	# Reflow the catalogue itself, without scaling the controls being reviewed.
	var compact := size.x < 1000
	var native := %Tabs.get_node("Native/Margin/Content")
	native.get_node("Inputs").columns = 2 if compact else 3
	for name in ["SemanticSurfaces", "Dense"]:
		native.get_node(name).vertical = compact
	%Tabs.get_node("Feedback/Margin/Content/States").columns = 1 if compact else 2
	%Tabs.get_node("Managed/Columns/Explanation").visible = not compact
	%ManagedDemo.custom_minimum_size = Vector2(360, 0)
	var header: Node = %ManagedDemo.call(&"get_header_slot").get_child(0)
	header.set("eyebrow", "" if compact else "Package task")
	header.set("subtitle", "" if compact else "The header, one scrolling task body, and footer remain one retained tree.")
	$Canvas/Page/Stack/Header/Copy/Title.add_theme_font_size_override(&"font_size", 30 if compact else 40)
	$Canvas/Page/Stack/Header/Copy/Subtitle.visible = size.y >= 600
	$Canvas/Page.add_theme_constant_override(&"margin_top", 12 if compact else 24)
	$Canvas/Page.add_theme_constant_override(&"margin_bottom", 12 if compact else 24)
	var compact_managed: bool = compact and %Tabs.current_tab == 6
	$Canvas/Page/Stack/Header.visible = not compact_managed
	for edge in ["top", "bottom"]:
		$Canvas/Page.add_theme_constant_override("margin_" + edge, 4 if compact_managed else 12 if compact else 24)
	for edge in ["left", "right", "top", "bottom"]:
		%Tabs.get_node("Managed").add_theme_constant_override("margin_" + edge, 0 if compact_managed else 16)


func _add_locale_samples() -> void:
	var content := %Tabs.get_node("Native/Margin/Content")
	var heading := _copy("Regional serif companions")
	heading.theme_type_variation = &"RookframeHeading"
	content.add_child(heading)
	for sample in [
		["ja", "日本語 · Rookframe — 冒険の記録、骨と直感。"],
		["ko", "한국어 · Rookframe — 모험의 기록과 세계."],
		["zh_CN", "简体中文 · Rookframe — 世界与冒险的记录。"],
		["zh_TW", "繁體中文 · Rookframe — 世界與冒險的記錄。"],
	]:
		var label := _copy(sample[1])
		label.language = sample[0]
		label.add_theme_font_override(&"font", load("res://rookframe/ui/theme/locales/%s_regular.tres" % sample[0]))
		content.add_child(label)


func _select_requested_tab() -> void:
	for argument in OS.get_cmdline_user_args():
		if argument.begins_with("--catalogue-tab="):
			%Tabs.current_tab = clampi(int(argument.get_slice("=", 1)), 0, %Tabs.get_tab_count() - 1)


func _populate_native_controls() -> void:
	var item_list := %NativeList as ItemList
	for item in ["Ancient gate", "Clockwork archive", "Lantern chamber", "Disabled record"]:
		item_list.add_item(item)
	item_list.select(1)
	item_list.set_item_disabled(3, true)

	var tree := %NativeTree as Tree
	var root := tree.create_item()
	tree.hide_root = true
	var surfaces := tree.create_item(root)
	surfaces.set_text(0, "Surfaces")
	var docked := tree.create_item(surfaces)
	docked.set_text(0, "Docked task")
	var floating := tree.create_item(surfaces)
	floating.set_text(0, "Floating task")
	var records := tree.create_item(root)
	records.set_text(0, "Structured records")
	records.set_selectable(0, false)
	docked.select(0)

	var option := %NativeOption as OptionButton
	for item in ["Default", "Compact", "Expanded"]:
		option.add_item(item)
	option.select(1)

	var menu := (%NativeMenu as MenuButton).get_popup()
	menu.add_icon_item(preload("res://rookframe/ui/icons/edit.svg"), "Edit", 1)
	menu.add_icon_item(preload("res://rookframe/ui/icons/copy.svg"), "Duplicate", 2)
	menu.add_separator()
	menu.add_icon_item(preload("res://rookframe/ui/icons/delete.svg"), "Delete", 3)
	menu.set_item_disabled(3, true)


func _populate_layout_relationships() -> void:
	var split := %ResponsiveSplit
	_add_panel_copy(split.call(&"get_primary_slot"), "Stage", "Bounded stage context stays first in compact reading order.")
	_add_panel_copy(split.call(&"get_secondary_slot"), "Content", "The primary task owns more width in the wide profile and the same retained controls in the compact profile.")

	var section := %SectionDemo
	section.call(&"get_action_slot").add_child(_button("Inspect", &"RookframeQuietButton"))
	section.call(&"get_body_slot").add_child(_copy("Section bodies accept fields, rows, native containers, or Package-owned compositions without a second content inset."))

	var action_bar := %ActionBarDemo
	action_bar.call(&"get_leading_slot").add_child(_button("Cancel", &"RookframeDangerButton"))
	action_bar.call(&"get_trailing_slot").add_child(_button("Back", &"RookframeSecondaryButton"))
	action_bar.call(&"get_trailing_slot").add_child(_button("Continue", &"RookframePrimaryButton"))

	var toolbar := %ToolbarDemo
	var search := SEARCH_FIELD_SCENE.instantiate()
	search.set("show_label", false)
	search.set("placeholder", "Filter records")
	search.custom_minimum_size.x = 360
	search.size_flags_horizontal = Control.SIZE_SHRINK_BEGIN
	toolbar.call(&"get_leading_slot").add_child(search)
	var count := _copy("24 records")
	count.custom_minimum_size.x = 100
	count.theme_type_variation = &"RookframeStatus"
	toolbar.call(&"get_leading_slot").add_child(count)
	toolbar.call(&"get_trailing_slot").add_child(_button("Filters", &"RookframeSecondaryButton", preload("res://rookframe/ui/icons/filter.svg")))
	toolbar.call(&"get_trailing_slot").add_child(_button("Add", &"RookframePrimaryButton", preload("res://rookframe/ui/icons/add.svg")))

	for item in [
		["Create", "add.svg"],
		["Open", "folder.svg"],
		["Duplicate", "copy.svg"],
		["Settings", "settings.svg"],
	]:
		%IconActionGrid.add_child(_button(item[0], &"RookframeSecondaryButton", load("res://rookframe/ui/icons/%s" % item[1])))


func _populate_data_relationships() -> void:
	for definition in [
		["Health", "18 / 24", "Wounded"],
		["Armor", "2", "Light"],
		["Omens", "d2", "Ready"],
		["Load", "5 / 8", "Three free"],
	]:
		var metric := METRIC_SCENE.instantiate()
		metric.set("label_text", definition[0])
		metric.set("value_text", definition[1])
		metric.set("detail_text", definition[2])
		%MetricStrip.add_child(metric)


func _populate_icon_gallery() -> void:
	var manifest_source := FileAccess.get_file_as_string("res://rookframe/ui/icons/manifest.json")
	var manifest: Variant = JSON.parse_string(manifest_source)
	if not manifest is Dictionary:
		push_error("Could not parse the semantic icon manifest.")
		return
	for definition: Dictionary in manifest.get("icons", []) + manifest.get("domainPictograms", []):
		var semantic_name := str(definition.get("name", ""))
		var accessible_label := str(definition.get("label", semantic_name))
		var cell := PanelContainer.new()
		cell.custom_minimum_size = Vector2(160, 96)
		cell.theme_type_variation = &"RookframeInsetSurface"
		cell.accessibility_name = "%s icon" % accessible_label
		if definition.has("author"):
			cell.tooltip_text = "%s — %s, CC BY 3.0\n%s" % [accessible_label, definition.author, definition.source]
		var stack := VBoxContainer.new()
		stack.alignment = BoxContainer.ALIGNMENT_CENTER
		stack.add_theme_constant_override(&"separation", 8)
		cell.add_child(stack)
		var icon := TextureRect.new()
		icon.custom_minimum_size = Vector2(32, 32)
		icon.texture = load("res://rookframe/ui/icons/" + str(definition.get("path", semantic_name + ".svg")))
		icon.self_modulate = RookframeUiTokens.COLOR_PICTOGRAM
		icon.expand_mode = TextureRect.EXPAND_IGNORE_SIZE
		icon.stretch_mode = TextureRect.STRETCH_KEEP_ASPECT_CENTERED
		icon.mouse_filter = Control.MOUSE_FILTER_IGNORE
		icon.accessibility_name = accessible_label
		stack.add_child(icon)
		var label := _copy(semantic_name)
		label.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
		label.theme_type_variation = &"RookframeMeta"
		stack.add_child(label)
		%IconGallery.add_child(cell)


func _populate_managed_surface() -> void:
	var surface := %ManagedDemo
	var header := TASK_HEADER_SCENE.instantiate()
	header.set("eyebrow", "Package task")
	header.set("title", "Review admitted records")
	header.set("subtitle", "The header, one scrolling task body, and footer remain one retained tree.")
	surface.call(&"get_header_slot").add_child(header)

	var task := VBoxContainer.new()
	task.add_theme_constant_override(&"separation", 12)
	for index in range(1, 9):
		var row := PanelContainer.new()
		row.theme_type_variation = &"RookframeStructuredRow"
		var label := _copy("Retained record %d - scroll position survives dock, float, minimize, and restore." % index)
		row.add_child(label)
		task.add_child(row)
	surface.call(&"set_task", task)

	var footer := ACTION_BAR_SCENE.instantiate()
	footer.call(&"get_leading_slot").add_child(_button("Cancel", &"RookframeDangerButton"))
	footer.call(&"get_trailing_slot").add_child(_button("Apply", &"RookframePrimaryButton"))
	surface.call(&"get_footer_slot").add_child(footer)


func _connect_overlays() -> void:
	%OpenAccept.pressed.connect(func() -> void: %AcceptDialogDemo.call(&"open_dialog"))
	%OpenConfirm.pressed.connect(func() -> void: %ConfirmationDialogDemo.call(&"open_dialog"))
	%OpenFile.pressed.connect(func() -> void: %FileDialogDemo.call(&"open_picker", ProjectSettings.globalize_path("res://")))


func _add_panel_copy(parent: Node, heading: String, body: String) -> void:
	var panel := PanelContainer.new()
	panel.theme_type_variation = &"RookframeInsetSurface"
	panel.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	panel.size_flags_vertical = Control.SIZE_EXPAND_FILL
	var stack := VBoxContainer.new()
	stack.add_theme_constant_override(&"separation", 8)
	panel.add_child(stack)
	var heading_label := _copy(heading)
	heading_label.theme_type_variation = &"RookframeHeading"
	stack.add_child(heading_label)
	stack.add_child(_copy(body))
	parent.add_child(panel)


func _copy(text: String) -> Label:
	var label := Label.new()
	label.text = text
	label.theme_type_variation = &"RookframeBody"
	label.autowrap_mode = TextServer.AUTOWRAP_WORD_SMART
	label.mouse_filter = Control.MOUSE_FILTER_IGNORE
	return label


func _button(text: String, variation: StringName, icon: Texture2D = null) -> Button:
	var button := Button.new()
	button.text = text
	button.theme_type_variation = variation
	button.custom_minimum_size.y = 44
	button.accessibility_name = text
	button.tooltip_text = text
	button.icon = icon
	return button


func _return_to_current() -> void:
	if return_requested.get_connections().is_empty():
		get_tree().change_scene_to_file("res://examples/catalogue/visual_catalogue.tscn")
	else:
		return_requested.emit()
