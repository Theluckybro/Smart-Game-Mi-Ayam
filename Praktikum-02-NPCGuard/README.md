# Praktikum 2 — NPC Guard: Sensor + Memory + Decision

Mata Kuliah **Game Cerdas** — S1 Teknik Informatika, Semester 7
Unity **6000.3.23f1** · C# · Built-in 3D template

Implementasi [MODUL PRAKTIKUM 2](https://darlis-its.github.io/materi-gim-cerdas/)
(`praktikum/modul-praktikum-2-npc-guard-sensor-memory-decision.md`).

## Menjalankan

1. Buka project ini lewat Unity Hub (Unity 6000.3.23f1).
2. Buka scene `Assets/Scenes/Praktikum02_NPCGuard.unity`.
3. Tekan **Play**. Gerakkan Player dengan `WASD` atau `Arrow Key`.
4. Pilih `NPC_Guard` di Hierarchy dan aktifkan **Gizmos** untuk melihat
   view radius, field of view, garis Line of Sight, dan Last Known Position.

## Arsitektur

```text
Environment → Perception → Memory → Decision → Action → Environment
```

| Konsep      | Implementasi                                                    |
|-------------|-----------------------------------------------------------------|
| Environment | Ground, Wall (layer `Obstacle`), PatrolPoints, NavMesh          |
| Perception  | `NPCSensor.cs` — radius, field of view, line of sight           |
| Memory      | `NPCBrain.lastKnownPosition` + `hasLastKnownPosition`           |
| Decision    | `NPCBrain.MakeDecision()` — 3 prioritas → `Patrol/Chase/Search` |
| Action      | `NPCBrain.Patrol/Chase/Search()` → `NavMeshAgent.SetDestination`|

State machine:

```text
PATROL ──player terlihat──▶ CHASE ──player hilang──▶ SEARCH ──timeout──▶ PATROL
                              ▲                         │
                              └───player ditemukan──────┘
```

## Struktur

```text
Assets
├── Editor
│   ├── Praktikum02SceneBuilder.cs   # menu "Praktikum 2 → Build Scene ..."
│   └── Praktikum02Verify.cs         # menu "Praktikum 2 → Verify Scene"
├── Materials                        # Ground / Player / NPC / Wall
├── Prefabs
├── Scenes
│   ├── Praktikum02_NPCGuard.unity
│   └── Praktikum02_NPCGuard/NavMesh-Navigation.asset
└── Scripts
    ├── PlayerController.cs          # verbatim dari modul (bagian 12)
    ├── NPCSensor.cs                 # verbatim dari modul (bagian 29)
    └── NPCBrain.cs                  # verbatim dari modul (bagian 38)
```

Hierarchy scene mengikuti bagian 77 modul: `Main Camera`, `Directional Light`,
`Ground`, `Navigation`, `Player`, `NPC_Guard` (+`DirectionMarker`), `Wall`,
`PatrolPoints` (`Point1`–`Point4`).

## Parameter (bagian 41)

| Komponen     | Parameter           | Nilai |
|--------------|---------------------|------:|
| NPCSensor    | View Radius         |     8 |
| NPCSensor    | View Angle          |    90 |
| NPCSensor    | Eye Height          |   1.2 |
| NPCSensor    | Obstacle Mask       | `Obstacle` |
| NPCBrain     | Waypoint Tolerance  |   0.7 |
| NPCBrain     | Patrol Speed        |     2 |
| NPCBrain     | Chase Speed         |     4 |
| NPCBrain     | Search Duration     |     4 |
| NPCBrain     | Search Tolerance    |   0.8 |
| NavMeshAgent | Speed / Angular / Accel / Stopping | 3 / 360 / 8 / 0.3 |
| PlayerController | Move / Rotation Speed | 5 / 10 |

## Verifikasi otomatis

Menu **Praktikum 2 → Verify Scene** menjalankan 52 pemeriksaan: seluruh nilai
Inspector, referensi antar-komponen, NavMesh di bawah NPC dan keempat waypoint,
serta lima uji logika `NPCSensor` (di depan / di belakang / di luar radius /
sebelum Wall / di balik Wall). Semuanya lolos.

Headless:

```bash
Unity.exe -projectPath . -batchmode -nographics -quit \
  -executeMethod Praktikum02Verify.Run -logFile verify.log
```

## Catatan bake NavMesh

`Praktikum02SceneBuilder` menonaktifkan `Player` dan `NPC_Guard` sementara
saat bake, karena `NavMeshSurface` default mengumpulkan **semua** render mesh —
kapsul Player/NPC akan terpahat jadi lubang di NavMesh dan membuat NavMeshAgent
gagal ditempatkan. Bila melakukan **re-bake manual** dari Inspector, nonaktifkan
dulu kedua object tersebut.

## Sisa deliverable (bagian 88 modul)

- [x] A. Unity Project / repository Git
- [ ] B. Screenshot Scene
- [ ] C. Screenshot Gizmos
- [ ] D. Screenshot State
- [ ] E. Video demo 1–3 menit (PATROL → CHASE → SEARCH → PATROL, dan SEARCH → CHASE)
- [ ] F. Laporan singkat (12 bagian)
