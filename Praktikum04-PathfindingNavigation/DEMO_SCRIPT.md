# Naskah Demo — Praktikum 4: A* Grid Pathfinder & Unity NavMesh Navigation

Durasi target: **± 10–12 menit** (Bagian A ± 6 menit, Bagian B ± 4 menit, penutup ± 1 menit).

Format tiap langkah:
- 🎬 **Aksi** = apa yang dilakukan di Unity/layar
- 🗣️ **Narasi** = apa yang diucapkan

---

## Persiapan Sebelum Demo (checklist)

- [ ] Buka project `Praktikum04-PathfindingNavigation` di Unity 6.
- [ ] Scene `P04_AStarGrid` siap: `AStarSystem` (GridManager + AStarPathfinder), `StartMaker`, `GoalMaker`, `Agent`, obstacle `Cube`–`Cube (7)` sudah di layer obstacle.
- [ ] Scene `P04_NavMesh` siap: Plane + `NavMeshSurface` (sudah di-Bake), NPC (`NavMeshAgent` + `NavMeshChaser` + `NavMeshPathDebugger`), Player (`PlayerTargetMovement`), obstacle statis, dan 1 obstacle dengan `NavMeshObstacle`.
  > ⚠️ Cek: file scene `P04_NavMesh.unity` yang ter-commit saat ini baru berisi Main Camera + Directional Light. Pastikan scene NavMesh sudah dibangun dan **di-save** sebelum demo.
- [ ] Tombol **Gizmos** di Game view aktif (agar garis path `NavMeshPathDebugger` terlihat).
- [ ] Buka `AStarPathFinder.cs` dan `NavMeshChaser.cs` di editor kode untuk ditunjukkan.
- [ ] Console di-clear.

---

## 1. Pembukaan (± 30 detik)

🗣️ **Narasi:**
> "Pada Praktikum 4 ini saya mengimplementasikan pathfinding dan navigation dengan dua pendekatan. Bagian A: algoritma A* yang saya tulis manual di atas grid. Bagian B: navigasi menggunakan Unity NavMesh.
>
> Poin pentingnya: **pathfinding** itu *menentukan jalur*, **navigation / path following** itu *mengikuti jalur*, dan **movement** adalah *gerakan aktual* NPC. Ketiganya berbeda, dan demo ini akan menunjukkan pemisahan tersebut."

🗣️ **Kenapa butuh pathfinding? (menjawab "Mengapa Seek saja tidak cukup")**
> "Kalau NPC hanya pakai steering Seek, ia hanya bergerak lurus ke arah target. Begitu ada obstacle besar seperti dinding berbentuk U, NPC akan menabrak dan terjebak, karena Seek hanya melihat arah, bukan struktur peta. Pathfinding melihat seluruh peta sebagai graph, jadi bisa menemukan jalan memutar."

---

## 2. Bagian A — A* Manual pada Grid (± 6 menit)

### 2.1 Grid sebagai Graph (± 1 menit)

🎬 **Aksi:** Buka scene `P04_AStarGrid`. Tekan **Play**. Tunjukkan grid 10×10 (putih = walkable, hitam = obstacle).

🗣️ **Narasi:**
> "Ini grid 10×10 dengan `cellSize` 1. Setiap **cell adalah node**, dan hubungan antar-cell yang bertetangga adalah **edge**. Jadi grid ini sebenarnya sebuah graph."

🎬 **Aksi:** Tunjukkan `GridManager.cs` baris 41 (`Physics.CheckBox`).

> "Deteksi obstacle dilakukan saat grid dibuat. Untuk setiap cell saya lakukan `Physics.CheckBox` terhadap `obstacleMask`. Kalau kena collider obstacle, node ditandai `walkable = false` dan diwarnai hitam."

🎬 **Aksi:** Tunjukkan `GridManager.cs` baris 133–136 (`GetNeighbors`).

> "Tetangga hanya 4 arah: kanan, kiri, atas, bawah. Tidak ada diagonal."

### 2.2 Data Node & Rumus A* (± 1 menit)

🎬 **Aksi:** Tunjukkan `GridNode.cs`.

🗣️ **Narasi:**
> "Setiap node menyimpan `gCost`, `hCost`, dan `parent`. `FCost` dihitung dari `gCost + hCost`.
> - **gCost** = biaya nyata dari Start ke node ini. Tiap langkah biayanya 10.
> - **hCost** = estimasi biaya dari node ini ke Goal (heuristic).
> - **fCost** = total estimasi jalur jika lewat node ini. A* selalu memproses node dengan fCost terkecil dulu."

