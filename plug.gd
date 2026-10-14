extends "res://addons/gd-plug/plug.gd"

func request_quit(exit_code := -1) -> bool:
	return super.request_quit(0 if exit_code == -1 else exit_code)

func _plugging() -> void:
	plug("rookframe/rookframe-ui-kit", {"commit": "1b4105151d3a802360e2e243cb34809101ad731e", "include": ["rookframe/ui"]})
