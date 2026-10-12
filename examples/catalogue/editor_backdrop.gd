extends Control
## Catalogue illustration only: a quiet top-down map behind the live controls.


func _ready() -> void:
	resized.connect(queue_redraw)
	theme_changed.connect(queue_redraw)


func _draw() -> void:
	var accent := get_theme_color("font_color", &"editor_accent")
	var cyberpunk := accent.b > accent.r
	var ground := Color("172e32") if cyberpunk else Color("344538")
	draw_rect(Rect2(Vector2.ZERO, size), ground)
	# Low-contrast contour lines leave the controls as the highest contrast layer.
	for ring in range(9):
		var points := PackedVector2Array()
		for step in range(81):
			var angle := TAU * step / 80.0
			var radius := 88.0 + ring * 26 + sin(angle * 3 + ring * 0.4) * 18
			points.append(size * Vector2(0.48, 0.48) + Vector2(cos(angle) * 1.6, sin(angle)) * radius)
		draw_polyline(points, Color(accent, 0.065), 1, true)
	for x in range(0, int(size.x), 40):
		draw_line(Vector2(x, 0), Vector2(x, size.y), Color(accent, 0.035))
	for y in range(0, int(size.y), 40):
		draw_line(Vector2(0, y), Vector2(size.x, y), Color(accent, 0.035))
	var center := size * Vector2(0.48, 0.46)
	var stone := Color("597478") if cyberpunk else Color("8f9276")
	draw_circle(center + Vector2(0, 7), 71, Color("00000033"))
	draw_circle(center, 68, stone)
	draw_circle(center, 58, ground.lightened(0.07))
	for beam in range(8):
		var angle := TAU * beam / 8.0
		var direction := Vector2.from_angle(angle)
		draw_line(center + direction * 57, center + direction * 68, ground, 3, true)
	draw_arc(center, 80, 0, TAU, 80, Color(accent, 0.55), 1, true)
	for offset in [Vector2(-80, -80), Vector2(80, -80), Vector2(-80, 80), Vector2(80, 80)]:
		draw_rect(Rect2(center + offset - Vector2(3, 3), Vector2(6, 6)), accent)