🎬 **Aksi:** Tunjukkan `AStarPathFinder.cs` baris 193 (`GetHeuristic`).

> "Heuristic yang saya pakai **Manhattan Distance**: `(|dx| + |dy|) × 10`. Ini cocok untuk grid 4 arah karena agent memang hanya bisa bergerak horizontal dan vertikal — jarak Manhattan adalah jumlah langkah minimum tanpa obstacle. Jadi nilainya tidak pernah melebihi biaya sebenarnya (*admissible*), sehingga A* dijamin menemukan jalur terpendek."

### 2.3 Open Set, Closed Set, dan Hasil Visualisasi (± 1,5 menit)

🎬 **Aksi:** Kembali ke Game/Scene view, tunjukkan warna hasil pencarian.

🗣️ **Narasi:**
> "Keterangan warna:
> - ⬜ Putih = walkable, belum disentuh
> - ⬛ Hitam = obstacle
> - 🟨 Kuning = node yang pernah masuk **Open Set**
> - 🟧 Oranye = node yang sudah masuk **Closed Set** (sudah dievaluasi)
> - 🟦 Cyan = **final path**
> - 🟩 Hijau = Start, 🟥 Merah = Goal"

🎬 **Aksi:** Tunjukkan `AStarPathFinder.cs` baris 76–81 dan 120–128.

> "**Open Set** adalah daftar node kandidat yang sudah ditemukan tapi belum dievaluasi. **Closed Set** adalah node yang sudah selesai dievaluasi dan tidak perlu diperiksa lagi.
>
> Di setiap iterasi, saya ambil node dengan fCost terkecil dari Open Set — kalau fCost-nya sama, yang hCost-nya lebih kecil yang menang. Node itu dipindah ke Closed Set. Lalu setiap tetangganya dicek: kalau obstacle atau sudah di Closed Set, dilewati. Kalau jalur lewat node sekarang lebih murah (`tentativeGCost < neighbor.gCost`), parent-nya di-update dan tetangga dimasukkan ke Open Set."

🎬 **Aksi:** Tunjukkan Console: `Path ditemukan. Node path: N`.

### 2.4 Reconstruct Path & Parent (± 45 detik)

🎬 **Aksi:** Tunjukkan `AStarPathFinder.cs` baris 203 (`ReconstructPath`).

🗣️ **Narasi:**
> "Saat Goal diambil dari Open Set, pencarian selesai. Tapi A* tidak menyimpan jalur secara langsung — yang disimpan hanya **parent** tiap node, yaitu 'saya datang dari node mana'. Jadi saya mulai dari Goal, ikuti `parent` terus sampai Start, lalu list-nya di-**Reverse** agar urutannya Start → Goal. Tanpa parent, kita hanya tahu Goal bisa dicapai, tapi tidak tahu lewat mana."

### 2.5 Agent Mengikuti Path (± 45 detik)

🎬 **Aksi:** Tunjukkan `Agent` (Capsule) bergerak menyusuri cell cyan. Tunjukkan `AgentPathFollower.cs`.

🗣️ **Narasi:**
> "Ini bagian **path following**. `AgentPathFollower` tidak menghitung jalur sama sekali — ia hanya membaca `currentPath` dari pathfinder, lalu bergerak ke waypoint satu per satu dengan `Vector3.MoveTowards`. Kalau jaraknya sudah di bawah `waypointThreshold`, lanjut ke waypoint berikutnya. Rotasi dihaluskan dengan `Slerp`.
>
> Jadi jelas pemisahannya: **AStarPathfinder = pathfinding**, **AgentPathFollower = path following + movement**."

### 2.6 Eksperimen A* (± 1,5 menit)

> 💡 Catatan teknis: grid dibuat di `Awake()` dan path dihitung di `Start()`. Untuk eksperimen, **stop Play → ubah scene → Play lagi** agar grid dan agent ter-reset. (Context menu **Find Path** pada komponen `AStarPathfinder` bisa dipakai untuk menghitung ulang warna path, tetapi agent tidak me-reset index waypoint-nya.)

**Eksperimen 1 — Pindahkan Goal**

🎬 **Aksi:** Stop. Geser `GoalMaker` ke posisi lain. Play.

🗣️ > "Setelah Goal dipindah, jalur berubah menyesuaikan posisi baru, dan area yang dieksplorasi (kuning/oranye) juga berubah karena heuristic sekarang mengarah ke Goal yang berbeda."

**Eksperimen 2 — Tambah Dinding/Obstacle**

