extends "res://addons/gd-plug/plug.gd"

func request_quit(exit_code := -1) -> bool:
	return super.request_quit(0 if exit_code == -1 else exit_code)

func _plugging() -> void:
	plug("rookframe/rookframe-ui-kit", {"commit": "625fde48d7a989cc5457547df7e2c86c9609790a", "include": ["rookframe/ui"]})
