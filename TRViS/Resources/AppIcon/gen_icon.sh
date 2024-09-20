#!/bin/sh

cd $(dirname $0)

# csprojと同じ階層に移動
cd ../../

function generate_icon() {
  size=$1
  size_name=$2
  appearance=$3
  rsvg-convert \
    -h $size \
    -w $size \
    -o 'Platforms/iOS/CustomAssets.xcassets/AppIcon.appiconset/appicon.'$size_name'.'$appearance'.png' \
    'Resources/AppIcon/appicon.'$appearance'.svg'
}

function generate_icon_three_appearances() {
  size=$1
  size_name=$2
  generate_icon $size $size_name any
  generate_icon $size $size_name dark
  generate_icon $size $size_name tint
}

function generate_icon_two_size() {
  base_size=$1
  x2_size=$(($base_size * 2))
  x3_size=$(($base_size * 3))
  generate_icon_three_appearances $x2_size $base_size@2x
  generate_icon_three_appearances $x3_size $base_size@3x
}

generate_icon_two_size 20
generate_icon_two_size 29
generate_icon_two_size 38
generate_icon_two_size 40
generate_icon_two_size 60
generate_icon_two_size 64

generate_icon_three_appearances $((68 * 2)) 68@2x
generate_icon_three_appearances $((76 * 2)) 76@2x
generate_icon_three_appearances 167 83.5@2x