🎬 **Aksi:** Stop. Duplikat beberapa `Cube` menjadi dinding panjang di antara Start dan Goal (pastikan layer obstacle). Play.

🗣️ > "Dengan dinding tambahan, A* memutari obstacle. Terlihat Closed Set jadi lebih banyak karena algoritma harus mengeksplorasi lebih jauh sebelum menemukan celah."

**Eksperimen 3 — Goal Terisolasi**

🎬 **Aksi:** Stop. Kurung `GoalMaker` dengan Cube di keempat sisinya. Play. Tunjukkan Console.

🗣️ > "Sekarang Goal terkurung. A* akan terus mengeksplorasi sampai Open Set kosong — semua node yang bisa dicapai menjadi oranye — lalu Console menampilkan **'Path tidak ditemukan.'** `currentPath` dikosongkan, sehingga agent diam dan tidak mencoba berjalan menembus dinding."

---

## 3. Bagian B — Unity NavMesh (± 4 menit)

### 3.1 NavMeshSurface & Bake (± 45 detik)

🎬 **Aksi:** Buka scene `P04_NavMesh`. Pilih objek dengan `NavMeshSurface`, tunjukkan area biru hasil Bake di Scene view.

🗣️ **Narasi:**
> "`NavMeshSurface` menganalisis geometri level dan menghasilkan **NavMesh** — area biru ini — yaitu permukaan yang bisa dilalui agent, dengan memperhitungkan radius dan tinggi agent. Area di sekitar obstacle berlubang, artinya tidak bisa dilewati. Kalau A* manual memakai grid, NavMesh memakai poligon, jadi lebih efisien dan jalurnya lebih halus."

### 3.2 NavMeshAgent & Stopping Distance (± 1 menit)

🎬 **Aksi:** Pilih NPC, tunjukkan Inspector `NavMeshAgent` (Speed, Angular Speed, **Stopping Distance**). Play.

🗣️ **Narasi:**
> "`NavMeshAgent` melakukan dua hal sekaligus: meminta jalur ke NavMesh (pathfinding internal Unity, berbasis A* pada poligon) lalu **mengikuti** jalur itu dengan steering dan avoidance-nya sendiri. Saya cukup memanggil `SetDestination()`."

🎬 **Aksi:** Tunjukkan NPC memutari obstacle menuju Player, lalu berhenti sebelum menempel.

> "Terlihat NPC memutari obstacle dan **berhenti pada jarak tertentu** dari target. Itu fungsi **Stopping Distance** — agent dianggap sudah sampai ketika sisa jaraknya di bawah nilai ini, sehingga NPC tidak menabrak atau mendorong target."

### 3.3 Visualisasi Path (± 30 detik)

🎬 **Aksi:** Aktifkan Gizmos, tunjukkan garis cyan dengan bola di setiap sudut.

🗣️ **Narasi:**
> "`NavMeshPathDebugger` menggambar `agent.path.corners` dengan Gizmos. Corners adalah titik belok jalur. Perhatikan bahwa jalur NavMesh hanya berisi sedikit titik — berbeda dengan A* grid yang berisi setiap cell."

### 3.4 Eksperimen: Target Bergerak & Repathing (± 1 menit)

🎬 **Aksi:** Gerakkan Player dengan **W/A/S/D**. Tunjukkan garis path ter-update dan NPC berbelok mengikuti.

🗣️ **Narasi:**
> "Saat target bergerak, NPC melakukan **repathing**. Tapi perhatikan kodenya di `NavMeshChaser.cs`: saya **tidak** memanggil `SetDestination` setiap frame. Ada dua kontrol:
> 1. `repathInterval = 0.25` detik — pengecekan hanya dilakukan 4 kali per detik.
> 2. `targetMoveThreshold = 0.5` — jalur baru hanya diminta kalau target sudah bergeser minimal 0,5 unit.
>
> Kenapa? Karena menghitung path itu mahal. Kalau ada banyak NPC dan semuanya repath tiap frame, CPU terbuang untuk hasil yang hampir sama. Perbedaan 1 frame tidak terlihat oleh pemain, jadi repathing terkontrol jauh lebih efisien."

### 3.5 Eksperimen: NavMeshObstacle Carve ON vs OFF (± 1 menit)

🎬 **Aksi:** Pilih obstacle yang punya `NavMeshObstacle`. Set **Carve = OFF**, Play, tempatkan obstacle di jalur NPC.

🗣️ > "Dengan Carve OFF, obstacle **tidak melubangi** NavMesh. Jalur yang dihitung tetap menembus obstacle; agent hanya menghindar secara lokal dengan avoidance, sehingga bisa terlihat mendorong-dorong atau tersangkut di depannya."

