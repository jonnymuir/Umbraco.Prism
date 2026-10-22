#!/usr/bin/env node
// Revokes the Apple Development certificates in this machine's keychain through the App Store
// Connect API. `xcodebuild archive -allowProvisioningUpdates` creates a new one on every fresh CI
// runner, and the account refuses to archive once it holds its limit of them. Run it only on a
// throwaway CI runner: on a developer's Mac it would revoke their own certificate.
//
// Needs APP_STORE_CONNECT_KEY_ID, APP_STORE_CONNECT_ISSUER_ID and the key's .p8 at
// ~/.appstoreconnect/private_keys/AuthKey_<key id>.p8, as the TestFlight workflow writes it.
import { execFileSync } from 'node:child_process';
import { X509Certificate, sign } from 'node:crypto';
import { readFileSync } from 'node:fs';
import { homedir } from 'node:os';
import { join } from 'node:path';

const api = 'https://api.appstoreconnect.apple.com/v1';
const keyId = required('APP_STORE_CONNECT_KEY_ID');
const issuerId = required('APP_STORE_CONNECT_ISSUER_ID');
const privateKey = readFileSync(join(homedir(), '.appstoreconnect', 'private_keys', `AuthKey_${keyId}.p8`), 'utf8');

const local = keychainSerials();
if (local.size === 0) {
  console.log('No Apple Development certificate in the keychain, so nothing to revoke.');
  process.exit(0);
}

const token = bearerToken();
const listed = await call('GET', '/certificates?filter[certificateType]=DEVELOPMENT,IOS_DEVELOPMENT&limit=200');
const created = listed.data.filter((certificate) => local.has(normalise(certificate.attributes.serialNumber)));
for (const certificate of created) {
  await call('DELETE', `/certificates/${certificate.id}`);
  console.log(`Revoked ${certificate.attributes.name} (serial ${certificate.attributes.serialNumber}).`);
}
if (created.length < local.size) {
  console.log(`${local.size - created.length} keychain certificate(s) were not found in the account, so were left alone.`);
}

function required(name) {
  const value = process.env[name];
  if (!value) throw new Error(`${name} is not set.`);
  return value;
}

function keychainSerials() {
  let pems;
  try {
    pems = execFileSync('security', ['find-certificate', '-a', '-c', 'Apple Development', '-p'], { encoding: 'utf8' });
  } catch {
    return new Set();
  }
  const blocks = pems.match(/-----BEGIN CERTIFICATE-----[\s\S]+?-----END CERTIFICATE-----/g) ?? [];
  return new Set(blocks.map((pem) => normalise(new X509Certificate(pem).serialNumber)));
}

function normalise(serial) {
  return serial.toUpperCase().replace(/^0+/, '');
}

function bearerToken() {
  const now = Math.floor(Date.now() / 1000);
  const encode = (part) => Buffer.from(JSON.stringify(part)).toString('base64url');
  const unsigned = `${encode({ alg: 'ES256', kid: keyId, typ: 'JWT' })}.${encode({ iss: issuerId, iat: now, exp: now + 600, aud: 'appstoreconnect-v1' })}`;
  const signature = sign('sha256', Buffer.from(unsigned), { key: privateKey, dsaEncoding: 'ieee-p1363' });
  return `${unsigned}.${signature.toString('base64url')}`;
}

async function call(method, path) {
  const response = await fetch(`${api}${path}`, { method, headers: { Authorization: `Bearer ${token}` } });
  if (!response.ok) throw new Error(`${method} ${path} answered ${response.status}: ${await response.text()}`);
  return response.status === 204 ? null : response.json();
}
