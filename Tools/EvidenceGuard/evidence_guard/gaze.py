"""Duration-weighted eye evidence with explicit missing coverage and exclusive ROIs."""


def summarize(samples, intervals, max_sample_seconds=.1):
    # Merge requested intervals; never count one sample twice for overlapping windows.
    merged = []
    for start, end in sorted(intervals):
        if end <= start:
            continue
        if merged and start <= merged[-1][1]:
            merged[-1][1] = max(end, merged[-1][1])
        else:
            merged.append([start, end])
    total = sum(b - a for a, b in merged)
    valid = task = movie = covered = 0.
    previous_end = float("-inf")
    for s in sorted(samples, key=lambda x: (x.start, x.id)):
        a, b = max(s.start, previous_end), min(s.end, s.start + max_sample_seconds)
        if b <= a:
            continue
        previous_end = b
        duration = sum(max(0., min(b, end) - max(a, start)) for start, end in merged)
        covered += duration
        if not s.quality:
            continue
        valid += duration
        target = s.data.get("target", "other")
        if target == "task" and s.data.get("task_visible", False):
            task += duration
        elif target == "movie" and s.data.get("movie_visible", False):
            movie += duration
    return {"gaze_valid_ratio": valid / total if total else None,
            "task_gaze_ratio": task / valid if valid else None,
            "movie_gaze_ratio": movie / valid if valid else None,
            "duration": total, "covered_seconds": covered, "valid_seconds": valid,
            "task_seconds": task, "movie_seconds": movie,
            "available": covered > 0 and valid > 0}
