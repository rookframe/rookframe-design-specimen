extends "res://addons/gd-plug/plug.gd"

func request_quit(exit_code := -1) -> bool:
	return super.request_quit(0 if exit_code == -1 else exit_code)

func _plugging() -> void:
	plug("rookframe/rookframe-ui-kit", {"commit": "49e68941f6a7c73d0a9a0a427e2421bd728f312c", "include": ["rookframe/ui"]})
