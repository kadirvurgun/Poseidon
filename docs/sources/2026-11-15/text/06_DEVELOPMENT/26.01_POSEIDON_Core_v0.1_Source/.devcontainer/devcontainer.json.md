# devcontainer.json

Source: analiz/POSEIDON GUNCEL 30.10.2026/06_DEVELOPMENT/26.01_POSEIDON_Core_v0.1_Source/.devcontainer/devcontainer.json
SHA256: 223734938f4f0f6741595a6401e8e6ff22d057f7fd1550819f265d1ef5aa726a

## lines:1-11

{
  "name": "Poseidon synthetic private UAT",
  "build": { "dockerfile": "Dockerfile" },
  "remoteUser": "postgres",
  "overrideCommand": true,
  "forwardPorts": [8080],
  "portsAttributes": { "8080": { "label": "Poseidon synthetic UAT — Private only", "onAutoForward": "silent" } },
  "otherPortsAttributes": { "onAutoForward": "ignore" },
  "customizations": { "vscode": { "settings": { "remote.autoForwardPorts": false } } },
  "shutdownAction": "stopContainer"
}
