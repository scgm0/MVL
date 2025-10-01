using System;
using System.Buffers;

namespace MVL.Utils;

public static class Fuzzy {
	/// <summary>
	/// 定义一个阈值，决定何时使用 stackalloc。
	/// 长度小于此值的字符串将使用栈分配，以获得最佳性能。
	/// 这个值可以根据目标平台的栈大小进行调整，256 通常是一个安全且高效的选择。
	/// </summary>
	private const int StackAllocThreshold = 256;

	/// <summary>
	/// 计算两个 ReadOnlySpan&lt;char&gt; 之间的高度优化的模糊匹配比率。
	/// </summary>
	/// <param name="s1">第一个文本片段。</param>
	/// <param name="s2">第二个文本片段。</param>
	/// <returns>返回一个 0 到 100 之间的整数，表示两个文本片段的相似度。</returns>
	public static int Ratio(ReadOnlySpan<char> s1, ReadOnlySpan<char> s2) {
		// 快速路径1：如果任一为空，相似度为 0
		if (s1.IsEmpty || s2.IsEmpty) {
			return 0;
		}

		// 快速路径2：如果两者完全相同，相似度为 100
		if (s1.SequenceEqual(s2)) {
			return 100;
		}

		var distance = LevenshteinDistance(s1, s2);

		var similarity = (double)(s1.Length + s2.Length - distance) / (s1.Length + s2.Length);

		return (int)Math.Round(similarity * 100);
	}

	/// <summary>
	/// 在长字符串中查找与短字符串最匹配的子串，并返回它们的相似度比率。
	/// </summary>
	/// <param name="s1">第一个文本片段。</param>
	/// <param name="s2">第二个文本片段。</param>
	/// <returns>返回一个 0 到 100 之间的整数，表示最佳局部匹配的相似度。</returns>
	public static int PartialRatio(ReadOnlySpan<char> s1, ReadOnlySpan<char> s2) {
		if (s1.IsEmpty || s2.IsEmpty) {
			return 0;
		}

		var shorter = s1.Length < s2.Length ? s1 : s2;
		var longer = s1.Length < s2.Length ? s2 : s1;

		var maxScore = 0;
		var shorterLength = shorter.Length;
		var longerLength = longer.Length;

		// 在长字符串上滑动一个与短字符串等长的窗口
		for (var i = 0; i <= longerLength - shorterLength; i++) {
			var slice = longer.Slice(i, shorterLength);

			// --- 这是关键的修改 ---
			// 直接计算 Levenshtein 距离，并使用更适合等长比较的公式
			var distance = LevenshteinDistance(shorter, slice);

			// 如果完全匹配，距离为0，相似度为1.0
			// 如果完全不匹配，距离为 shorterLength, 相似度为0.0
			var similarity = (double)(shorterLength - distance) / shorterLength;
			var currentScore = (int)Math.Round(similarity * 100);

			if (currentScore > maxScore) {
				maxScore = currentScore;
			}

			// 优化：如果已经找到了完美匹配，就没有必要继续搜索了
			if (maxScore == 100) {
				return 100;
			}
		}

		return maxScore;
	}

	/// <summary>
	/// 计算 Levenshtein 距离，内部使用 stackalloc 和 ArrayPool 进行内存优化。
	/// </summary>
	static private int LevenshteinDistance(ReadOnlySpan<char> s1, ReadOnlySpan<char> s2) {
		// 确保 s2 是较短的 Span，以优化内存使用
		if (s1.Length < s2.Length) {
			var temp = s1;
			s1 = s2;
			s2 = temp;
		}

		var len1 = s1.Length;
		var len2 = s2.Length;

		switch (len2) {
			case 0: return len1;
			// 根据较短字符串的长度决定使用栈分配还是数组池
			case < StackAllocThreshold: {
				// 在栈上分配内存，零 GC 压力
				Span<int> v0 = stackalloc int[len2 + 1];
				Span<int> v1 = stackalloc int[len2 + 1];
				return CalculateDistance(s1, s2, v0, v1);
			}
			default: {
				// 从数组池租用数组，减少 GC 压力
				var v0Array = ArrayPool<int>.Shared.Rent(len2 + 1);
				var v1Array = ArrayPool<int>.Shared.Rent(len2 + 1);

				try {
					return CalculateDistance(s1, s2, v0Array.AsSpan(0, len2 + 1), v1Array.AsSpan(0, len2 + 1));
				} finally {
					// 确保无论如何都将数组归还给池
					ArrayPool<int>.Shared.Return(v0Array);
					ArrayPool<int>.Shared.Return(v1Array);
				}
			}
		}
	}

	/// <summary>
	/// 实际执行 Levenshtein 距离计算的核心逻辑。
	/// </summary>
	static private int CalculateDistance(ReadOnlySpan<char> s1, ReadOnlySpan<char> s2, Span<int> v0, Span<int> v1) {
		var len1 = s1.Length;
		var len2 = s2.Length;

		// 初始化 v0 (第一行)
		for (var i = 0; i <= len2; i++) {
			v0[i] = i;
		}

		for (var i = 0; i < len1; i++) {
			v1[0] = i + 1;

			for (var j = 0; j < len2; j++) {
				var cost = s1[i] == s2[j] ? 0 : 1;
				v1[j + 1] = Math.Min(v1[j] + 1, // Deletion
					Math.Min(v0[j + 1] + 1, // Insertion
						v0[j] + cost)); // Substitution
			}

			// 交换 v0 和 v1 以进行下一轮迭代
			var temp = v0;
			v0 = v1;
			v1 = temp;
		}

		// 经过最后一次循环后，结果在 v0 中（因为我们交换了）
		return v0[len2];
	}
}