#!/usr/bin/env bash

set -Eeuo pipefail

readonly DEPLOY_DIR="${DEPLOY_DIR:-/home/nexus-deploy/openhealthmcp}"
readonly COMPOSE_FILE="${DEPLOY_DIR}/compose.production.yml"
readonly ENV_FILE="${DEPLOY_DIR}/.env"
readonly HEALTH_URL="http://openhealthmcp:8080/health"
readonly HEALTH_ATTEMPTS=30
readonly HEALTH_DELAY_SECONDS=4

if [[ $# -ne 1 ]]; then
    echo "Usage: $0 <ghcr-image-with-sha-tag>" >&2
    exit 2
fi

readonly NEW_IMAGE="$1"

if [[ ! "${NEW_IMAGE}" =~ ^ghcr\.io/xaxee/open-health-mcp:sha-[0-9a-f]{40}$ ]]; then
    echo "Refusing to deploy an image without the expected immutable SHA tag." >&2
    exit 2
fi

if [[ ! -f "${COMPOSE_FILE}" ]]; then
    echo "Missing Compose file: ${COMPOSE_FILE}" >&2
    exit 1
fi

if [[ ! -f "${ENV_FILE}" ]]; then
    echo "Missing environment file: ${ENV_FILE}" >&2
    exit 1
fi

if ! docker network inspect proxy >/dev/null 2>&1; then
    echo "Required external Docker network 'proxy' does not exist." >&2
    exit 1
fi

cd "${DEPLOY_DIR}"

current_image="$({ sed -n 's/^OPENHEALTHMCP_IMAGE=//p' "${ENV_FILE}" || true; } | tail -n 1)"
previous_image_available=false
if [[ "${current_image}" =~ ^ghcr\.io/xaxee/open-health-mcp:sha-[0-9a-f]{40}$ ]] \
    && docker image inspect "${current_image}" >/dev/null 2>&1; then
    previous_image_available=true
fi

env_backup="$(mktemp "${DEPLOY_DIR}/.env.backup.XXXXXX")"
cp "${ENV_FILE}" "${env_backup}"
chmod 600 "${env_backup}"

cleanup() {
    rm -f "${env_backup}"
}

trap cleanup EXIT

set_image() {
    local image="$1"
    local temporary_file
    temporary_file="$(mktemp "${DEPLOY_DIR}/.env.tmp.XXXXXX")"

    awk -v image="${image}" '
        BEGIN { replaced = 0 }
        /^OPENHEALTHMCP_IMAGE=/ {
            if (!replaced) {
                print "OPENHEALTHMCP_IMAGE=" image
                replaced = 1
            }
            next
        }
        { print }
        END {
            if (!replaced) {
                print "OPENHEALTHMCP_IMAGE=" image
            }
        }
    ' "${ENV_FILE}" > "${temporary_file}"

    chmod 600 "${temporary_file}"
    mv "${temporary_file}" "${ENV_FILE}"
}

wait_for_health() {
    local attempt

    for ((attempt = 1; attempt <= HEALTH_ATTEMPTS; attempt++)); do
        if docker run --rm --network proxy busybox:1.36 \
            wget --quiet --output-document=- --timeout=5 "${HEALTH_URL}" \
            | grep --quiet '"status":"healthy"'; then
            echo "Health check passed on attempt ${attempt}."
            return 0
        fi

        echo "Health check attempt ${attempt}/${HEALTH_ATTEMPTS} failed; retrying..."
        sleep "${HEALTH_DELAY_SECONDS}"
    done

    return 1
}

rollback() {
    if [[ "${previous_image_available}" != true || "${current_image}" == "${NEW_IMAGE}" ]]; then
        echo "No distinct previous image is available for automatic rollback." >&2
        return 1
    fi

    echo "Rolling back to the previous immutable image."
    set_image "${current_image}"
    docker compose --project-name openhealthmcp --file "${COMPOSE_FILE}" pull openhealthmcp
    docker compose --project-name openhealthmcp --file "${COMPOSE_FILE}" up --detach --remove-orphans
    wait_for_health
}

echo "Deploying ${NEW_IMAGE}."
set_image "${NEW_IMAGE}"

if ! docker compose --project-name openhealthmcp --file "${COMPOSE_FILE}" pull openhealthmcp; then
    cp "${env_backup}" "${ENV_FILE}"
    chmod 600 "${ENV_FILE}"
    echo "Image pull failed; the previous environment configuration was restored." >&2
    exit 1
fi

if ! docker compose --project-name openhealthmcp --file "${COMPOSE_FILE}" up --detach --remove-orphans; then
    echo "Docker Compose failed to start the deployment." >&2
    rollback || true
    exit 1
fi

if ! wait_for_health; then
    echo "Deployment health check failed." >&2
    docker compose --project-name openhealthmcp --file "${COMPOSE_FILE}" logs --tail 100 openhealthmcp >&2
    rollback || true
    exit 1
fi

docker compose --project-name openhealthmcp --file "${COMPOSE_FILE}" ps
echo "Deployment completed successfully."