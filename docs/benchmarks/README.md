# Motion matching benchmark protocol

Compare V1 brute force, V2 KD-tree and V3/V4 neural approximations on the **same** versioned dataset and frozen query suite.

Record: code commit, Unity editor, CPU/GPU, OS, dataset and schema hashes, number of frames, number of eligible frames, 27D feature scaling/weights, warm-up count, number of queries, median/p95/p99 latency, allocations, index build time, memory, correctness/recall, and visual artifacts.

For KD-tree exact mode, candidate choice must match brute force under documented ties and filters; if approximate, report recall@k and comparison-cost quality separately. For neural inference, include model cold-start, steady-state, ONNX file bytes, tensor memory and Python-to-Unity numerical parity.

Do not publish performance superiority based on editor-only profiler screenshots or one input sequence; create representative stops, starts, inversions, turns, idle and collision cases.
