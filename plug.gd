extends "res://addons/gd-plug/plug.gd"

func request_quit(exit_code := -1) -> bool:
	return super.request_quit(0 if exit_code == -1 else exit_code)

func _plugging() -> void:
	plug("rookframe/rookframe-ui-kit", {"commit": "7cebf49128f56a58542465ca309de0dcbd92cc0c", "include": ["rookframe/ui"]})