🎬 **Aksi:** Set **Carve = ON**, Play lagi. Tunjukkan lubang pada NavMesh di Scene view dan garis path yang memutar.

🗣️ > "Dengan Carve ON, obstacle **memotong NavMesh secara runtime**. Area itu benar-benar hilang dari NavMesh, jadi path baru langsung memutari obstacle. Ini cocok untuk obstacle dinamis yang posisinya bisa berubah, misalnya pintu atau kotak yang bisa didorong."

---

## 4. Penutup (± 45 detik)

🗣️ **Narasi:**
> "Kesimpulannya:
> - **Pathfinding** (A* / NavMesh query) → *menentukan* jalur.
> - **Navigation / path following** (`AgentPathFollower` / `NavMeshAgent`) → *mengikuti* jalur.
> - **Movement** (`MoveTowards`, steering agent) → *gerakan aktual* NPC.
>
> Di Bagian A, ketiganya saya pisahkan secara eksplisit di script yang berbeda. Di Bagian B, `NavMeshAgent` membungkus semuanya, tetapi konsep di belakangnya sama dengan A* yang saya implementasikan manual. Terima kasih."

---

## 5. Cadangan Jawaban Tanya-Jawab

| Pertanyaan | Jawaban singkat |
|---|---|
| Kenapa Seek saja tidak cukup? | Seek hanya tahu arah ke target, tidak tahu struktur peta. Obstacle besar/cekung membuat NPC terjebak (local minimum). Pathfinding merencanakan jalur global. |
| Fungsi gCost, hCost, fCost? | g = biaya nyata dari Start; h = estimasi ke Goal; f = g + h, dipakai memilih node paling menjanjikan. |
| Kenapa Manhattan untuk grid 4 arah? | Gerakan hanya horizontal/vertikal, jadi jarak minimum = \|dx\| + \|dy\|. Tidak pernah overestimate → jalur optimal. Euclidean juga admissible tapi underestimate lebih jauh sehingga eksplorasi lebih banyak. |
| Fungsi parent? | Menyimpan node asal. Dipakai untuk menelusuri balik dari Goal ke Start saat reconstruct path. |
| Open Set vs Closed Set? | Open = kandidat yang belum dievaluasi; Closed = sudah dievaluasi, tidak diproses ulang. |
| Pathfinding vs path following? | Pathfinding menghasilkan daftar titik; path following menggerakkan agent melewati titik tersebut. |
| Kenapa biaya langkah 10, bukan 1? | Konvensi agar mudah dikembangkan ke diagonal (≈14 = 10·√2) tanpa float. |
| Kalau fCost sama? | Tie-breaker: pilih hCost terkecil (lebih dekat ke Goal) — lihat `GetLowestFCostNode`. |
| Fungsi NavMeshSurface? | Membake area walkable dari geometri scene menjadi NavMesh. |
| Fungsi NavMeshAgent? | Mencari jalur di NavMesh dan menggerakkan objek mengikutinya (steering + avoidance). |
| Fungsi Stopping Distance? | Jarak di mana agent berhenti sebelum mencapai titik tujuan persis. |
| NavMeshObstacle + Carve? | Carve melubangi NavMesh secara runtime sehingga path memutar; tanpa Carve hanya local avoidance. |
| Kenapa tidak repath tiap frame? | Mahal secara komputasi; hasil hampir identik antar-frame. Pakai interval + threshold jarak. |
| Kelemahan implementasi Open Set dengan `List`? | Mencari fCost terkecil O(n). Untuk grid besar lebih baik pakai priority queue / binary heap. |
| Apa yang terjadi kalau Start/Goal di obstacle? | Pencarian dibatalkan dengan warning "Start/Goal berada pada obstacle." |

---

## 6. Checklist Screenshot Pengumpulan

**Bagian A**
- [ ] Grid + obstacle (sebelum/tanpa path, atau matikan `showOpenClosed`)
- [ ] Open Set (kuning), Closed Set (oranye), final path (cyan)
- [ ] Agent sedang mengikuti path
- [ ] Eksperimen: Goal dipindah, dinding tambahan, Goal terisolasi (+ Console "Path tidak ditemukan.")

**Bagian B**
- [ ] Hasil Bake NavMesh (area biru)
- [ ] NPC memutari obstacle menuju target
- [ ] Visualisasi path (Gizmos cyan)
- [ ] NavMeshObstacle Carve OFF vs Carve ON
