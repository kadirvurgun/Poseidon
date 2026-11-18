# tsconfig.json

Source: analiz/POSEIDON GUNCEL 30.10.2026/06_DEVELOPMENT/26.01_POSEIDON_Core_v0.1_Source/src/Frontend/tsconfig.json
SHA256: 2d85adfa96e126497a12e26780a63e74c931ea8ff8762acfc0afc53ad43815d6

## lines:1-23

{
  "compilerOptions": {
    "target": "ES2022",
    "useDefineForClassFields": true,
    "lib": ["ES2022", "DOM", "DOM.Iterable"],
    "module": "ESNext",
    "skipLibCheck": true,
    "moduleResolution": "Bundler",
    "allowImportingTsExtensions": true,
    "resolveJsonModule": true,
    "isolatedModules": true,
    "noEmit": true,
    "jsx": "react-jsx",
    "strict": true,
    "types": [
      "node",
      "vite/client",
      "vitest/globals",
      "@testing-library/jest-dom"
    ]
  },
  "include": ["src", "vite.config.ts"]
}
