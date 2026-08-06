using System;
using System.Threading;
using System.Threading.Tasks;

namespace MVL.Utils;

public class AsyncDebouncer(TimeSpan delayMilliseconds) {
	private CancellationTokenSource? _cts;
	public TimeSpan DelayMilliseconds { get; set; } = delayMilliseconds;

	public async Task DebounceAsync(Action action) {
		if (await WaitAndCheckCancellationAsync()) {
			action.Invoke();
		}
	}

	public async Task DebounceAsync(Func<Task> asyncAction) {
		if (await WaitAndCheckCancellationAsync()) {
			await asyncAction.Invoke();
		}
	}

	public void Cancel() {
		_cts?.Cancel();
		_cts?.Dispose();
		_cts = null;
	}

	private async Task<bool> WaitAndCheckCancellationAsync() {
		var cts = new CancellationTokenSource();
		var oldCts = Interlocked.Exchange(ref _cts, cts);

		if (oldCts != null) {
			await oldCts.CancelAsync();
			oldCts.Dispose();
		}

		try {
			await Task.Delay(DelayMilliseconds, cts.Token);

			return !cts.Token.IsCancellationRequested;
		} catch (TaskCanceledException) {
			return false;
		}
	}
}