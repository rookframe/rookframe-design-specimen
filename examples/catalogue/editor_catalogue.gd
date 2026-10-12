extends VBoxContainer
## Demonstration only. The kit owns controls; a host owns editor/domain actions.
signal feedback(message: String)
var _log_count := 0


func _ready() -> void:
	resized.connect(_fit)
	for rail in [%LeftRail, %RightRail]:
		var group := ButtonGroup.new()
		for button: Button in rail.get_node("Tools").get_children():
			button.button_group = group
			button.pressed.connect(func() -> void:
				%ToolStatus.text = button.accessibility_name + " active"
				feedback.emit(button.accessibility_name + " selected.")
			)
	var layer_group := ButtonGroup.new()
	for button: Button in %LayerList.get_children():
		button.button_group = layer_group
		button.pressed.connect(func() -> void:
			%Selection.text = button.text.strip_edges() + " selected"
			feedback.emit(%Selection.text)
		)
	%AddObject.pressed.connect(func() -> void:
		_log_count += 1
		%LogTitle.text = "Object placed · %d" % _log_count
		%LogDetail.text = "Scene changes appear in this surface."
		feedback.emit("Example object placed.")
	)
	%ResetView.pressed.connect(func() -> void:
		%ToolStatus.text = "View reset · 100%"
		feedback.emit("Example view reset.")
	)
	_fit()


func configure(active_theme: Theme, compact: bool) -> void:
	for cell in %States.get_children():
		cell.get_node("Preview").configure(active_theme, compact)


func _fit() -> void:
	var narrow := size.x < 900
	%Workspace.vertical = narrow
	%Inspector.custom_minimum_size.x = 0 if narrow else 340
	%States.columns = 3 if size.x < 640 else 6
