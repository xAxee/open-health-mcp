#!/usr/bin/env bash

set -Eeuo pipefail

readonly BASE_URL="${BASE_URL:-http://localhost:8080}"
readonly OAUTH_RESOURCE="${OAUTH_RESOURCE:-https://health.hubertiwan.pl/mcp}"
readonly OAUTH_ISSUER="${OAUTH_RESOURCE%/mcp}"
readonly OWNER_PASSWORD="${OAUTH_OWNER_PASSWORD:?OAUTH_OWNER_PASSWORD is required}"
readonly STATIC_TOKEN="${MCP_AUTH_TOKEN:?MCP_AUTH_TOKEN is required}"
readonly CALLBACK_URL="${CALLBACK_URL:-https://chatgpt.com/connector_platform_oauth_redirect}"
TEMP_DIR="$(mktemp -d)"
readonly TEMP_DIR

cleanup() {
    rm -rf "${TEMP_DIR}"
}

trap cleanup EXIT

fail() {
    echo "OAuth flow test failed: $1" >&2
    exit 1
}

wait_for_health() {
    for attempt in {1..30}; do
        if curl --fail --silent --show-error --max-time 5 "${BASE_URL}/health" \
            | grep -q '"status":"healthy"'; then
            return 0
        fi

        echo "Health check ${attempt}/30 failed; retrying..."
        sleep 2
    done

    fail "application did not become healthy"
}

json_value() {
    local file="$1"
    local key="$2"
    python3 -c 'import json,sys; print(json.load(open(sys.argv[1], encoding="utf-8"))[sys.argv[2]])' \
        "${file}" "${key}"
}

form_encode() {
    python3 -c 'import sys,urllib.parse; print(urllib.parse.urlencode(dict(arg.split("=", 1) for arg in sys.argv[1:])))' "$@"
}

expect_status() {
    local expected="$1"
    local actual="$2"
    local description="$3"
    [[ "${actual}" == "${expected}" ]] || fail "${description}: expected ${expected}, received ${actual}"
}

wait_for_health

invalid_registration_status=$(curl --silent --show-error --output /dev/null --write-out '%{http_code}' \
    --request POST "${BASE_URL}/oauth/register" \
    --header 'Content-Type: application/json' \
    --data '{"client_name":"Invalid OAuth client","redirect_uris":["http://attacker.example/callback"],"token_endpoint_auth_method":"none"}')
expect_status 400 "${invalid_registration_status}" "unsafe redirect URI registration"

resource_metadata="${TEMP_DIR}/resource-metadata.json"
curl --fail --silent --show-error "${BASE_URL}/.well-known/oauth-protected-resource/mcp" \
    --output "${resource_metadata}"
[[ "$(json_value "${resource_metadata}" resource)" == "${OAUTH_RESOURCE}" ]] \
    || fail "protected resource metadata has the wrong resource"

authorization_metadata="${TEMP_DIR}/authorization-metadata.json"
curl --fail --silent --show-error "${BASE_URL}/.well-known/oauth-authorization-server" \
    --output "${authorization_metadata}"
[[ "$(json_value "${authorization_metadata}" issuer)" == "${OAUTH_ISSUER}" ]] \
    || fail "authorization server metadata has the wrong issuer"

challenge_headers="${TEMP_DIR}/challenge.headers"
challenge_status=$(curl --silent --show-error --output /dev/null --dump-header "${challenge_headers}" \
    --write-out '%{http_code}' --request POST "${BASE_URL}/mcp" \
    --header 'Content-Type: application/json' \
    --data '{}')
expect_status 401 "${challenge_status}" "unauthenticated MCP request"
grep -Fqi "resource_metadata=\"${OAUTH_ISSUER}/.well-known/oauth-protected-resource/mcp\"" \
    "${challenge_headers}" || fail "MCP challenge does not advertise protected resource metadata"

registration_response="${TEMP_DIR}/registration.json"
registration_status=$(curl --silent --show-error --output "${registration_response}" --write-out '%{http_code}' \
    --request POST "${BASE_URL}/oauth/register" \
    --header 'Content-Type: application/json' \
    --data "{\"client_name\":\"OAuth integration test\",\"redirect_uris\":[\"${CALLBACK_URL}\"],\"grant_types\":[\"authorization_code\",\"refresh_token\"],\"response_types\":[\"code\"],\"token_endpoint_auth_method\":\"none\"}")
