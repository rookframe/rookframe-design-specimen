extends "res://rookframe/ui/components/content/miniature_browser.gd"
## Throwaway split-pane sizing; native search, selection, cards and paging stay intact.
func _filter(query: String) -> void:
	super._filter(query)
	get_node(^"Results").visible = _state == "ready" and not _matches.is_empty()

func _arrange() -> void:
	if not is_inside_tree(): return
	var results := get_node(^"Results") as ScrollContainer
	results.vertical_scroll_mode = ScrollContainer.SCROLL_MODE_SHOW_NEVER if paginated else ScrollContainer.SCROLL_MODE_AUTO
	get_node(^"Pager").visible = paginated and _state == "ready" and not _matches.is_empty()
	var width: float = results.size.x - (0 if paginated else results.get_v_scroll_bar().get_combined_minimum_size().x)
	var columns := clampi(int((width + 12) / 180), 1, 4)
	get_node(^"Results/Rows").columns = columns
	var caption_height := 40.0
	for row in _rows:
		caption_height = maxf(caption_height, row.get_node(^"Content/Copy").get_combined_minimum_size().y)
	var rows := 1 if results.size.y < 360 else 2
	_capacity = columns * rows
	var cell_height := floorf((results.size.y - (rows - 1) * 12) / rows)
	var preview_height := maxf(48, cell_height - caption_height - 24)
	if not paginated: preview_height = maxf(56, minf(140, results.size.y - caption_height - 24))
	for row in _rows:
		row.minimum_height = 0
		row.custom_minimum_size = Vector2(136, preview_height + caption_height + 24)
		row.get_node(^"Content/Stage").custom_minimum_size.y = preview_height
	if paginated: _show_page()
