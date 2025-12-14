using System;
using System.IO;
using System.Linq;
using Godot;
using Microsoft.Extensions.Logging;
using Utf8StringInterpolation;
using ZLogger;

namespace MVL.Utils;

public static class Log {
	private const string LogName = "MVL";
	static private string LogPath { get; } = Paths.LogFolder.PathJoin($"{LogName}.log");

	static private ILoggerFactory? _factory;
	static private ILogger? _logger;

	public static void LogOutput(LogLevel level, ReadOnlySpan<char> message, Exception? exception = null) {
		var localTime = DateTimeOffset.Now;
		var messageStr = $"{localTime:yyyy-MM-dd HH:mm:ss.fff} | {level:G} | {message}";
		if (_logger is not null) {
			_logger.ZLog(level, exception, $"{messageStr}");
		} else {
			if (level is LogLevel.Error or LogLevel.Critical) {
				GD.PrintErr(exception == null ? messageStr : $"{messageStr}\n{exception}");
			} else {
				GD.Print(messageStr);
			}
		}
	}

	public static void Trace(ReadOnlySpan<char> message, Exception? exception = null) =>
		LogOutput(LogLevel.Trace, message, exception);

	public static void Debug(ReadOnlySpan<char> message, Exception? exception = null) =>
		LogOutput(LogLevel.Debug, message, exception);

	public static void Info(ReadOnlySpan<char> message, Exception? exception = null) =>
		LogOutput(LogLevel.Information, message, exception);

	public static void Warn(ReadOnlySpan<char> message, Exception? exception = null) =>
		LogOutput(LogLevel.Warning, message, exception);

	public static void Error(ReadOnlySpan<char> message, Exception? exception = null) =>
		LogOutput(LogLevel.Error, message, exception);

	public static void Error(Exception exception) => LogOutput(LogLevel.Error, exception.Message, exception);

	public static void Critical(ReadOnlySpan<char> message, Exception? exception = null) =>
		LogOutput(LogLevel.Critical, message, exception);

	public static void Critical(Exception exception) => LogOutput(LogLevel.Critical, exception.Message, exception);

	static private void Init() {
		try {
			_factory = LoggerFactory.Create(builder => {
				builder.SetMinimumLevel(LogLevel.Debug)
					.AddZLoggerConsole(options => {
						options.ConfigureEnableAnsiEscapeCode = true;
						options.UsePlainTextFormatter(formatter => {
							formatter.SetPrefixFormatter($"{0}",
								(in t, in i) => {
									var color = i.LogLevel switch {
										LogLevel.Trace => "\e[90m",
										LogLevel.Debug => "\e[0m",
										LogLevel.Information => "\e[32m",
										LogLevel.Warning => "\e[33m",
										LogLevel.Error => "\e[31m",
										LogLevel.Critical => "\e[35m",
										_ => "\e[0m"
									};
									t.Format(color);
								});
							formatter.SetSuffixFormatter($"{0}",
								(in t, in _) => { t.Format("\e[0m"); });
							formatter.SetExceptionFormatter((writer, ex) =>
								Utf8String.Format(writer, $"\n\e[90m{ex}\e[0m"));
						});
					}).AddZLoggerFile(LogPath,
						options => { options.UsePlainTextFormatter(); });
			});
			_logger = _factory.CreateLogger("MVL");
			Tools.SceneTree.Root.TreeExiting += OnRootOnTreeExiting;
		} catch (Exception e) {
			GD.PrintErr(e);
		}
	}

	static private void OnRootOnTreeExiting() {
		_factory?.Dispose();
		_factory = null;
		_logger = null;
	}

	static private void LogRotator() {
		try {
			if (!Directory.Exists(Paths.LogFolder)) {
				Directory.CreateDirectory(Paths.LogFolder);
			}

			if (File.Exists(LogPath)) {
				var logFile = new FileInfo(LogPath);
				var newPath = Paths.LogFolder.PathJoin($"{LogName}{logFile.CreationTime:yyyy-MM-ddTHH.mm.ss}.log");
				File.Move(LogPath, newPath);
			}

			var backups = Directory.GetFileSystemEntries(Paths.LogFolder, $"{LogName}*.log")
				.Select(path => new FileInfo(path))
				.OrderByDescending(f => f.CreationTime).Skip(5);
			foreach (var backup in backups) {
				backup.Delete();
			}
		} catch (Exception e) {
			GD.PrintErr(e);
		}
	}

	static Log() {
		LogRotator();
		Init();
	}
}