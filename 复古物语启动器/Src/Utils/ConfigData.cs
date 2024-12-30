using System;
using Godot;

namespace 复古物语启动器.Utils;

public class ConfigData {
	private readonly string _configPath;
	private readonly ConfigFile _config = new();

	public ConfigData(string configPath) {
		_configPath = configPath;
		var error = _config.Load(configPath);
		switch (error) {
			case Error.Ok: return;
			case Error.FileNotFound: _config.Save(_configPath); break;
			default: throw new(error.ToString());
		}
	}

	public Variant this[string key] {
		get => _config.HasSectionKey("Data", key) ? _config.GetValue("Data", key) : default;
		set {
			_config.SetValue("Data", key, value);
			_config.Save(_configPath);
		}
	}

	public string[] GamePaths { get => (string[])this["GamePaths"]; set => this["GamePaths"] = value; }
}