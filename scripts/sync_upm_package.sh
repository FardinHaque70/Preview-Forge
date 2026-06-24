#!/usr/bin/env bash

set -euo pipefail

script_dir="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
repo_root="$(cd "${script_dir}/.." && pwd)"

asset_root="${repo_root}/Assets/Noodle Hammer/Preview Forge"
asset_docs_root="${asset_root}/Documentation"
upm_root="${repo_root}/upm/src/Noodle Hammer/Preview Forge"
upm_package_root="${repo_root}/upm/src"

if [[ ! -d "${asset_root}" ]]; then
  echo "Missing asset source root: ${asset_root}" >&2
  exit 1
fi

if [[ ! -d "${upm_root}" ]]; then
  echo "Missing UPM mirror root: ${upm_root}" >&2
  exit 1
fi

rsync -ac --delete \
  --exclude '.DS_Store' \
  --exclude 'Demo/' \
  --exclude 'Demo.meta' \
  --exclude 'Documentation/' \
  --exclude 'Documentation.meta' \
  --exclude 'Settings/' \
  --exclude 'Settings.meta' \
  "${asset_root}/" "${upm_root}/"

for file_name in README.md README.md.meta CHANGELOG.md CHANGELOG.md.meta THIRD_PARTY_NOTICES.md THIRD_PARTY_NOTICES.md.meta; do
  cp "${asset_docs_root}/${file_name}" "${upm_package_root}/${file_name}"
done

echo "UPM mirror synced from Assets/Noodle Hammer/Preview Forge"
