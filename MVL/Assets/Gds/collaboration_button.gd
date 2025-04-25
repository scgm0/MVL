extends Button
@export var button: Button

func _ready() -> void:
	pressed.connect(_on_pressed)

func _on_pressed() -> void:
	button.button_pressed = true
	button.pressed.emit()
	pass