expect_status 201 "${registration_status}" "dynamic client registration"
client_id="$(json_value "${registration_response}" client_id)"

code_verifier='0123456789abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ-._~'
code_challenge=$(printf '%s' "${code_verifier}" | openssl dgst -binary -sha256 | openssl base64 -A | tr '+/' '-_' | tr -d '=')
state='oauth-integration-test-state'

authorize_query=$(form_encode \
    'response_type=code' \
    "client_id=${client_id}" \
    "redirect_uri=${CALLBACK_URL}" \
    "state=${state}" \
    "code_challenge=${code_challenge}" \
    'code_challenge_method=S256' \
    'scope=health.read' \
    "resource=${OAUTH_RESOURCE}")

consent_page="${TEMP_DIR}/consent.html"
consent_status=$(curl --silent --show-error --output "${consent_page}" --write-out '%{http_code}' \
    "${BASE_URL}/oauth/authorize?${authorize_query}")
expect_status 200 "${consent_status}" "authorization page"
grep -q 'Authorize OpenHealthMCP' "${consent_page}" || fail "authorization page content is missing"

wrong_resource_query=$(form_encode \
    'response_type=code' \
    "client_id=${client_id}" \
    "redirect_uri=${CALLBACK_URL}" \
    "state=${state}" \
    "code_challenge=${code_challenge}" \
    'code_challenge_method=S256' \
    'scope=health.read' \
    'resource=https://attacker.example/mcp')
wrong_resource_status=$(curl --silent --show-error --output /dev/null --write-out '%{http_code}' \
    "${BASE_URL}/oauth/authorize?${wrong_resource_query}")
expect_status 400 "${wrong_resource_status}" "authorization for the wrong resource"

approval_headers="${TEMP_DIR}/approval.headers"
approval_status=$(curl --silent --show-error --output /dev/null --dump-header "${approval_headers}" \
    --write-out '%{http_code}' --request POST "${BASE_URL}/oauth/authorize" \
    --header 'Content-Type: application/x-www-form-urlencoded' \
    --data "${authorize_query}&decision=approve&owner_password=$(python3 -c 'import sys,urllib.parse; print(urllib.parse.quote(sys.argv[1], safe=""))' "${OWNER_PASSWORD}")")
expect_status 302 "${approval_status}" "authorization approval"
location=$(grep -i '^location:' "${approval_headers}" | tail -n 1 | tr -d '\r' | cut -d' ' -f2-)
code=$(python3 -c 'import sys,urllib.parse; print(urllib.parse.parse_qs(urllib.parse.urlsplit(sys.argv[1]).query)["code"][0])' "${location}")
returned_state=$(python3 -c 'import sys,urllib.parse; print(urllib.parse.parse_qs(urllib.parse.urlsplit(sys.argv[1]).query)["state"][0])' "${location}")
[[ "${returned_state}" == "${state}" ]] || fail "authorization response state does not match"

wrong_verifier_status=$(curl --silent --show-error --output /dev/null --write-out '%{http_code}' \
    --request POST "${BASE_URL}/oauth/token" \
    --header 'Content-Type: application/x-www-form-urlencoded' \
    --data-urlencode 'grant_type=authorization_code' \
    --data-urlencode "code=${code}" \
    --data-urlencode "client_id=${client_id}" \
    --data-urlencode "redirect_uri=${CALLBACK_URL}" \
    --data-urlencode 'code_verifier=this-is-an-intentionally-wrong-pkce-verifier-value-1234567890' \
    --data-urlencode "resource=${OAUTH_RESOURCE}")
expect_status 400 "${wrong_verifier_status}" "incorrect PKCE verifier"

token_response="${TEMP_DIR}/token.json"
token_status=$(curl --silent --show-error --output "${token_response}" --write-out '%{http_code}' \
    --request POST "${BASE_URL}/oauth/token" \
    --header 'Content-Type: application/x-www-form-urlencoded' \
    --data-urlencode 'grant_type=authorization_code' \
    --data-urlencode "code=${code}" \
    --data-urlencode "client_id=${client_id}" \
    --data-urlencode "redirect_uri=${CALLBACK_URL}" \
    --data-urlencode "code_verifier=${code_verifier}" \
    --data-urlencode "resource=${OAUTH_RESOURCE}")
