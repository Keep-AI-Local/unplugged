#!/usr/bin/env bash
# Downloads Google's Gemma 3 1B (instruction-tuned, quantization-aware trained, Q4_0), as packaged
# in GGUF format by the llama.cpp project (ggml-org), and verifies it against the SHA-256 published
# on Hugging Face. ~720 MB. Gemma is provided under and subject to the Gemma Terms of Use
# (ai.google.dev/gemma/terms); NOTICE.txt and the Terms are in models/.
set -euo pipefail
cd "$(dirname "$0")/../models"
FILE=gemma-3-1b-it-qat-Q4_0.gguf
SHA=ef60e4e91a738c99ae9976b050657dfe68a4007a0ccca121b55ec0c413dccd58
URL="https://huggingface.co/ggml-org/gemma-3-1b-it-qat-GGUF/resolve/main/$FILE"
[ -f "$FILE" ] || curl -L --fail -C - -o "$FILE" "$URL"
echo "$SHA  $FILE" | sha256sum -c -
