#!/bin/sh
set -eu
cd "$(dirname "$0")/../video/preview"
npm ci
python3 ../../source/prepare-preview.py
npx --yes hyperframes@0.8.30 check --snapshots --at 0,1,5.5,7,14 --json > ../../source/preview-check.json
npx --yes hyperframes@0.8.30 render --quality high --fps 30 --video-bitrate 11M --workers 2 --strict --output ../ring-rush-render.mp4
# Exact App Store deliverable: progressive High Profile Level 4.0; 11 Mbps CBR;
# AAC stereo 256 kbps / 48 kHz; web-friendly moov atom; precisely 15 seconds.
ffmpeg -y -v error -i ../ring-rush-render.mp4 -t 15 -c:v libx264 -preset slow -profile:v high -level:v 4.0 -pix_fmt yuv420p -r 30 -b:v 11M -minrate 11M -maxrate 11M -bufsize 22M -x264-params nal-hrd=cbr:force-cfr=1 -g 30 -c:a aac -b:a 256k -ar 48000 -ac 2 -movflags +faststart ../ring-rush-preview.mp4
ffmpeg -y -v error -ss 1 -i ../ring-rush-preview.mp4 -frames:v 1 ../poster-frame.png
