# UTS Game Cerdas — Dungeon Hunter: Intelligent Enemy Dungeon

**Topik 3** · Unity 6000.3.23f1 · URP · AI Navigation (NavMesh) · New Input System

Player menjelajahi dungeon, mencari **Dungeon Key**, membuka pintu ruang Artifact,
lalu mengambil **Ancient Artifact**. (Guardian menyusul di progress 2.)

> Status: **Progress Demo 1 (± 50% Fokus AI)**
>
> Fokus AI Topik 3: `Melee AI + Ranged AI + Maintain Distance + Flee/Retreat + Navigation + Guardian Behavior`
>
> |||
> |---|---|
> | ✅ Melee AI | ⬜ Flee / Retreat |
> | ✅ Ranged AI | ⬜ Navigation (lanjutan) |
> | ✅ Maintain Distance | ⬜ Guardian Behavior |

## Cara membuka

1. Unity Hub → **Add project from disk** → pilih folder `UTS-DungeonHunter`.
2. Buka scene `Assets/_DungeonHunter/Scenes/DungeonHunter_Progress.unity` → **Play**.
3. Kalau scene belum ada / ingin di-generate ulang: menu **Dungeon Hunter → Build Prototype Scene**.
   Layout level diatur lewat peta ASCII di `Assets/_DungeonHunter/Editor/DungeonSceneBuilder.cs`.

## Kontrol

| Input | Aksi |
|---|---|
| WASD | Bergerak |
| Mouse | Arah hadap |
| Klik kiri / Space | Serang (pedang) |
| Shift | Sprint (lebih berisik → terdengar enemy) |
| **F1** | **AI Debug Mode** |
| R | Main lagi (setelah menang / kalah) |

## Gameplay loop

```text
START (ruang awal)
 ↓
Explore hall → lawan Skeleton Warrior & Skeleton Archer
 ↓
Ambil Dungeon Key (ruang timur, dijaga Archer)
 ↓
Buka pintu ruang Artifact (utara, dijaga Archer)
 ↓
Ambil Artifact → WIN   (progress 2: harus mengalahkan Guardian dulu)
(HP player habis → LOSE)
```

## Arsitektur AI

```text
Perception ──► Memory ──► Decision (FSM) ──► Navigation (NavMesh) + Steering (Separation)
```

| Materi | Implementasi | File |
|---|---|---|
| Perception | Distance + FOV + Line of Sight (raycast) + close awareness + **Hearing** (noise langkah/serangan) | `AI/AIPerception.cs`, `Core/NoiseSystem.cs` |
| Memory | Last Known Position, Last Seen Time, sumber info (Vision/Hearing/Damage), kadaluarsa | `AI/AIMemory.cs` |
| Decision | Finite State Machine: Patrol, Investigate, Search, Chase, Attack, Reposition, Dead | `AI/EnemyAI.cs` |
| Movement / Steering | NavMeshAgent + **Separation** antar enemy | `AI/EnemyAI.cs` |
| Navigation (dasar) | Unity NavMesh (NavMeshSurface, NavMeshObstacle carve pada pintu), patrol waypoint | `Editor/DungeonSceneBuilder.cs` |
| Tactical | **Maintain Distance** (archer) | `AI/RangedEnemy.cs` |

### Tipe enemy

| Enemy | Jumlah | Perilaku |
|---|---|---|
| **Skeleton Warrior** (Melee AI) | 2 | Patroli waypoint → Chase → Attack (windup, bisa dihindari). Kehilangan player → ke Last Known Position → Search → Patrol |
| **Skeleton Archer** (Ranged AI) | 3 | Berjaga & look-around → **Maintain Distance** 5–9 m: terlalu jauh = mendekat, ideal = membidik & menembak, terlalu dekat = mundur sambil menghadap player (evaluasi 7 arah × 2 jarak di NavMesh, bisa menyamping di pojok). Terpojok → tetap menembak, lalu coba mundur lagi |

### FSM Melee

```text
PATROL ──lihat player──► CHASE ──dalam jangkauan──► ATTACK
  ▲  └──dengar suara──► INVESTIGATE ◄──hilang LOS──┘
  │                          │ sampai di LKP
  └──── selesai mencari ─── SEARCH
```

### FSM Ranged (Maintain Distance)

```text
PATROL (sentry) ──lihat player──► CHASE ──jarak ≤ 9 m──► ATTACK (aim → shoot)
                                    ▲                      │  ▲
                         jarak > 10 m└──────────────────────┘  │ jarak ≥ 6.5 m
                                                   jarak < 5 m ▼  │
                                                          REPOSITION (mundur)
hilang Line of Sight ──► INVESTIGATE ──► SEARCH ──► PATROL
```

## AI Debug Mode (F1)

Panel di atas setiap enemy: **State, alasan keputusan (Why), Target, Can See (+ alasan
tidak melihat: luar radius / luar FOV / terhalang), Distance, Destination, Path,
Last Known Position**, HP, dan info khusus (status windup melee, range & aiming archer).

Garis di Game view: FOV (kuning / merah saat melihat), path NavMesh (cyan),
Last Known Position (magenta), attack range melee (merah), ring maintain distance (oranye).

## Skenario uji untuk demo progress

1. **Perception** — tekan F1, berdiri di luar radius / di belakang enemy / di balik pilar hall → `Can See: FALSE (alasan)`.
2. **Memory** — terlihat Warrior → CHASE, sembunyi di balik pilar → INVESTIGATE ke titik magenta (LKP) → SEARCH → PATROL.
3. **Hearing** — sprint (Shift) di dekat enemy yang membelakangi → INVESTIGATE "Mendengar suara".
4. **Melee AI** — dekati Warrior → ATTACK, perhatikan windup (kedip kuning) lalu menghindar.
5. **Ranged AI + Maintain Distance** — dekati Archer → REPOSITION (mundur), jauhi → CHASE, jarak ideal → ATTACK (kedip oranye = membidik).
6. **Win / Lose** — ambil Artifact / biarkan HP habis.

## Progress

**Progress 1 (sekarang)**
- [x] Level dungeon playable & NavMesh dasar
- [x] Player controller, melee combat, HP, objective (key → pintu → artifact), Win & Lose, HUD
- [x] Perception (vision + hearing) & Memory
- [x] FSM decision making + steering separation
- [x] **Melee AI** (Skeleton Warrior ×2)
- [x] **Ranged AI** (Skeleton Archer ×3)
- [x] **Maintain Distance** (tactical behavior archer)
- [x] AI Debug Mode (F1)

**Progress 2**
- [ ] **Flee / Retreat** — enemy kabur ke titik aman saat HP rendah, regen, lalu kembali
- [ ] **Navigation** lanjutan
- [ ] **Guardian Behavior** — boss penjaga Artifact (arena/leash, serangan khusus, fase)
- [ ] Aset final, animasi, audio, VFX, UI, video demo

## Pembagian kerja (usulan)

| Anggota | Fokus |
|---|---|
| Anggota 1 | Perception, Memory, FSM, AI Debug Mode |
| Anggota 2 | Melee AI, Ranged AI, Maintain Distance, NavMesh |
| Anggota 3 | Player, combat, GameManager, objective, HUD, level |
