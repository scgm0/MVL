extends Translation
class_name DefaultTranslation

func _get_message(src_message: StringName, _context: StringName) -> StringName:
	if TranslationServer.compare_locales(locale, TranslationServer.get_locale()) == 10:
		return src_message
	else:
		return StringName()

func _get_plural_message(src_message: StringName, _src_plural_message: StringName, _n: int, _context: StringName) -> StringName:
	if TranslationServer.compare_locales(locale, TranslationServer.get_locale()) == 10:
		return src_message
	else:
		return StringName()
