#!/bin/sh
set -eu

if [ "$(id -u)" -ne 0 ]; then
    printf '%s\n' 'dungeonstory-wiki-deploy must run as root.' >&2
    exit 1
fi
if [ "$#" -ne 1 ]; then
    printf '%s\n' 'usage: dungeonstory-wiki-deploy <release-id>' >&2
    exit 1
fi

release_id=$1
if ! printf '%s' "$release_id" | grep -Eq '^0\.0\.1v-[0-9]{8}T[0-9]{6}Z-[0-9a-f]{12}-[0-9a-f]{12}$'; then
    printf 'Invalid release id: %s\n' "$release_id" >&2
    exit 1
fi

owner=creeper0809
site_origin=https://creeper0809.synology.me
incoming="/volume1/homes/$owner/wiki-releases/$release_id"
deploy_parent=/volume1/wiki-deploy/releases
content_parent=/volume1/wiki-content/releases
deploy_release="$deploy_parent/$release_id"
content_release="$content_parent/$release_id"
docker=/var/packages/Docker/target/usr/bin/docker
image="dungeonstory-wiki:$release_id"
container="dungeonstory-wiki-$release_id"

for path in \
    "$incoming/content.zip" \
    "$incoming/renderer-context.zip" \
    "$incoming/release-manifest.json" \
    "$incoming/SHA256SUMS" \
    "$docker"
do
    if [ ! -e "$path" ]; then
        printf 'Required deployment input is missing: %s\n' "$path" >&2
        exit 1
    fi
done

if [ -e "$deploy_release" ] || [ -e "$content_release" ]; then
    printf 'Release is already staged: %s\n' "$release_id" >&2
    exit 1
fi
if "$docker" container inspect "$container" >/dev/null 2>&1; then
    printf 'Release container already exists: %s\n' "$container" >&2
    exit 1
fi

cd "$incoming"
tr -d '\r' < SHA256SUMS | sha256sum -c -

validate_zip() {
    archive_path=$1
    expected_root=$2
    /usr/bin/python3 - "$archive_path" "$expected_root" <<'PY'
import sys
import zipfile
from pathlib import PurePosixPath

archive_path, expected_root = sys.argv[1:]
with zipfile.ZipFile(archive_path) as archive:
    names = archive.namelist()
    if not names:
        raise SystemExit(f"Archive is empty: {archive_path}")
    for raw_name in names:
        name = raw_name.replace("\\", "/")
        path = PurePosixPath(name)
        if name.startswith("/") or ".." in path.parts or not path.parts or path.parts[0] != expected_root:
            raise SystemExit(f"Unsafe archive entry: {raw_name}")
PY
}

validate_zip "$incoming/content.zip" game-versions
validate_zip "$incoming/renderer-context.zip" wiki

mkdir -p "$deploy_parent" "$content_parent" /volume1/wiki-deploy
deploy_stage=$(mktemp -d "/volume1/wiki-deploy/.renderer-$release_id.XXXXXX")
content_stage=$(mktemp -d "/volume1/wiki-content/.content-$release_id.XXXXXX")
cleanup_staging() {
    if [ -n "${deploy_stage:-}" ] && [ -d "$deploy_stage" ]; then
        rm -rf -- "$deploy_stage"
    fi
    if [ -n "${content_stage:-}" ] && [ -d "$content_stage" ]; then
        rm -rf -- "$content_stage"
    fi
}
trap cleanup_staging EXIT HUP INT TERM

/usr/bin/7z x -y -bd -o"$deploy_stage" "$incoming/renderer-context.zip" >/dev/null
/usr/bin/7z x -y -bd -o"$content_stage" "$incoming/content.zip" >/dev/null

for path in \
    "$deploy_stage/wiki/Dockerfile" \
    "$deploy_stage/wiki/package-lock.json" \
    "$deploy_stage/wiki/game-versions/0.0.1v/game-version.json" \
    "$content_stage/game-versions/registry.json" \
    "$content_stage/game-versions/0.0.1v/game-version.json"
