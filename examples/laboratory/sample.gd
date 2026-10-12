extends Control
## Composition-only samples. All material and motion comes from the installed kit.
const PANEL = preload("res://rookframe/ui/components/surfaces/action_panel/action_panel.tscn")
const SECTION = preload("res://rookframe/ui/components/surfaces/action_panel/action_section.tscn")
const HINGED = preload("res://rookframe/ui/components/surfaces/hinged_leaf/hinged_leaf.tscn")
const PANEL_THEME = preload("res://rookframe/ui/theme/action_panel_theme.tres")
const LEDGER_THEME = preload("res://rookframe/ui/theme/silkbound_theme.tres")
const LINE_HEIGHT = preload("res://rookframe/ui/theme/silkbound_line_height.gd")
const TITLES = {"en": "Shortsword", "ru": "Короткий меч", "ja": "ショートソード"}
const CATEGORIES = {"en": ["Abilities", "Attacks", "Powers", "Items", "Features", "Companions", "Recovery"], "ru": ["Характеристики", "Атаки", "Силы", "Предметы", "Особенности", "Спутники", "Отдых"], "ja": ["能力", "攻撃", "魔法", "道具", "特徴", "仲間", "回復"]}
var recipe := 0
var long_content := false
var locale := "en"
var reduced := false
var extra_rows := 0
var bright := false
var surface: Control
var hinged: Control
var _scroll: ScrollContainer
var _column: VBoxContainer

func _ready() -> void:
	theme = LEDGER_THEME
	var backdrop := ColorRect.new()
	backdrop.color = Color("879383") if bright else Color("26302e")
	backdrop.mouse_filter = Control.MOUSE_FILTER_IGNORE
	add_child(backdrop)
	backdrop.set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
	_scroll = ScrollContainer.new()
	_scroll.get_v_scroll_bar().custom_minimum_size.x = 16
	_scroll.horizontal_scroll_mode = ScrollContainer.SCROLL_MODE_DISABLED
	add_child(_scroll)
	_scroll.set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
	var margin := MarginContainer.new()
	margin.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	margin.size_flags_vertical = Control.SIZE_EXPAND_FILL
	for edge in ["left", "right", "top", "bottom"]:
		margin.add_theme_constant_override("margin_" + edge, 16)
	_scroll.add_child(margin)
	_column = VBoxContainer.new()
	_column.alignment = BoxContainer.ALIGNMENT_CENTER
	margin.add_child(_column)
	if recipe == 1: _make_hud()
	elif recipe == 3: _make_ledger()
	else: _make_panel()
	resized.connect(_fit)
	_fit.call_deferred()

func _label(copy: String, parent: Node, heading := false) -> Label:
	var label := Label.new()
	label.text = copy
	label.language = locale
	label.autowrap_mode = TextServer.AUTOWRAP_WORD_SMART
	label.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	parent.add_child(label)
	if heading: label.add_theme_font_size_override("font_size", 26)
	if locale == "ja": label.add_theme_font_override("font", load("res://rookframe/ui/theme/locales/ja_regular.tres"))
	LINE_HEIGHT.apply(label, 1.2)
	return label

func _title() -> String:
	return TITLES[locale] + (" · " + " / ".join(CATEGORIES[locale]) if long_content else "")

func _make_panel() -> void:
	surface = PANEL.instantiate()
	surface.reduced_motion = reduced
	surface.show_footer = recipe == 0
	surface.size_flags_horizontal = Control.SIZE_SHRINK_CENTER
	_column.add_child(surface)
	_label("Graveworm", surface.get_header_slot())
	_label(_title() if recipe == 0 else "The March of the Dead", surface.get_header_slot(), true)
	var icon := TextureRect.new()
	icon.texture = load("res://rookframe/ui/icons/game-icons/broadsword.svg" if recipe == 0 else "res://rookframe/ui/icons/game-icons/evil-book.svg")
	icon.custom_minimum_size = Vector2(32, 32)
	icon.self_modulate = PANEL_THEME.get_color("icon_normal_color", "RookframeActionIcon")
	icon.expand_mode = TextureRect.EXPAND_IGNORE_SIZE
	icon.stretch_mode = TextureRect.STRETCH_KEEP_ASPECT_CENTERED
	surface.get_emblem_slot().add_child(icon)
	var section := SECTION.instantiate()
	surface.get_sections_slot().add_child(section)
	var contents := VBoxContainer.new()
	section.add_child(contents)
	if recipe == 0:
		var row := HBoxContainer.new()
		contents.add_child(row)
		_label(CATEGORIES[locale][1], row)
		_label("1d20 − 1", row, true)
		var roll := Button.new()
		roll.theme_type_variation = &"RookframeActionRoll"
		roll.custom_minimum_size = Vector2(56, 52)
		roll.accessibility_name = "Example roll; no rules executed"
		row.add_child(roll)
		var divider := HSeparator.new()
		divider.theme_type_variation = &"RookframeActionDivider"
		contents.add_child(divider)
		_label("Damage · on hit     d4", contents)
		var field := LineEdit.new()
		field.placeholder_text = "Improvised object"
		field.accessibility_name = "Object name"
		field.custom_minimum_size.y = 44
		contents.add_child(field)
		var choice := OptionButton.new()
		choice.accessibility_name = "Attack type"
		choice.add_item("Melee")
		choice.add_item("Ranged")
		choice.custom_minimum_size.y = 44
		contents.add_child(choice)
	else:
		_label("Reference description · synthetic content", contents)
		_label("Share an explanation while the table resolves its effects. This sample demonstrates wrapping, reading and material composition without implementing a game rule.", contents)
	for i in range(extra_rows):
		_label("%s · %s" % [i + 1, _title()], contents)
	if recipe == 0: _label("Restless dead · 4 ft", surface.get_footer_slot())