expect_status 200 "${token_status}" "authorization code exchange"
access_token="$(json_value "${token_response}" access_token)"
refresh_token="$(json_value "${token_response}" refresh_token)"

reused_code_status=$(curl --silent --show-error --output /dev/null --write-out '%{http_code}' \
    --request POST "${BASE_URL}/oauth/token" \
    --header 'Content-Type: application/x-www-form-urlencoded' \
    --data-urlencode 'grant_type=authorization_code' \
    --data-urlencode "code=${code}" \
    --data-urlencode "client_id=${client_id}" \
    --data-urlencode "redirect_uri=${CALLBACK_URL}" \
    --data-urlencode "code_verifier=${code_verifier}" \
    --data-urlencode "resource=${OAUTH_RESOURCE}")
expect_status 400 "${reused_code_status}" "authorization code reuse"

mcp_status=$(curl --silent --show-error --output "${TEMP_DIR}/mcp-response" --write-out '%{http_code}' \
    --request POST "${BASE_URL}/mcp" \
    --header "Authorization: Bearer ${access_token}" \
    --header 'Content-Type: application/json' \
    --header 'Accept: application/json, text/event-stream' \
    --data '{"jsonrpc":"2.0","id":1,"method":"initialize","params":{"protocolVersion":"2025-06-18","capabilities":{},"clientInfo":{"name":"oauth-test","version":"1.0"}}}')
expect_status 200 "${mcp_status}" "OAuth-authenticated MCP initialize"
grep -q '"jsonrpc"' "${TEMP_DIR}/mcp-response" || fail "MCP response is not JSON-RPC"

admin_status=$(curl --silent --show-error --output /dev/null --write-out '%{http_code}' \
    --request POST "${BASE_URL}/admin/sync" \
    --header "Authorization: Bearer ${access_token}" \
    --header 'Content-Type: application/json' \
    --data '{}')
expect_status 403 "${admin_status}" "OAuth access to admin endpoint"

static_mcp_status=$(curl --silent --show-error --output /dev/null --write-out '%{http_code}' \
    --request POST "${BASE_URL}/mcp" \
    --header "Authorization: Bearer ${STATIC_TOKEN}" \
    --header 'Content-Type: application/json' \
    --header 'Accept: application/json, text/event-stream' \
    --data '{"jsonrpc":"2.0","id":2,"method":"initialize","params":{"protocolVersion":"2025-06-18","capabilities":{},"clientInfo":{"name":"static-test","version":"1.0"}}}')
expect_status 200 "${static_mcp_status}" "static bearer access to MCP"

refresh_response="${TEMP_DIR}/refresh.json"
refresh_status=$(curl --silent --show-error --output "${refresh_response}" --write-out '%{http_code}' \
    --request POST "${BASE_URL}/oauth/token" \
    --header 'Content-Type: application/x-www-form-urlencoded' \
    --data-urlencode 'grant_type=refresh_token' \
    --data-urlencode "refresh_token=${refresh_token}" \
    --data-urlencode "client_id=${client_id}" \
    --data-urlencode 'scope=health.read' \
    --data-urlencode "resource=${OAUTH_RESOURCE}")
expect_status 200 "${refresh_status}" "refresh token exchange"
new_access_token="$(json_value "${refresh_response}" access_token)"
[[ "${new_access_token}" != "${access_token}" ]] || fail "refresh did not rotate the access token"

reused_refresh_status=$(curl --silent --show-error --output /dev/null --write-out '%{http_code}' \
    --request POST "${BASE_URL}/oauth/token" \
    --header 'Content-Type: application/x-www-form-urlencoded' \
    --data-urlencode 'grant_type=refresh_token' \
    --data-urlencode "refresh_token=${refresh_token}" \
    --data-urlencode "client_id=${client_id}" \
    --data-urlencode 'scope=health.read' \
    --data-urlencode "resource=${OAUTH_RESOURCE}")
expect_status 400 "${reused_refresh_status}" "refresh token reuse"

echo "OAuth flow test passed."