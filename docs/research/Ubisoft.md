# Ubisoft motion-matching research references

## Classical introduction (2016)

- Simon Clavet, **Motion Matching and The Road to Next-Gen Animation**, Ubisoft Montreal, GDC 2016.
- Slides: https://ubm-twvideo01.s3.amazonaws.com/o1/vault/gdc2016/Presentations/Clavet_Simon_MotionMatching.pdf
- Session page: https://www.gdcvault.com/play/1023280/
- Foundational concepts: match selected feet pose/velocity features with requested future trajectory; minimize cost across candidate frames; author procedural correction/warping for gameplay; optimize search (the presentation explicitly mentions KD-tree).
- It is a **talk and slide deck**, not the 2020 learned paper.

## Learned motion matching (2020)

- Daniel Holden, Oussama Kanoun, Maksym Perepichka and Tiberiu Popa. **Learned Motion Matching**, ACM Transactions on Graphics 39(4), 2020. DOI https://doi.org/10.1145/3386569.3392440
- Official full PDF: https://static-wordpress.ubisoft.com/montreal.ubisoft.com/wp-content/uploads/2020/07/09154101/Learned_Motion_Matching.pdf
- Ubisoft summary: https://www.ubisoft.com/en-us/studio/laforge/news/3NWqwcMU9lMfumgfPAEfka/learned-motion-matching

### Classical baseline explained in paper

The locomotion example uses a **27-value feature vector**: 2D future positions at 20/40/60 frames, matching future 2D facing vectors, 3D positions and 3D velocities of two feet, and 3D hip velocity (60 Hz reference). Search minimizes distance in feature space after standard-deviation scaling.

### Learned decomposition

- **Projection**: nearest-neighbor result in matching-feature space; learned **Projector** predicts a matched feature/latent representation.
- **Stepping**: advance from a current motion frame to the next; learned **Stepper** advances latent and feature state.
- **Decompression**: retrieve the full animation pose; learned **Compressor/Decompressor** encodes and reconstructs pose data.

Important: replacing only nearest-neighbor search with an MLP is **not** the complete paper. Don't claim memory benefits until the matching and pose databases are actually eliminated or reduced.

## Experimental methodology

- Maintain a frozen gold set of input queries with labels from exact brute-force search.
- Split sequences to prevent adjacent-frame train/test leakage.
- Use the same root-space features and exported normalization metadata across Python and Unity.
- Report output fidelity, motion continuity, query latency (p50/p95), memory and model load costs.
- Note that paper benchmark numbers were from its own data/hardware and should **not** be promised for this Unity implementation.

## Runtime integration reference

- Unity Sentis (`com.unity.ai.inference`) 2.6 overview: https://docs.unity.cn/Packages/com.unity.ai.inference%402.6/manual/index.html
- On modern 2.6 API the namespace is `Unity.InferenceEngine`. Confirm exact installed package in Unity and validate ONNX operators, tensor lifetime and supported compute backends before code migration.
