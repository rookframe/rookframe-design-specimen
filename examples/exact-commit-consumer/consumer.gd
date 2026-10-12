extends Control


func _on_search_value_changed(value: String) -> void:
	%ProofStatus.text = "Search value changed to: %s" % value


func _on_search_cleared() -> void:
	%ProofStatus.text = "Public Search Field cleared"
