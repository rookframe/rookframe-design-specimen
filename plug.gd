extends "res://addons/gd-plug/plug.gd"

func request_quit(exit_code := -1) -> bool:
	return super.request_quit(0 if exit_code == -1 else exit_code)

func _plugging() -> void:
	plug("rookframe/rookframe-ui-kit", {"commit": "db82c1d1b5fe8380a350ca7773b3df52f6fd21de", "include": ["rookframe/ui"]})