func _make_hud() -> void:
	surface = VBoxContainer.new()
	surface.add_theme_constant_override("separation", 26)
	surface.size_flags_horizontal = Control.SIZE_SHRINK_CENTER
	_column.add_child(surface)
	hinged = HINGED.instantiate()
	hinged.reduced_motion = reduced
	hinged.custom_minimum_size = Vector2(0, 158)
	surface.add_child(hinged)
	# A SubViewport is its own Theme inheritance boundary.
	hinged.get_content().theme = PANEL_THEME
	var margin := MarginContainer.new()
	hinged.get_content().add_child(margin)
	margin.set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
	for edge in ["left", "right", "top", "bottom"]: margin.add_theme_constant_override("margin_" + edge, 18)
	var rows := VBoxContainer.new()
	margin.add_child(rows)
	_label(CATEGORIES[locale][1], rows, true)
	_label(_title() + "    d4", rows)
	_label("Unarmed    d2", rows)
	var support := SECTION.instantiate()
	support.theme = PANEL_THEME
	surface.add_child(support)
	var categories := GridContainer.new()
	categories.columns = 4
	support.add_child(categories)
	for title in CATEGORIES[locale]:
		var button := Button.new()
		button.text = title
		button.language = locale
		if locale == "ja": button.add_theme_font_override("font", load("res://rookframe/ui/theme/locales/ja_regular.tres"))
		button.custom_minimum_size.y = 44
		button.size_flags_horizontal = Control.SIZE_EXPAND_FILL
		button.pressed.connect(func() -> void: hinged.set_open(not hinged.is_open))
		categories.add_child(button)
	hinged.set_open.call_deferred(true)

func _make_ledger() -> void:
	surface = PanelContainer.new()
	# This owner composes stock material resources; records remain unframed.
	surface.add_theme_stylebox_override("panel", LEDGER_THEME.get_stylebox("panel", "WizardLedger"))
	surface.size_flags_horizontal = Control.SIZE_SHRINK_CENTER
	_column.add_child(surface)
	var linen := TextureRect.new()
	linen.texture = load("res://rookframe/ui/assets/silkbound-ledger/linen.png")
	linen.expand_mode = TextureRect.EXPAND_IGNORE_SIZE
	linen.stretch_mode = TextureRect.STRETCH_TILE
	linen.mouse_filter = Control.MOUSE_FILTER_IGNORE
	var cloth_inset := MarginContainer.new()
	cloth_inset.add_theme_constant_override("margin_top", 6)
	cloth_inset.add_theme_constant_override("margin_bottom", 6)
	surface.add_child(cloth_inset)
	cloth_inset.add_child(linen)
	var inset := MarginContainer.new()
	for edge in ["left", "right", "top", "bottom"]: inset.add_theme_constant_override("margin_" + edge, 16)
	surface.add_child(inset)
	var rows := VBoxContainer.new()
	inset.add_child(rows)
	_label("Action log · reading composition", rows, true)
	for i in range(3 + extra_rows):
		_label("Game Master · 11:%02d" % (5 + i), rows)
		_label("Graveworm · " + _title(), rows)
		_label("Miss · Rolled 1d20 − 1 = 9\nAttack d20 [10]\nGraveworm misses Restless dead.", rows)
		var divider := HSeparator.new()
		divider.custom_minimum_size.y = 1
		rows.add_child(divider)

func _fit() -> void:
	if surface == null: return
	var width := minf(580 if recipe == 1 else 520, maxf(240, size.x - 32))
	surface.custom_minimum_size.x = width
	if recipe == 0 or recipe == 2: surface.compact = size.x < 1100 or size.y < 600
	if hinged != null:
		hinged.custom_minimum_size.y = 185 if long_content else 158
	surface.reset_size()

func replay() -> void:
	if hinged != null:
		hinged.set_open(false, false)
		hinged.set_open(true)
	elif surface.has_method("get_header_slot"):
		surface.hide()
		surface.show()
