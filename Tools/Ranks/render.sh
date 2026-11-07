#!/bin/zsh
set -e
here="${0:A:h}"
root="${here:h:h}"
svg="$here/svg"
out="$root/Assets/Textures/Resources/ui/ranks"
mkdir -p "$out"
python3 -I "$here/make_badges.py" "$svg"
swift "$here/render.swift" "$svg" "$out" 512 rank_