do
    if [ ! -f "$path" ]; then
        printf 'Extracted release is missing: %s\n' "$path" >&2
        exit 1
    fi
done

chown -R root:root "$deploy_stage" "$content_stage"
chmod -R go-w "$deploy_stage" "$content_stage"
mv "$deploy_stage" "$deploy_release"
mv "$content_stage" "$content_release"
deploy_stage=
content_stage=

"$docker" build \
    --build-arg "DUNGEONSTORY_WIKI_SITE_URL=$site_origin" \
    --file "$deploy_release/wiki/Dockerfile" \
    --tag "$image" \
    "$deploy_release"

previous_containers=$("$docker" ps -q --filter 'label=com.dungeonstory.wiki=true')
previous_count=$(printf '%s\n' "$previous_containers" | sed '/^$/d' | wc -l | tr -d ' ')
if [ "$previous_count" -gt 1 ]; then
    printf '%s\n' 'More than one DungeonStory wiki container is running; refusing an ambiguous handover.' >&2
    exit 1
fi
previous_container=$(printf '%s\n' "$previous_containers" | sed '/^$/d' | head -1)

restore_previous() {
    if [ -n "$previous_container" ]; then
        "$docker" start "$previous_container" >/dev/null 2>&1 || true
    fi
}

if [ -n "$previous_container" ]; then
    "$docker" stop --time 20 "$previous_container" >/dev/null
fi

if ! "$docker" run -d \
    --name "$container" \
    --label com.dungeonstory.wiki=true \
    --label "com.dungeonstory.release=$release_id" \
    --restart unless-stopped \
    --init \
    --read-only \
    --security-opt no-new-privileges:true \
    --cap-drop ALL \
    --memory 384m \
    --publish 127.0.0.1:4321:4321 \
    --volume "$content_release/game-versions:/app/game-versions:ro" \
    --tmpfs /tmp \
    "$image" >/dev/null
then
    restore_previous
    exit 1
fi

ready=no
attempt=0
while [ "$attempt" -lt 30 ]; do
    attempt=$((attempt + 1))
    if curl --fail --silent --show-error --max-time 5 \
        --header 'Host: creeper0809.synology.me' \
        http://127.0.0.1:4321/ >/tmp/dungeonstory-wiki-home.html
    then
        ready=yes
        break
    fi
    sleep 1
done

verify_route() {
    route=$1
    marker=$2
    output=$3
    curl --fail --silent --show-error --max-time 10 \
        --header 'Host: creeper0809.synology.me' \
        "http://127.0.0.1:4321$route" >"$output"
    grep -q "$marker" "$output"
}

if [ "$ready" != yes ] \
    || ! verify_route /guide/residents-and-work/ '스킬 경험과 감소' /tmp/dungeonstory-wiki-residents.html \
    || ! verify_route /guide/combat-and-equipment/ '장비 소재와 품질' /tmp/dungeonstory-wiki-combat.html \
    || ! verify_route /game-versions/0.0.1v/guide/residents-and-work/ '스킬 경험과 감소' /tmp/dungeonstory-wiki-archive.html
then
    "$docker" logs --tail 80 "$container" >&2 || true
    "$docker" stop --time 10 "$container" >/dev/null 2>&1 || true
    restore_previous
    exit 1
fi

printf '%s\n' "$release_id" >/volume1/wiki-deploy/current-release
if [ -n "$previous_container" ]; then
    printf '%s\n' "$previous_container" >/volume1/wiki-deploy/previous-container
else
    : >/volume1/wiki-deploy/previous-container
fi

printf 'DEPLOYED_RELEASE=%s\n' "$release_id"
printf 'CONTAINER=%s\n' "$container"
printf '%s\n' 'LOOPBACK_SMOKE=passed'
